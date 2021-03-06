﻿/**********************************************************\
|                                                          |
|                          hprose                          |
|                                                          |
| Official WebSite: http://www.hprose.com/                 |
|                   http://www.hprose.org/                 |
|                                                          |
\**********************************************************/
/**********************************************************\
 *                                                        *
 * HproseHttpListenerService.cs                           *
 *                                                        *
 * hprose http listener service class for C#.             *
 *                                                        *
 * LastModified: May 30, 2015                             *
 * Author: Ma Bingyao <andot@hprose.com>                  *
 *                                                        *
\**********************************************************/
#if !(dotNET10 || dotNET11 || ClientOnly || Smartphone)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Principal;
using Hprose.Common;

namespace Hprose.Server {

    public class HproseHttpListenerService : HproseService {

        private bool crossDomainEnabled = false;
        private bool p3pEnabled = false;
        private bool getEnabled = true;
        private bool compressionEnabled = false;
        private Dictionary<string, bool> origins = new Dictionary<string, bool>();
        public event SendHeaderEvent OnSendHeader = null;

        [ThreadStatic]
        private static HttpListenerContext currentContext;

        protected override object[] FixArguments(Type[] argumentTypes, object[] arguments, int count, HproseContext context) {
            HproseHttpListenerContext currentContext = (HproseHttpListenerContext)context;
            if (argumentTypes.Length != count) {
                object[] args = new object[argumentTypes.Length];
                System.Array.Copy(arguments, 0, args, 0, count);
                Type argType = argumentTypes[count];
                if (argType == typeof(HproseContext) ||
                    argType == typeof(HproseHttpListenerContext)) {
                    args[count] = currentContext;
                }
                else if (argType == typeof(HttpListenerContext)) {
                    args[count] = currentContext.Context;
                }
                else if (argType == typeof(HttpListenerRequest)) {
                    args[count] = currentContext.Request;
                }
                else if (argType == typeof(HttpListenerResponse)) {
                    args[count] = currentContext.Response;
                }
                else if (argType == typeof(IPrincipal)) {
                    args[count] = currentContext.User;
                }
                return args;
            }
            return arguments;
        }

        public override HproseMethods GlobalMethods {
            get {
                if (gMethods == null) {
                    gMethods = new HproseHttpListenerMethods();
                }
                return gMethods;
            }
        }

        public static new HttpListenerContext CurrentContext {
            get {
                return currentContext;
            }
        }

        public bool IsCrossDomainEnabled {
            get {
                return crossDomainEnabled;
            }
            set {
                crossDomainEnabled = value;
            }
        }

        public bool IsP3pEnabled {
            get {
                return p3pEnabled;
            }
            set {
                p3pEnabled = value;
            }
        }

        public bool IsGetEnabled {
            get {
                return getEnabled;
            }
            set {
                getEnabled = value;
            }
        }

        public bool IsCompressionEnabled {
            get {
                return compressionEnabled;
            }
            set {
                compressionEnabled = value;
            }
        }

        public void AddAccessControlAllowOrigin(string origin) {
            origins[origin] = true;
        }

        public void RemoveAccessControlAllowOrigin(string origin) {
            origins.Remove(origin);
        }

        private Stream GetOutputStream(HproseHttpListenerContext currentContext) {
            Stream ostream = currentContext.Response.OutputStream;
            if (compressionEnabled) {
                string acceptEncoding = currentContext.Request.Headers["Accept-Encoding"];
                if (acceptEncoding != null) {
                    acceptEncoding = acceptEncoding.ToLower();
                    if (acceptEncoding.IndexOf("deflate") > -1) {
                        ostream = new DeflateStream(ostream, CompressionMode.Compress);
                    }
                    else if (acceptEncoding.IndexOf("gzip") > -1) {
                        ostream = new GZipStream(ostream, CompressionMode.Compress);
                    }
                }
            }
            return ostream;
        }

        private class NonBlockingWriteContext {
            public HproseHttpListenerContext currentContext;
            public Stream ostream;
        }

        private void NonBlockingWriteCallback(IAsyncResult asyncResult) {
            NonBlockingWriteContext context = (NonBlockingWriteContext)asyncResult.AsyncState;
            try {
                context.ostream.EndWrite(asyncResult);
            }
            catch (Exception e) {
                FireErrorEvent(e, context.currentContext);
            }
            finally {
                try {
                    if (context.ostream.CanWrite) {
                        context.ostream.Close();
                        context.currentContext.Response.Close();
                    }
                }
                catch (Exception) {
                }
            }
        }

        private class NonBlockingReadContext {
            public HproseHttpListenerContext currentContext;
            public HproseHttpListenerMethods methods;
            public Stream istream;
            public MemoryStream data;
            public int bufferlength;
            public byte[] buffer;
        }

        private void NonBlockingWrite(NonBlockingReadContext context) {
            currentContext = context.currentContext.Context;
            NonBlockingWriteContext writeContext = new NonBlockingWriteContext();
            writeContext.currentContext = context.currentContext;
            try {
                MemoryStream data = Handle(context.data, context.methods, context.currentContext);
                writeContext.ostream = GetOutputStream(context.currentContext);
                writeContext.ostream.BeginWrite(data.GetBuffer(), 0, (int)data.Length,
                        new AsyncCallback(NonBlockingWriteCallback), writeContext);
            }
            catch (Exception e) {
                FireErrorEvent(e, context.currentContext);
                try {
                    if (writeContext.ostream != null) {
                        writeContext.ostream.Close();
                    }
                    context.currentContext.Response.Close();
                }
                catch (Exception) { }
                return;
            }
            finally {
                currentContext = null;
            }
        }

        private void NonBlockingReadCallback(IAsyncResult asyncResult) {
            NonBlockingReadContext context = (NonBlockingReadContext)asyncResult.AsyncState;
            Stream istream = context.istream;
            try {
                if (istream.CanRead) {
                    int n = istream.EndRead(asyncResult);
                    if (n > 0) {
                        context.data.Write(context.buffer, 0, n);
                        istream.BeginRead(context.buffer, 0, context.bufferlength,
                                new AsyncCallback(NonBlockingReadCallback), context);
                        return;
                    }
                }
                else {
                    return;
                }
                istream.Close();
            }
            catch (Exception e) {
                FireErrorEvent(e, context.currentContext);
                try {
                    istream.Close();
                    context.currentContext.Response.Close();
                }
                catch (Exception) { }
                return;
            }
            NonBlockingWrite(context);
        }

        private void NonBlockingHandle(HproseHttpListenerContext currentContext, HproseHttpListenerMethods methods) {
            NonBlockingReadContext context = new NonBlockingReadContext();
            context.currentContext = currentContext;
            context.methods = methods;
            context.istream = currentContext.Request.InputStream;
            int len = (int)currentContext.Request.ContentLength64;
            context.data = (len > 0) ? new MemoryStream(len) : new MemoryStream();
            context.bufferlength = (len > 81920 || len < 0) ? 81920 : len;
            context.buffer = new byte[context.bufferlength];
            context.istream.BeginRead(context.buffer, 0, context.bufferlength, new AsyncCallback(NonBlockingReadCallback), context);
        }

        private void SendHeader(HproseHttpListenerContext currentContext) {
            if (OnSendHeader != null) {
                OnSendHeader(currentContext);
            }
            HttpListenerRequest request = currentContext.Request;
            HttpListenerResponse response = currentContext.Response;
            response.ContentType = "text/plain";
            if (p3pEnabled) {
                response.AddHeader("P3P",
                    "CP=\"CAO DSP COR CUR ADM DEV TAI PSA PSD " +
                    "IVAi IVDi CONi TELo OTPi OUR DELi SAMi " +
                    "OTRi UNRi PUBi IND PHY ONL UNI PUR FIN " +
                    "COM NAV INT DEM CNT STA POL HEA PRE GOV\"");
            }
            if (crossDomainEnabled) {
                string origin = request.Headers["Origin"];
                if (origin != null && origin != "" && origin != "null") {
                    if (origins.Count == 0 || origins.ContainsKey(origin)) {
                        response.AddHeader("Access-Control-Allow-Origin", origin);
                        response.AddHeader("Access-Control-Allow-Credentials", "true");
                    }
                }
                else {
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                }
            }
            if (compressionEnabled) {
                string acceptEncoding = request.Headers["Accept-Encoding"];
                if (acceptEncoding != null) {
                    acceptEncoding = acceptEncoding.ToLower();
                    if (acceptEncoding.IndexOf("deflate") > -1) {
                        response.AddHeader("Content-Encoding", "deflate");
                    }
                    else if (acceptEncoding.IndexOf("gzip") > -1) {
                        response.AddHeader("Content-Encoding", "gzip");
                    }
                }
            }
        }

        protected void Handle(HttpListenerContext httpListenerContext) {
            Handle(httpListenerContext, null);
        }

        protected void Handle(HttpListenerContext httpListenerContext, HproseHttpListenerMethods methods) {
            HproseHttpListenerContext context = new HproseHttpListenerContext(httpListenerContext);
            SendHeader(context);
            string method = context.Request.HttpMethod;
            if (method == "GET") {
                if (getEnabled) {
                    MemoryStream data = DoFunctionList(methods, context);
                    NonBlockingWriteContext writeContext = new NonBlockingWriteContext();
                    writeContext.currentContext = context;
                    writeContext.ostream = GetOutputStream(context);
                    writeContext.ostream.BeginWrite(data.GetBuffer(), 0, (int)data.Length,
                        new AsyncCallback(NonBlockingWriteCallback), writeContext);
                }
                else {
                    context.Response.StatusCode = 403;
                    context.Response.Close();
                }
            }
            else if (method == "POST") {
                NonBlockingHandle(context, methods);
            }
            else {
                context.Response.Close();
            }
        }
    }
}
#endif
