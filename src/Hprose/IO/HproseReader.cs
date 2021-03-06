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
 * HproseReader.cs                                        *
 *                                                        *
 * hprose reader class for C#.                            *
 *                                                        *
 * LastModified: Jan 17, 2015                             *
 * Author: Ma Bingyao <andot@hprose.com>                  *
 *                                                        *
\**********************************************************/
using System;
using System.Collections;
#if !(dotNET10 || dotNET11 || dotNETCF10)
using System.Collections.Generic;
#endif
using System.Globalization;
using System.Numerics;
using System.IO;
using System.Text;
using System.Reflection;
using Hprose.Common;
#if !(PocketPC || Smartphone || WindowsCE || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
using Hprose.Reflection;
#endif

namespace Hprose.IO {
    interface ReaderRefer {
        void Set(object obj);
        object Read(int index);
        void Reset();
    }
    sealed class FakeReaderRefer : ReaderRefer {
        public void Set(object obj) {}
        public object Read(int index) {
            throw new HproseException("Unexpected serialize tag '" +
                                       (char)HproseTags.TagRef +
                                       "' in stream");
        }
        public void Reset() {}
    }
    sealed class RealReaderRefer : ReaderRefer {
#if !(dotNET10 || dotNET11 || dotNETCF10)
        private List<object> references = new List<object>();
#else
        private ArrayList references = new ArrayList();
#endif
        public void Set(object obj) {
            references.Add(obj);
        }
        public object Read(int index) {
            return references[index];
        }
        public void Reset() {
            references.Clear();
        }
    }
    public sealed class HproseReader {
        public Stream stream;
        private HproseMode mode;
        private ReaderRefer refer;
#if !(dotNET10 || dotNET11 || dotNETCF10)
        private List<object> classref = new List<object>();
        private Dictionary<object, string[]> membersref = new Dictionary<object, string[]>();
#else
        private ArrayList classref = new ArrayList();
        private Hashtable membersref = new Hashtable();
#endif
        public HproseReader(Stream stream)
            : this(stream, HproseMode.MemberMode, false) {
        }
        public HproseReader(Stream stream, bool simple)
            : this(stream, HproseMode.MemberMode, simple) {
        }
        public HproseReader(Stream stream, HproseMode mode)
            : this(stream, mode, false) {
        }
        public HproseReader(Stream stream, HproseMode mode, bool simple) {
            this.stream = stream;
            this.mode = mode;
            this.refer = (simple ? new FakeReaderRefer() as ReaderRefer : new RealReaderRefer() as ReaderRefer);
        }

        public HproseException UnexpectedTag(int tag) {
            return UnexpectedTag(tag, null);
        }

        public HproseException UnexpectedTag(int tag, string expectTags) {
            if (tag == -1) {
                return new HproseException("No byte found in stream");
            }
            else if (expectTags == null) {
                return new HproseException("Unexpected serialize tag '" + (char)tag + "' in stream");
            }
            else {
                return new HproseException("Tag '" + expectTags + "' expected, but '" + (char)tag +
                                          "' found in stream");
            }
        }

        private HproseException CastError(string srctype, Type desttype) {
            return new HproseException(srctype + " can't change to " + desttype.FullName);
        }

        private HproseException CastError(object obj, Type type) {
            return new HproseException(obj.GetType().FullName + " can't change to " + type.FullName);
        }

        private void CheckTag(int tag, int expectTag) {
            if (tag != expectTag) throw UnexpectedTag(tag, new String((char)expectTag, 1));
        }

        public void CheckTag(int expectTag) {
            CheckTag(stream.ReadByte(), expectTag);
        }

        private int CheckTags(int tag, string expectTags) {
            if (expectTags.IndexOf((char)tag) == -1) throw UnexpectedTag(tag, expectTags);
            return tag;
        }

        public int CheckTags(string expectTags) {
            return CheckTags(stream.ReadByte(), expectTags);
        }

        private StringBuilder ReadUntil(int tag) {
            StringBuilder sb = new StringBuilder();
            int i = stream.ReadByte();
            while ((i != tag) && (i != -1)) {
                sb.Append((char)i);
                i = stream.ReadByte();
            }
            return sb;
        }

        private void SkipUntil(int tag) {
            int i = stream.ReadByte();
            while ((i != tag) && (i != -1)) {
                i = stream.ReadByte();
            }
        }

        public int ReadInt(int tag) {
            int result = 0;
            int sign = 1;
            int i = stream.ReadByte();
            switch (i) {
                case '-':
                    sign = -1;
                    goto case '+';
                case '+':
                    i = stream.ReadByte();
                    break;
            }
            while ((i != tag) && (i != -1)) {
                result *= 10;
                result += (i - '0') * sign;
                i = stream.ReadByte();
            }
            return result;
        }

        public long ReadLong(int tag) {
            long result = 0L;
            long sign = 1L;
            int i = stream.ReadByte();
            switch (i) {
                case '-':
                    sign = -1L;
                    goto case '+';
                case '+':
                    i = stream.ReadByte();
                    break;
            }
            while ((i != tag) && (i != -1)) {
                result *= 10L;
                result += (i - '0') * sign;
                i = stream.ReadByte();
            }
            return result;
        }

        public float ReadIntAsFloat() {
            float result = 0.0F;
            float sign = 1.0F;
            int i = stream.ReadByte();
            switch (i) {
                case '-':
                    sign = -1.0F;
                    goto case '+';
                case '+':
                    i = stream.ReadByte();
                    break;
            }
            while ((i != HproseTags.TagSemicolon) && (i != -1)) {
                result *= 10.0F;
                result += (i - '0') * sign;
                i = stream.ReadByte();
            }
            return result;
        }

        public double ReadIntAsDouble() {
            double result = 0.0;
            double sign = 1.0;
            int i = stream.ReadByte();
            switch (i) {
                case '-':
                    sign = -1.0;
                    goto case '+';
                case '+':
                    i = stream.ReadByte();
                    break;
            }
            while ((i != HproseTags.TagSemicolon) && (i != -1)) {
                result *= 10.0;
                result += (i - '0') * sign;
                i = stream.ReadByte();
            }
            return result;
        }

        public decimal ReadIntAsDecimal() {
            decimal result = 0.0M;
            decimal sign = 1.0M;
            int i = stream.ReadByte();
            switch (i) {
                case '-':
                    sign = -1.0M;
                    goto case '+';
                case '+':
                    i = stream.ReadByte();
                    break;
            }
            while ((i != HproseTags.TagSemicolon) && (i != -1)) {
                result *= 10.0M;
                result += (i - '0') * sign;
                i = stream.ReadByte();
            }
            return result;
        }

        private float ParseFloat(StringBuilder value) {
            return ParseFloat(value.ToString());
        }

        private float ParseFloat(String value) {
            try {
                return float.Parse(value);
            }
            catch (OverflowException) {
                return (value[0] == HproseTags.TagNeg) ?
                        float.NegativeInfinity :
                        float.PositiveInfinity;
            }
        }

        private double ParseDouble(StringBuilder value) {
            return ParseDouble(value.ToString());
        }

        private double ParseDouble(String value) {
            try {
                return double.Parse(value);
            }
            catch (OverflowException) {
                return (value[0] == HproseTags.TagNeg) ?
                        double.NegativeInfinity :
                        double.PositiveInfinity;
            }
        }

        private char ReadUTF8CharAsChar() {
            char u;
            int c = stream.ReadByte();
            switch (c >> 4) {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7: {
                    // 0xxx xxxx
                    u = (char)c;
                    break;
                }
                case 12:
                case 13: {
                    // 110x xxxx   10xx xxxx
                    int c2 = stream.ReadByte();
                    u = (char)(((c & 0x1f) << 6) |
                               (c2 & 0x3f));
                    break;
                }
                case 14: {
                    // 1110 xxxx  10xx xxxx  10xx xxxx
                    int c2 = stream.ReadByte();
                    int c3 = stream.ReadByte();
                    u = (char)(((c & 0x0f) << 12) |
                              ((c2 & 0x3f) << 6) |
                               (c3 & 0x3f));
                    break;
                }
                default:
                    throw new HproseException("bad utf-8 encoding at " +
                                                  ((c < 0) ? "end of stream" :
                                                  "0x" + (c & 0xff).ToString("x2")));
            }
            return u;
        }

        private char[] ReadChars() {
            int count = ReadInt(HproseTags.TagQuote);
            char[] buf = new char[count];
            for (int i = 0; i < count; ++i) {
                int c = stream.ReadByte();
                switch (c >> 4) {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                    case 6:
                    case 7: {
                            // 0xxx xxxx
                            buf[i] = (char)c;
                            break;
                        }
                    case 12:
                    case 13: {
                            // 110x xxxx   10xx xxxx
                            int c2 = stream.ReadByte();
                            buf[i] = (char)(((c & 0x1f) << 6) |
                                             (c2 & 0x3f));
                            break;
                        }
                    case 14: {
                            // 1110 xxxx  10xx xxxx  10xx xxxx
                            int c2 = stream.ReadByte();
                            int c3 = stream.ReadByte();
                            buf[i] = (char)(((c & 0x0f) << 12) |
                                             ((c2 & 0x3f) << 6) |
                                             (c3 & 0x3f));
                            break;
                        }
                    case 15: {
                            // 1111 0xxx  10xx xxxx  10xx xxxx  10xx xxxx
                            if ((c & 0xf) <= 4) {
                                int c2 = stream.ReadByte();
                                int c3 = stream.ReadByte();
                                int c4 = stream.ReadByte();
                                int s = ((c & 0x07) << 18) |
                                        ((c2 & 0x3f) << 12) |
                                        ((c3 & 0x3f) << 6) |
                                        (c4 & 0x3f) - 0x10000;
                                if (0 <= s && s <= 0xfffff) {
                                    buf[i] = (char)(((s >> 10) & 0x03ff) | 0xd800);
                                    buf[++i] = (char)((s & 0x03ff) | 0xdc00);
                                    break;
                                }
                            }
                            goto default;
                            // no break here!! here need throw exception.
                        }
                    default:
                        throw new HproseException("bad utf-8 encoding at " +
                                                  ((c < 0) ? "end of stream" :
                                                  "0x" + (c & 0xff).ToString("x2")));
                }
            }
            stream.ReadByte();
            return buf;
        }

        private String ReadCharsAsString() {
            return new String(ReadChars());
        }

        private MemoryStream ReadUTF8CharAsStream() {
            MemoryStream ms = new MemoryStream();
            int c = stream.ReadByte();
            switch (c >> 4) {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7: {
                    ms.WriteByte((byte)c);
                    break;
                }
                case 12:
                case 13: {
                    int c2 = stream.ReadByte();
                    ms.WriteByte((byte)c);
                    ms.WriteByte((byte)c2);
                    break;
                }
                case 14: {
                    int c2 = stream.ReadByte();
                    int c3 = stream.ReadByte();
                    ms.WriteByte((byte)c);
                    ms.WriteByte((byte)c2);
                    ms.WriteByte((byte)c3);
                    break;
                }
                default:
                    throw new HproseException("bad utf-8 encoding at " +
                                              ((c < 0) ? "end of stream" :
                                              "0x" + (c & 0xff).ToString("x2")));
            }
            ms.Position = 0;
            return ms;
        }

        private MemoryStream ReadCharsAsStream() {
            int count = ReadInt(HproseTags.TagQuote);
            // here count is capacity, not the real size
            MemoryStream ms = new MemoryStream(count * 3);
            for (int i = 0; i < count; ++i) {
                int c = stream.ReadByte();
                switch (c >> 4) {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                    case 6:
                    case 7: {
                            ms.WriteByte((byte)c);
                            break;
                        }
                    case 12:
                    case 13: {
                            int c2 = stream.ReadByte();
                            ms.WriteByte((byte)c);
                            ms.WriteByte((byte)c2);
                            break;
                        }
                    case 14: {
                            int c2 = stream.ReadByte();
                            int c3 = stream.ReadByte();
                            ms.WriteByte((byte)c);
                            ms.WriteByte((byte)c2);
                            ms.WriteByte((byte)c3);
                            break;
                        }
                    case 15: {
                            // 1111 0xxx  10xx xxxx  10xx xxxx  10xx xxxx
                            if ((c & 0xf) <= 4) {
                                int c2 = stream.ReadByte();
                                int c3 = stream.ReadByte();
                                int c4 = stream.ReadByte();
                                int s = ((c & 0x07) << 18) |
                                        ((c2 & 0x3f) << 12) |
                                        ((c3 & 0x3f) << 6) |
                                        (c4 & 0x3f) - 0x10000;
                                if (0 <= s && s <= 0xfffff) {
                                    ms.WriteByte((byte)c);
                                    ms.WriteByte((byte)c2);
                                    ms.WriteByte((byte)c3);
                                    ms.WriteByte((byte)c4);
                                    break;
                                }
                            }
                            goto default;
                            // no break here!! here need throw exception.
                        }
                    default:
                        throw new HproseException("bad utf-8 encoding at " +
                                                  ((c < 0) ? "end of stream" :
                                                  "0x" + (c & 0xff).ToString("x2")));
                }
            }
            stream.ReadByte();
            ms.Position = 0;
            refer.Set(ms);
            return ms;
        }

        private MemoryStream ReadBytesAsStream() {
            int len = ReadInt(HproseTags.TagQuote);
            int off = 0;
            byte[] b = new byte[len];
            while (len > 0) {
                int size = stream.Read(b, off, len);
                off += size;
                len -= size;
            }
            MemoryStream ms = new MemoryStream(b, 0, len, true);
            stream.ReadByte();
            refer.Set(ms);
            return ms;
        }

        private IDictionary ReadObjectAsMap(IDictionary map) {
            object c = classref[ReadInt(HproseTags.TagOpenbrace)];
#if !(dotNET10 || dotNET11 || dotNETCF10)
            string[] memberNames = membersref[c];
#else
            string[] memberNames = (string[])membersref[c];
#endif
            refer.Set(map);
            int count = memberNames.Length;
            for (int i = 0; i < count; ++i) {
                map[memberNames[i]] = Unserialize();
            }
            stream.ReadByte();
            return map;
        }

        private void ReadMapAsObjectFields(object obj, Type type, int count) {
#if !(dotNET10 || dotNET11 || dotNETCF10)
            Dictionary<string, FieldTypeInfo> fields = HproseHelper.GetFields(type);
#else
            Hashtable fields = HproseHelper.GetFields(type);
#endif
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            string[] names = new string[count];
            object[] values = new object[count];
#endif
            FieldTypeInfo field;
            for (int i = 0; i < count; ++i) {
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                names[i] = ReadString();
                if (fields.TryGetValue(names[i], out field)) {
#elif !(dotNET10 || dotNET11 || dotNETCF10)
                if (fields.TryGetValue(ReadString(), out field)) {
#else
                string name = ReadString();
                if (fields.ContainsKey(name)) {
                    field = (FieldTypeInfo)fields[name];
#endif
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                    values[i] = Unserialize(field.type, field.typeEnum);
#else
                    field.info.SetValue(obj, Unserialize(field.type, field.typeEnum));
#endif
                }
                else {
                    Unserialize();
                }
            }
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            ObjectFieldModeUnserializer.Get(type, names).Unserialize(obj, values);
#endif
        }

        private void ReadMapAsObjectProperties(object obj, Type type, int count) {
#if !(dotNET10 || dotNET11 || dotNETCF10)
            Dictionary<string, PropertyTypeInfo> properties = HproseHelper.GetProperties(type);
#else
            Hashtable properties = HproseHelper.GetProperties(type);
#endif
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            string[] names = new string[count];
            object[] values = new object[count];
#endif
            PropertyTypeInfo property;
            for (int i = 0; i < count; ++i) {
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                names[i] = ReadString();
                if (properties.TryGetValue(names[i], out property)) {
#elif !(dotNET10 || dotNET11 || dotNETCF10)
                if (properties.TryGetValue(ReadString(), out property)) {
#else
                string name = ReadString();
                if (properties.ContainsKey(name)) {
                    property = (PropertyTypeInfo)properties[name];
#endif
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                    values[i] = Unserialize(property.type, property.typeEnum);
#elif (dotNET10 || dotNET11)
                    PropertyAccessor.Get(property.info).SetValue(obj, Unserialize(property.type, property.typeEnum));
#elif Core
                    property.info.SetValue(obj, Unserialize(property.type, property.typeEnum));
#else
                    property.info.GetSetMethod(true).Invoke(obj, new object[] { Unserialize(property.type, property.typeEnum)});
#endif
                }
                else {
                    Unserialize();
                }
            }
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            ObjectPropertyModeUnserializer.Get(type, names).Unserialize(obj, values);
#endif
        }

        private void ReadMapAsObjectMembers(object obj, Type type, int count) {
#if !(dotNET10 || dotNET11 || dotNETCF10)
            Dictionary<string, MemberTypeInfo> members = HproseHelper.GetMembers(type);
#else
            Hashtable members = HproseHelper.GetMembers(type);
#endif
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            string[] names = new string[count];
            object[] values = new object[count];
#endif
            MemberTypeInfo member;
            for (int i = 0; i < count; ++i) {
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                names[i] = ReadString();
                if (members.TryGetValue(names[i], out member)) {
#elif !(dotNET10 || dotNET11 || dotNETCF10)
                if (members.TryGetValue(ReadString(), out member)) {
#else
                string name = ReadString();
                if (members.ContainsKey(name)) {
                    member = (MemberTypeInfo)members[name];
#endif
#if (PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                    if (member.info is FieldInfo) {
                        FieldInfo field = (FieldInfo)member.info;
                        field.SetValue(obj, Unserialize(member.type, member.typeEnum));
                    }
                    else {
                        PropertyInfo property = (PropertyInfo)member.info;
#if (dotNET10 || dotNET11)
                        PropertyAccessor.Get(property).SetValue(obj, Unserialize(member.type, member.typeEnum));
#elif Core
                        property.SetValue(obj, Unserialize(member.type, member.typeEnum));
#else
                        property.GetSetMethod(true).Invoke(obj, new object[] { Unserialize(member.type, member.typeEnum)});
#endif
                    }
#else
                    values[i] = Unserialize(member.type, member.typeEnum);
#endif
                }
                else {
                    Unserialize();
                }
            }
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            ObjectMemberModeUnserializer.Get(type, names).Unserialize(obj, values);
#endif
        }

        private object ReadMapAsObject(Type type) {
            int count = ReadInt(HproseTags.TagOpenbrace);
            object obj = HproseHelper.NewInstance(type);
            if (obj == null) throw new HproseException("Can not make an instance of type: " + type.FullName);
            refer.Set(obj);
            if ((mode != HproseMode.MemberMode) && HproseHelper.IsSerializable(type)) {
                if (mode == HproseMode.FieldMode) {
                    ReadMapAsObjectFields(obj, type, count);
                }
                else {
                    ReadMapAsObjectProperties(obj, type, count);
                }
            }
            else {
                ReadMapAsObjectMembers(obj, type, count);
            }
            stream.ReadByte();
            return obj;
        }

        private void ReadClass() {
            string className = ReadCharsAsString();
            int count = ReadInt(HproseTags.TagOpenbrace);
            string[] memberNames = new string[count];
            for (int i = 0; i < count; ++i) {
                memberNames[i] = ReadString();
            }
            stream.ReadByte();
            Type type = HproseHelper.GetClass(className);
            if (type == null) {
                object key = new object();
                classref.Add(key);
                membersref[key] = memberNames;
            }
            else {
                classref.Add(type);
                membersref[type] = memberNames;
            }
        }

        private object ReadRef() {
            return refer.Read(ReadIntWithoutTag());
        }

        private object ReadRef(Type type) {
            object obj = ReadRef();
            if (obj.GetType() == type) return obj;
#if Core
            if (type.GetTypeInfo().IsAssignableFrom(obj.GetType().GetTypeInfo())) return obj;
#else
            if (type.IsAssignableFrom(obj.GetType())) return obj;
#endif
            throw CastError(obj, type);
        }

        public int ReadIntWithoutTag() {
            return ReadInt(HproseTags.TagSemicolon);
        }

        public BigInteger ReadBigIntegerWithoutTag() {
            return BigInteger.Parse(ReadUntil(HproseTags.TagSemicolon).ToString());
        }

        public long ReadLongWithoutTag() {
            return ReadLong(HproseTags.TagSemicolon);
        }

        public double ReadDoubleWithoutTag() {
            return ParseDouble(ReadUntil(HproseTags.TagSemicolon));
        }

        public double ReadInfinityWithoutTag() {
            return ((stream.ReadByte() == HproseTags.TagNeg) ?
                double.NegativeInfinity : double.PositiveInfinity);
        }

        public DateTime ReadDateWithoutTag() {
            DateTime datetime;
            int year = stream.ReadByte() - '0';
            year = year * 10 + stream.ReadByte() - '0';
            year = year * 10 + stream.ReadByte() - '0';
            year = year * 10 + stream.ReadByte() - '0';
            int month = stream.ReadByte() - '0';
            month = month * 10 + stream.ReadByte() - '0';
            int day = stream.ReadByte() - '0';
            day = day * 10 + stream.ReadByte() - '0';
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagTime) {
                int hour = stream.ReadByte() - '0';
                hour = hour * 10 + stream.ReadByte() - '0';
                int minute = stream.ReadByte() - '0';
                minute = minute * 10 + stream.ReadByte() - '0';
                int second = stream.ReadByte() - '0';
                second = second * 10 + stream.ReadByte() - '0';
                int millisecond = 0;
                tag = stream.ReadByte();
                if (tag == HproseTags.TagPoint) {
                    millisecond = stream.ReadByte() - '0';
                    millisecond = millisecond * 10 + stream.ReadByte() - '0';
                    millisecond = millisecond * 10 + stream.ReadByte() - '0';
                    tag = stream.ReadByte();
                    if ((tag >= '0') && (tag <= '9')) {
                        stream.ReadByte();
                        stream.ReadByte();
                        tag = stream.ReadByte();
                        if ((tag >= '0') && (tag <= '9')) {
                            stream.ReadByte();
                            stream.ReadByte();
                            tag = stream.ReadByte();
                        }
                    }
                }
#if !(dotNET10 || dotNET11 || dotNETCF10)
                DateTimeKind kind = (tag == HproseTags.TagUTC ? DateTimeKind.Utc : DateTimeKind.Local);
                datetime = new DateTime(year, month, day, hour, minute, second, millisecond, kind);
#else
                datetime = new DateTime(year, month, day, hour, minute, second, millisecond);
#endif
            }
            else {
#if !(dotNET10 || dotNET11 || dotNETCF10)
                DateTimeKind kind = (tag == HproseTags.TagUTC ? DateTimeKind.Utc : DateTimeKind.Local);
                datetime = new DateTime(year, month, day, 0, 0, 0, kind);
#else
                datetime = new DateTime(year, month, day);
#endif
            }
            refer.Set(datetime);
            return datetime;
        }

        public DateTime ReadTimeWithoutTag() {
            int hour = stream.ReadByte() - '0';
            hour = hour * 10 + stream.ReadByte() - '0';
            int minute = stream.ReadByte() - '0';
            minute = minute * 10 + stream.ReadByte() - '0';
            int second = stream.ReadByte() - '0';
            second = second * 10 + stream.ReadByte() - '0';
            int millisecond = 0;
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagPoint) {
                millisecond = stream.ReadByte() - '0';
                millisecond = millisecond * 10 + stream.ReadByte() - '0';
                millisecond = millisecond * 10 + stream.ReadByte() - '0';
                tag = stream.ReadByte();
                if ((tag >= '0') && (tag <= '9')) {
                    stream.ReadByte();
                    stream.ReadByte();
                    tag = stream.ReadByte();
                    if ((tag >= '0') && (tag <= '9')) {
                        stream.ReadByte();
                        stream.ReadByte();
                        tag = stream.ReadByte();
                    }
                }
            }
#if !(dotNET10 || dotNET11 || dotNETCF10)
            DateTimeKind kind = (tag == HproseTags.TagUTC ? DateTimeKind.Utc : DateTimeKind.Local);
            DateTime datetime = new DateTime(1970, 1, 1, hour, minute, second, millisecond, kind);
#else
            DateTime datetime = new DateTime(1970, 1, 1, hour, minute, second, millisecond);
#endif
            refer.Set(datetime);
            return datetime;
        }

        public byte[] ReadBytesWithoutTag() {
            int len = ReadInt(HproseTags.TagQuote);
            int off = 0;
            byte[] b = new byte[len];
            while (len > 0) {
                int size = stream.Read(b, off, len);
                off += size;
                len -= size;
            }
            stream.ReadByte();
            refer.Set(b);
            return b;
        }

        public string ReadUTF8CharWithoutTag() {
            return new string(ReadUTF8CharAsChar(), 1);
        }

        public String ReadStringWithoutTag() {
            String str = ReadCharsAsString();
            refer.Set(str);
            return str;
        }

        public char[] ReadCharsWithoutTag() {
            char[] chars = ReadChars();
            refer.Set(chars);
            return chars;
        }

        public Guid ReadGuidWithoutTag() {
            char[] buf = new char[38];
            for (int i = 0; i < 38; ++i) {
                buf[i] = (char)stream.ReadByte();
            }
            Guid guid = new Guid(new String(buf));
            refer.Set(guid);
            return guid;
        }

        public IList ReadListWithoutTag() {
            int count = ReadInt(HproseTags.TagOpenbrace);
#if (dotNET10 || dotNET11 || dotNETCF10)
            ArrayList a = new ArrayList(count);
#else
            List<object> a = new List<object>(count);
#endif
            refer.Set(a);
            for (int i = 0; i < count; ++i) {
                a.Add(Unserialize());
            }
            stream.ReadByte();
            return a;
        }

        public IDictionary ReadMapWithoutTag() {
            int count = ReadInt(HproseTags.TagOpenbrace);
#if (dotNET10 || dotNET11 || dotNETCF10)
            HashMap map = new HashMap(count);
#else
            HashMap<object, object> map = new HashMap<object, object>(count);
#endif
            refer.Set(map);
            for (int i = 0; i < count; ++i) {
                object key = Unserialize();
                object value = Unserialize();
                map[key] = value;
            }
            stream.ReadByte();
            return map;
        }

        private void ReadObjectFields(object obj, Type type, int count, string[] memberNames) {
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            object[] values = new object[count];
#endif
#if !(dotNET10 || dotNET11 || dotNETCF10)
            Dictionary<string, FieldTypeInfo> fields = HproseHelper.GetFields(type);
#else
            Hashtable fields = HproseHelper.GetFields(type);
#endif
            FieldTypeInfo field;
            for (int i = 0; i < count; ++i) {
#if !(dotNET10 || dotNET11 || dotNETCF10)
                if (fields.TryGetValue(memberNames[i], out field)) {
#else
                if (fields.ContainsKey(memberNames[i])) {
                    field = (FieldTypeInfo)fields[memberNames[i]];
#endif
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                    values[i] = Unserialize(field.type, field.typeEnum);
#else
                    field.info.SetValue(obj, Unserialize(field.type, field.typeEnum));
#endif
                }
                else {
                    Unserialize();
                }
            }
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            ObjectFieldModeUnserializer.Get(type, memberNames).Unserialize(obj, values);
#endif
        }

        private void ReadObjectProperties(object obj, Type type, int count, string[] memberNames) {
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            object[] values = new object[count];
#endif
#if !(dotNET10 || dotNET11 || dotNETCF10)
            Dictionary<string, PropertyTypeInfo> properties = HproseHelper.GetProperties(type);
#else
            Hashtable properties = HproseHelper.GetProperties(type);
#endif
            PropertyTypeInfo property;
            for (int i = 0; i < count; ++i) {
#if !(dotNET10 || dotNET11 || dotNETCF10)
                if (properties.TryGetValue(memberNames[i], out property)) {
#else
                if (properties.ContainsKey(memberNames[i])) {
                    property = (PropertyTypeInfo)properties[memberNames[i]];
#endif
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                    values[i] = Unserialize(property.type, property.typeEnum);
#elif (dotNET10 || dotNET11)
                    PropertyAccessor.Get(property.info).SetValue(obj, Unserialize(property.type, property.typeEnum));
#elif Core
                    property.info.SetValue(obj, Unserialize(property.type, property.typeEnum));
#else
                    property.info.GetSetMethod(true).Invoke(obj, new object[] { Unserialize(property.type, property.typeEnum)});
#endif
                }
                else {
                    Unserialize();
                }
            }
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            ObjectPropertyModeUnserializer.Get(type, memberNames).Unserialize(obj, values);
#endif
        }

        private void ReadObjectMembers(object obj, Type type, int count, string[] memberNames) {
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            object[] values = new object[count];
#endif
#if !(dotNET10 || dotNET11 || dotNETCF10)
            Dictionary<string, MemberTypeInfo> members = HproseHelper.GetMembers(type);
#else
            Hashtable members = HproseHelper.GetMembers(type);
#endif
            MemberTypeInfo member;
            for (int i = 0; i < count; ++i) {
#if !(dotNET10 || dotNET11 || dotNETCF10)
                if (members.TryGetValue(memberNames[i], out member)) {
#else
                if (members.ContainsKey(memberNames[i])) {
                    member = (MemberTypeInfo)members[memberNames[i]];
#endif
#if (PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
                    if (member.info is FieldInfo) {
                        FieldInfo field = (FieldInfo)member.info;
                        field.SetValue(obj, Unserialize(member.type, member.typeEnum));
                    }
                    else {
                        PropertyInfo property = (PropertyInfo)member.info;
#if (dotNET10 || dotNET11)
                        PropertyAccessor.Get(property).SetValue(obj, Unserialize(member.type, member.typeEnum));
#elif Core
                        property.SetValue(obj, Unserialize(member.type, member.typeEnum));
#else
                        property.GetSetMethod(true).Invoke(obj, new object[] { Unserialize(member.type, member.typeEnum)});
#endif
                    }
#else
                    values[i] = Unserialize(member.type, member.typeEnum);
#endif
                }
                else {
                    Unserialize();
                }
            }
#if !(PocketPC || Smartphone || WindowsCE || dotNET10 || dotNET11 || SILVERLIGHT || WINDOWS_PHONE || Core || Unity_iOS)
            ObjectMemberModeUnserializer.Get(type, memberNames).Unserialize(obj, values);
#endif
        }

        public object ReadObjectWithoutTag(Type type) {
            object c = classref[ReadInt(HproseTags.TagOpenbrace)];
#if !(dotNET10 || dotNET11 || dotNETCF10)
            string[] memberNames = membersref[c];
#else
            string[] memberNames = (string[])membersref[c];
#endif
            int count = memberNames.Length;
            object obj = null;
            if (c is Type) {
                Type cls = (Type)c;
                if ((type == null) ||
#if Core
                    type.GetTypeInfo().IsAssignableFrom(cls.GetTypeInfo())
#else
                    type.IsAssignableFrom(cls)
#endif
                ) type = cls;
            }
            if (type == null) {
#if (dotNET10 || dotNET11 || dotNETCF10)
                Hashtable map = new Hashtable(count);
#else
                Dictionary<string, object> map = new Dictionary<string, object>(count);
#endif
                refer.Set(map);
                for (int i = 0; i < count; ++i) {
                    map[memberNames[i]] = Unserialize();
                }
                obj = map;
            }
            else {
                obj = HproseHelper.NewInstance(type);
                if (obj == null) throw new HproseException("Can not make an instance of type: " + type.FullName);
                refer.Set(obj);
                if ((mode != HproseMode.MemberMode) && HproseHelper.IsSerializable(type)) {
                    if (mode == HproseMode.FieldMode) {
                        ReadObjectFields(obj, type, count, memberNames);
                    }
                    else {
                        ReadObjectProperties(obj, type, count, memberNames);
                    }
                }
                else {
                    ReadObjectMembers(obj, type, count, memberNames);
                }
            }
            stream.ReadByte();
            return obj;
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public T Unserialize<T>() {
            return (T)Unserialize(typeof(T), HproseHelper.GetTypeEnum(typeof(T)));
        }
#endif

        private object Unserialize(int tag) {
            switch (tag) {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case HproseTags.TagInteger: return ReadIntWithoutTag();
                case HproseTags.TagLong: return ReadBigIntegerWithoutTag();
                case HproseTags.TagDouble: return ReadDoubleWithoutTag();
                case HproseTags.TagNull: return null;
                case HproseTags.TagEmpty: return "";
                case HproseTags.TagTrue: return true;
                case HproseTags.TagFalse: return false;
                case HproseTags.TagNaN: return double.NaN;
                case HproseTags.TagInfinity: return ReadInfinityWithoutTag();
                case HproseTags.TagDate: return ReadDateWithoutTag();
                case HproseTags.TagTime: return ReadTimeWithoutTag();
                case HproseTags.TagBytes: return ReadBytesWithoutTag();
                case HproseTags.TagUTF8Char: return ReadUTF8CharWithoutTag();
                case HproseTags.TagString: return ReadStringWithoutTag();
                case HproseTags.TagGuid: return ReadGuidWithoutTag();
                case HproseTags.TagList: return ReadListWithoutTag();
                case HproseTags.TagMap: return ReadMapWithoutTag();
                case HproseTags.TagClass: ReadClass(); return ReadObject(null);
                case HproseTags.TagObject: return ReadObjectWithoutTag(null);
                case HproseTags.TagRef: return ReadRef();
                case HproseTags.TagError: throw new HproseException(ReadString());
                default: throw UnexpectedTag(tag);
            }
        }

        public object Unserialize() {
            return Unserialize(stream.ReadByte());
        }

        public object ReadObject() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagUTF8Char) return ReadUTF8CharAsChar();
            return Unserialize(tag);
        }

        private string TagToString(int tag) {
            switch (tag) {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case HproseTags.TagInteger: return "Integer";
                case HproseTags.TagLong: return "BigInteger";
                case HproseTags.TagDouble: return "Double";
                case HproseTags.TagNull: return "Null";
                case HproseTags.TagEmpty: return "Empty String";
                case HproseTags.TagTrue: return "Boolean True";
                case HproseTags.TagFalse: return "Boolean False";
                case HproseTags.TagNaN: return "NaN";
                case HproseTags.TagInfinity: return "Infinity";
                case HproseTags.TagDate: return "DateTime";
                case HproseTags.TagTime: return "DateTime";
                case HproseTags.TagBytes: return "Byte[]";
                case HproseTags.TagUTF8Char: return "Char";
                case HproseTags.TagString: return "String";
                case HproseTags.TagGuid: return "Guid";
                case HproseTags.TagList: return "IList";
                case HproseTags.TagMap: return "IDictionary";
                case HproseTags.TagClass: return "Class";
                case HproseTags.TagObject: return "Object";
                case HproseTags.TagRef: return "Object Reference";
                case HproseTags.TagError: throw new HproseException(ReadString());
                default: throw UnexpectedTag(tag);
            }
        }
#if !Core
        public DBNull ReadDBNull() {
            int tag = stream.ReadByte();
            switch (tag) {
                case '0':
                case HproseTags.TagNull:
                case HproseTags.TagEmpty:
                case HproseTags.TagFalse: return DBNull.Value;
                default: throw CastError(TagToString(tag), HproseHelper.typeofDBNull);
            }
        }
#endif

        private bool ReadBooleanWithTag(int tag) {
            switch (tag) {
                case '0': return false;
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9': return true;
                case HproseTags.TagInteger: return ReadIntWithoutTag() != 0;
                case HproseTags.TagLong: return !(ReadBigIntegerWithoutTag().IsZero);
                case HproseTags.TagDouble: return ReadDoubleWithoutTag() != 0.0;
                case HproseTags.TagEmpty: return false;
                case HproseTags.TagTrue: return true;
                case HproseTags.TagFalse: return false;
                case HproseTags.TagNaN: return true;
                case HproseTags.TagInfinity: stream.ReadByte(); return true;
                case HproseTags.TagUTF8Char: return "\00".IndexOf(ReadUTF8CharAsChar()) == -1;
                case HproseTags.TagString: return bool.Parse(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToBoolean(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofBoolean);
            }
        }

        public bool ReadBoolean() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? false : ReadBooleanWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public bool? ReadNullableBoolean() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadBooleanWithTag(tag);
            }
        }
#endif

        private char ReadCharWithTag(int tag) {
            switch (tag) {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9': return (char)tag;
                case HproseTags.TagInteger: return Convert.ToChar(ReadIntWithoutTag());
                case HproseTags.TagLong: return Convert.ToChar(ReadLongWithoutTag());
                case HproseTags.TagUTF8Char: return ReadUTF8CharAsChar();
                case HproseTags.TagString: return Convert.ToChar(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToChar(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofChar);
            }
        }

        public char ReadChar() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? (char)0 : ReadCharWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public char? ReadNullableChar() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadCharWithTag(tag);
            }
        }
#endif

        private sbyte ReadSByteWithTag(int tag) {
            switch (tag) {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case HproseTags.TagInteger: return Convert.ToSByte(ReadIntWithoutTag());
                case HproseTags.TagLong: return Convert.ToSByte(ReadLongWithoutTag());
                case HproseTags.TagDouble: return Convert.ToSByte(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return 0;
                case HproseTags.TagTrue: return 1;
                case HproseTags.TagFalse: return 0;
                case HproseTags.TagUTF8Char: return Convert.ToSByte(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToSByte(ReadStringWithoutTag());
#if dotNETCF10
                case HproseTags.TagRef: return Convert.ToSByte(Convert.ToInt32(ReadRef()));
#else
                case HproseTags.TagRef: return Convert.ToSByte(ReadRef());
#endif
                default: throw CastError(TagToString(tag), HproseHelper.typeofSByte);
            }
        }

        [CLSCompliantAttribute(false)]
        public sbyte ReadSByte() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? (sbyte)0 : ReadSByteWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        [CLSCompliantAttribute(false)]
        public sbyte? ReadNullableSByte() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadSByteWithTag(tag);
            }
        }
#endif

        private byte ReadByteWithTag(int tag) {
            switch (tag) {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case HproseTags.TagInteger: return Convert.ToByte(ReadIntWithoutTag());
                case HproseTags.TagLong: return Convert.ToByte(ReadLongWithoutTag());
                case HproseTags.TagDouble: return Convert.ToByte(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return 0;
                case HproseTags.TagTrue: return 1;
                case HproseTags.TagFalse: return 0;
                case HproseTags.TagUTF8Char: return Convert.ToByte(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToByte(ReadStringWithoutTag());
#if dotNETCF10
                case HproseTags.TagRef: return Convert.ToByte(Convert.ToInt32(ReadRef()));
#else
                case HproseTags.TagRef: return Convert.ToByte(ReadRef());
#endif
                default: throw CastError(TagToString(tag), HproseHelper.typeofByte);
            }
        }

        public byte ReadByte() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? (byte)0 : ReadByteWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public byte? ReadNullableByte() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadByteWithTag(tag);
            }
        }
#endif

        private short ReadInt16WithTag(int tag) {
            switch (tag) {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case HproseTags.TagInteger: return Convert.ToInt16(ReadIntWithoutTag());
                case HproseTags.TagLong: return Convert.ToInt16(ReadLongWithoutTag());
                case HproseTags.TagDouble: return Convert.ToInt16(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return 0;
                case HproseTags.TagTrue: return 1;
                case HproseTags.TagFalse: return 0;
                case HproseTags.TagUTF8Char: return Convert.ToInt16(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToInt16(ReadStringWithoutTag());
#if dotNETCF10
                case HproseTags.TagRef: return Convert.ToInt16(Convert.ToInt32(ReadRef()));
#else
                case HproseTags.TagRef: return Convert.ToInt16(ReadRef());
#endif
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt16);
            }
        }

        public short ReadInt16() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? (short)0 : ReadInt16WithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public short? ReadNullableInt16() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadInt16WithTag(tag);
            }
        }
#endif

        private ushort ReadUInt16WithTag(int tag) {
            switch (tag) {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case HproseTags.TagInteger: return Convert.ToUInt16(ReadIntWithoutTag());
                case HproseTags.TagLong: return Convert.ToUInt16(ReadLongWithoutTag());
                case HproseTags.TagDouble: return Convert.ToUInt16(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return 0;
                case HproseTags.TagTrue: return 1;
                case HproseTags.TagFalse: return 0;
                case HproseTags.TagUTF8Char: return Convert.ToUInt16(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToUInt16(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToUInt16(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt16);
            }
        }

        [CLSCompliantAttribute(false)]
        public ushort ReadUInt16() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? (ushort)0 : ReadUInt16WithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        [CLSCompliantAttribute(false)]
        public ushort? ReadNullableUInt16() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadUInt16WithTag(tag);
            }
        }
#endif

        private int ReadInt32WithTag(int tag) {
            switch (tag) {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case HproseTags.TagInteger: return ReadIntWithoutTag();
                case HproseTags.TagLong: return Convert.ToInt32(ReadLongWithoutTag());
                case HproseTags.TagDouble: return Convert.ToInt32(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return 0;
                case HproseTags.TagTrue: return 1;
                case HproseTags.TagFalse: return 0;
                case HproseTags.TagUTF8Char: return Convert.ToInt32(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToInt32(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToInt32(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt32);
            }
        }

        public int ReadInt32() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? 0 : ReadInt32WithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public int? ReadNullableInt32() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadInt32WithTag(tag);
            }
        }
#endif

        private uint ReadUInt32WithTag(int tag) {
            switch (tag) {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case HproseTags.TagInteger: return Convert.ToUInt32(ReadIntWithoutTag());
                case HproseTags.TagLong: return Convert.ToUInt32(ReadLongWithoutTag());
                case HproseTags.TagDouble: return Convert.ToUInt32(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return 0;
                case HproseTags.TagTrue: return 1;
                case HproseTags.TagFalse: return 0;
                case HproseTags.TagUTF8Char: return Convert.ToUInt32(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToUInt32(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToUInt32(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt32);
            }
        }

        [CLSCompliantAttribute(false)]
        public uint ReadUInt32() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? 0 : ReadUInt32WithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        [CLSCompliantAttribute(false)]
        public uint? ReadNullableUInt32() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadUInt32WithTag(tag);
            }
        }
#endif

        private long ReadInt64WithTag(int tag) {
            switch (tag) {
                case '0': return 0L;
                case '1': return 1L;
                case '2': return 2L;
                case '3': return 3L;
                case '4': return 4L;
                case '5': return 5L;
                case '6': return 6L;
                case '7': return 7L;
                case '8': return 8L;
                case '9': return 9L;
                case HproseTags.TagInteger: return ReadLongWithoutTag();
                case HproseTags.TagLong: return ReadLongWithoutTag();
                case HproseTags.TagDouble: return Convert.ToInt64(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return 0L;
                case HproseTags.TagTrue: return 1L;
                case HproseTags.TagFalse: return 0L;
                case HproseTags.TagDate: return ReadDateWithoutTag().Ticks;
                case HproseTags.TagTime: return ReadTimeWithoutTag().Ticks;
                case HproseTags.TagUTF8Char: return Convert.ToInt64(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToInt64(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToInt64(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt64);
            }
        }

        public long ReadInt64() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? 0L : ReadInt64WithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public long? ReadNullableInt64() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadInt64WithTag(tag);
            }
        }
#endif

        private ulong ReadUInt64WithTag(int tag) {
            switch (tag) {
                case '0': return 0L;
                case '1': return 1L;
                case '2': return 2L;
                case '3': return 3L;
                case '4': return 4L;
                case '5': return 5L;
                case '6': return 6L;
                case '7': return 7L;
                case '8': return 8L;
                case '9': return 9L;
                case HproseTags.TagInteger: return Convert.ToUInt64(ReadLongWithoutTag());
                case HproseTags.TagLong: return Convert.ToUInt64(ReadLongWithoutTag());
                case HproseTags.TagDouble: return Convert.ToUInt64(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return 0L;
                case HproseTags.TagTrue: return 1L;
                case HproseTags.TagFalse: return 0L;
                case HproseTags.TagUTF8Char: return Convert.ToUInt64(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToUInt64(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToUInt64(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt64);
            }
        }

        [CLSCompliantAttribute(false)]
        public ulong ReadUInt64() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? 0L : ReadUInt64WithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        [CLSCompliantAttribute(false)]
        public ulong? ReadNullableUInt64() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadUInt64WithTag(tag);
            }
        }
#endif

        private float ReadSingleWithTag(int tag) {
            switch (tag) {
                case '0': return 0.0F;
                case '1': return 1.0F;
                case '2': return 2.0F;
                case '3': return 3.0F;
                case '4': return 4.0F;
                case '5': return 5.0F;
                case '6': return 6.0F;
                case '7': return 7.0F;
                case '8': return 8.0F;
                case '9': return 9.0F;
                case HproseTags.TagInteger: return ReadIntAsFloat();
                case HproseTags.TagLong: return ReadIntAsFloat();
                case HproseTags.TagDouble: return ParseFloat(ReadUntil(HproseTags.TagSemicolon));
                case HproseTags.TagEmpty: return 0.0F;
                case HproseTags.TagTrue: return 1.0F;
                case HproseTags.TagFalse: return 0.0F;
                case HproseTags.TagNaN: return float.NaN;
                case HproseTags.TagInfinity: return (stream.ReadByte() == HproseTags.TagPos) ?
                                                     float.PositiveInfinity :
                                                     float.NegativeInfinity;
                case HproseTags.TagUTF8Char: return Convert.ToSingle(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToSingle(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToSingle(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofSingle);
            }
        }

        public float ReadSingle() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? 0.0F : ReadSingleWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public float? ReadNullableSingle() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadSingleWithTag(tag);
            }
        }
#endif

        private double ReadDoubleWithTag(int tag) {
            switch (tag) {
                case '0': return 0.0;
                case '1': return 1.0;
                case '2': return 2.0;
                case '3': return 3.0;
                case '4': return 4.0;
                case '5': return 5.0;
                case '6': return 6.0;
                case '7': return 7.0;
                case '8': return 8.0;
                case '9': return 9.0;
                case HproseTags.TagInteger: return ReadIntAsDouble();
                case HproseTags.TagLong: return ReadIntAsDouble();
                case HproseTags.TagDouble: return ReadDoubleWithoutTag();
                case HproseTags.TagEmpty: return 0.0;
                case HproseTags.TagTrue: return 1.0;
                case HproseTags.TagFalse: return 0.0;
                case HproseTags.TagNaN: return double.NaN;
                case HproseTags.TagInfinity: return ReadInfinityWithoutTag();
                case HproseTags.TagUTF8Char: return Convert.ToDouble(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToDouble(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToDouble(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofDouble);
            }
        }

        public double ReadDouble() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? 0.0 : ReadDoubleWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public double? ReadNullableDouble() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadDoubleWithTag(tag);
            }
        }
#endif

        private decimal ReadDecimalWithTag(int tag) {
            switch (tag) {
                case '0': return 0.0M;
                case '1': return 1.0M;
                case '2': return 2.0M;
                case '3': return 3.0M;
                case '4': return 4.0M;
                case '5': return 5.0M;
                case '6': return 6.0M;
                case '7': return 7.0M;
                case '8': return 8.0M;
                case '9': return 9.0M;
                case HproseTags.TagInteger: return ReadIntAsDecimal();
                case HproseTags.TagLong: return ReadIntAsDecimal();
                case HproseTags.TagDouble: return decimal.Parse(ReadUntil(HproseTags.TagSemicolon).ToString());
                case HproseTags.TagEmpty: return 0.0M;
                case HproseTags.TagTrue: return 1.0M;
                case HproseTags.TagFalse: return 0.0M;
                case HproseTags.TagUTF8Char: return Convert.ToDecimal(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return Convert.ToDecimal(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToDecimal(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofDecimal);
            }
        }

        public decimal ReadDecimal() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? 0.0M : ReadDecimalWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public decimal? ReadNullableDecimal() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadDecimalWithTag(tag);
            }
        }
#endif

        private DateTime ReadDateTimeWithTag(int tag) {
            switch (tag) {
                case '0': return new DateTime(0L);
                case '1': return new DateTime(1L);
                case '2': return new DateTime(2L);
                case '3': return new DateTime(3L);
                case '4': return new DateTime(4L);
                case '5': return new DateTime(5L);
                case '6': return new DateTime(6L);
                case '7': return new DateTime(7L);
                case '8': return new DateTime(8L);
                case '9': return new DateTime(9L);
                case HproseTags.TagInteger: return new DateTime(ReadLongWithoutTag());
                case HproseTags.TagLong: return new DateTime(ReadLongWithoutTag());
                case HproseTags.TagDouble: return new DateTime((long)ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return new DateTime(0L);
                case HproseTags.TagDate: return ReadDateWithoutTag();
                case HproseTags.TagTime: return ReadTimeWithoutTag();
                case HproseTags.TagString: return Convert.ToDateTime(ReadStringWithoutTag());
                case HproseTags.TagRef: return Convert.ToDateTime(ReadRef());
                default: throw CastError(TagToString(tag), HproseHelper.typeofDateTime);
            }
        }

        public DateTime ReadDateTime() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? DateTime.MinValue : ReadDateTimeWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public DateTime? ReadNullableDateTime() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadDateTimeWithTag(tag);
            }
        }
#endif

        public string ReadString() {
            int tag = stream.ReadByte();
            switch (tag) {
                case '0': return "0";
                case '1': return "1";
                case '2': return "2";
                case '3': return "3";
                case '4': return "4";
                case '5': return "5";
                case '6': return "6";
                case '7': return "7";
                case '8': return "8";
                case '9': return "9";
                case HproseTags.TagInteger: return ReadUntil(HproseTags.TagSemicolon).ToString();
                case HproseTags.TagLong: return ReadUntil(HproseTags.TagSemicolon).ToString();
                case HproseTags.TagDouble: return ReadUntil(HproseTags.TagSemicolon).ToString();
                case HproseTags.TagNull: return null;
                case HproseTags.TagEmpty: return "";
                case HproseTags.TagTrue: return bool.TrueString;
                case HproseTags.TagFalse: return bool.FalseString;
                case HproseTags.TagNaN: return double.NaN.ToString();
                case HproseTags.TagInfinity: return ReadInfinityWithoutTag().ToString();
                case HproseTags.TagDate: return ReadDateWithoutTag().ToString();
                case HproseTags.TagTime: return ReadTimeWithoutTag().ToString();
                case HproseTags.TagUTF8Char: return ReadUTF8CharWithoutTag();
                case HproseTags.TagString: return ReadStringWithoutTag();
                case HproseTags.TagGuid: return ReadGuidWithoutTag().ToString();
                case HproseTags.TagList: return ReadListWithoutTag().ToString();
                case HproseTags.TagMap: return ReadMapWithoutTag().ToString();
                case HproseTags.TagClass: ReadClass(); return ReadObject(null).ToString();
                case HproseTags.TagObject: return ReadObjectWithoutTag(null).ToString();
                case HproseTags.TagRef: {
                    object obj = ReadRef();
                    if (obj is char[]) return new String((char[])obj);
                    return Convert.ToString(obj);
                }
                default: throw CastError(TagToString(tag), HproseHelper.typeofString);
            }
        }

        public StringBuilder ReadStringBuilder() {
            int tag = stream.ReadByte();
            switch (tag) {
                case '0': return new StringBuilder("0");
                case '1': return new StringBuilder("1");
                case '2': return new StringBuilder("2");
                case '3': return new StringBuilder("3");
                case '4': return new StringBuilder("4");
                case '5': return new StringBuilder("5");
                case '6': return new StringBuilder("6");
                case '7': return new StringBuilder("7");
                case '8': return new StringBuilder("8");
                case '9': return new StringBuilder("9");
                case HproseTags.TagInteger: return ReadUntil(HproseTags.TagSemicolon);
                case HproseTags.TagLong: return ReadUntil(HproseTags.TagSemicolon);
                case HproseTags.TagDouble: return ReadUntil(HproseTags.TagSemicolon);
                case HproseTags.TagNull: return null;
                case HproseTags.TagEmpty: return new StringBuilder();
                case HproseTags.TagTrue: return new StringBuilder(bool.TrueString);
                case HproseTags.TagFalse: return new StringBuilder(bool.FalseString);
                case HproseTags.TagNaN: return new StringBuilder(double.NaN.ToString());
                case HproseTags.TagInfinity: return new StringBuilder(ReadInfinityWithoutTag().ToString());
                case HproseTags.TagDate: return new StringBuilder(ReadDateWithoutTag().ToString());
                case HproseTags.TagTime: return new StringBuilder(ReadTimeWithoutTag().ToString());
                case HproseTags.TagUTF8Char: return new StringBuilder(1).Append(ReadUTF8CharAsChar());
                case HproseTags.TagString: return new StringBuilder(ReadStringWithoutTag());
                case HproseTags.TagGuid: return new StringBuilder(ReadGuidWithoutTag().ToString());
                case HproseTags.TagRef: {
                    object obj = ReadRef();
                    if (obj is char[]) return new StringBuilder(new String((char[])obj));
                    return new StringBuilder(Convert.ToString(obj));
                }
                default: throw CastError(TagToString(tag), HproseHelper.typeofStringBuilder);
            }
        }

        private Guid ReadGuidWithTag(int tag)  {
            switch (tag) {
                case HproseTags.TagBytes: return new Guid(ReadBytesWithoutTag());
                case HproseTags.TagGuid: return ReadGuidWithoutTag();
                case HproseTags.TagString: return new Guid(ReadStringWithoutTag());
                case HproseTags.TagRef: {
                    object obj = ReadRef();
                    if (obj is Guid) return (Guid)obj;
                    if (obj is byte[]) return new Guid((byte[])obj);
                    if (obj is string) return new Guid((string)obj);
                    if (obj is char[]) return new Guid(new string((char[])obj));
                    throw CastError(obj, HproseHelper.typeofGuid);
                }
                default: throw CastError(TagToString(tag), HproseHelper.typeofGuid);
            }
        }

        public Guid ReadGuid() {
            return ReadGuidWithTag(stream.ReadByte());
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public Guid? ReadNullableGuid() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadGuidWithTag(tag);
            }
        }
#endif

        private BigInteger ReadBigIntegerWithTag(int tag) {
            switch (tag) {
                case '0': return BigInteger.Zero;
                case '1': return BigInteger.One;
                case '2': return new BigInteger(2);
                case '3': return new BigInteger(3);
                case '4': return new BigInteger(4);
                case '5': return new BigInteger(5);
                case '6': return new BigInteger(6);
                case '7': return new BigInteger(7);
                case '8': return new BigInteger(8);
                case '9': return new BigInteger(9);
                case HproseTags.TagInteger: return new BigInteger(ReadIntWithoutTag());
                case HproseTags.TagLong: return ReadBigIntegerWithoutTag();
                case HproseTags.TagDouble: return new BigInteger(ReadDoubleWithoutTag());
                case HproseTags.TagEmpty: return BigInteger.Zero;
                case HproseTags.TagTrue: return BigInteger.One;
                case HproseTags.TagFalse: return BigInteger.Zero;
                case HproseTags.TagDate: return new BigInteger(ReadDateWithoutTag().Ticks);
                case HproseTags.TagTime: return new BigInteger(ReadTimeWithoutTag().Ticks);
                case HproseTags.TagUTF8Char: return BigInteger.Parse(ReadUTF8CharWithoutTag());
                case HproseTags.TagString: return BigInteger.Parse(ReadStringWithoutTag());
                case HproseTags.TagRef: return BigInteger.Parse(Convert.ToString(ReadRef()));
                default: throw CastError(TagToString(tag), HproseHelper.typeofBigInteger);
            }
        }

        public BigInteger ReadBigInteger() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? BigInteger.Zero : ReadBigIntegerWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public BigInteger? ReadNullableBigInteger() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadBigIntegerWithTag(tag);
            }
        }
#endif

        private TimeSpan ReadTimeSpanWithTag(int tag) {
            switch (tag) {
                case '0': return TimeSpan.Zero;
                case '1': return new TimeSpan(1L);
                case '2': return new TimeSpan(2L);
                case '3': return new TimeSpan(3L);
                case '4': return new TimeSpan(4L);
                case '5': return new TimeSpan(5L);
                case '6': return new TimeSpan(6L);
                case '7': return new TimeSpan(7L);
                case '8': return new TimeSpan(8L);
                case '9': return new TimeSpan(9L);
                case HproseTags.TagInteger: return new TimeSpan(ReadLongWithoutTag());
                case HproseTags.TagLong: return new TimeSpan(ReadLongWithoutTag());
                case HproseTags.TagDouble: return new TimeSpan(Convert.ToInt64(ReadDoubleWithoutTag()));
                case HproseTags.TagEmpty: return TimeSpan.Zero;
                case HproseTags.TagTrue: return new TimeSpan(1L);
                case HproseTags.TagFalse: return TimeSpan.Zero;
                case HproseTags.TagDate: return new TimeSpan(ReadDateWithoutTag().Ticks);
                case HproseTags.TagTime: return new TimeSpan(ReadTimeWithoutTag().Ticks);
                case HproseTags.TagString: return new TimeSpan(Convert.ToDateTime(ReadStringWithoutTag()).Ticks);
                case HproseTags.TagRef: return new TimeSpan(Convert.ToDateTime(ReadRef()).Ticks);
                default: throw CastError(TagToString(tag), HproseHelper.typeofTimeSpan);
            }
        }

        public TimeSpan ReadTimeSpan() {
            int tag = stream.ReadByte();
            return (tag == HproseTags.TagNull) ? TimeSpan.Zero : ReadTimeSpanWithTag(tag);
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public TimeSpan? ReadNullableTimeSpan() {
            int tag = stream.ReadByte();
            if (tag == HproseTags.TagNull) {
                return null;
            }
            else {
                return ReadTimeSpanWithTag(tag);
            }
        }
#endif

        public MemoryStream ReadStream() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagUTF8Char: return ReadUTF8CharAsStream();
                case HproseTags.TagString: return ReadCharsAsStream();
                case HproseTags.TagBytes: return ReadBytesAsStream();
                case HproseTags.TagRef: return (MemoryStream)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofStream);
            }
        }

        public object ReadEnum(Type type) {
            int tag = stream.ReadByte();
            switch (tag) {
                case '0': return Enum.ToObject(type, 0);
                case '1': return Enum.ToObject(type, 1);
                case '2': return Enum.ToObject(type, 2);
                case '3': return Enum.ToObject(type, 3);
                case '4': return Enum.ToObject(type, 4);
                case '5': return Enum.ToObject(type, 5);
                case '6': return Enum.ToObject(type, 6);
                case '7': return Enum.ToObject(type, 7);
                case '8': return Enum.ToObject(type, 8);
                case '9': return Enum.ToObject(type, 9);
                case HproseTags.TagInteger: return Enum.ToObject(type, ReadIntWithoutTag());
                case HproseTags.TagLong: return Enum.ToObject(type, ReadLongWithoutTag());
                case HproseTags.TagDouble: return Enum.ToObject(type, Convert.ToInt64(ReadDoubleWithoutTag()));
                case HproseTags.TagNull: return Enum.ToObject(type, 0);
                case HproseTags.TagEmpty: return Enum.ToObject(type, 0);
                case HproseTags.TagTrue: return Enum.ToObject(type, 1);
                case HproseTags.TagFalse: return Enum.ToObject(type, 0);
#if !dotNETCF10
                case HproseTags.TagUTF8Char: return Enum.Parse(type, ReadUTF8CharWithoutTag(), true);
                case HproseTags.TagString: return Enum.Parse(type, ReadStringWithoutTag(), true);
                case HproseTags.TagRef: return Enum.Parse(type, ReadRef().ToString(), true);
#endif
                default: throw CastError(TagToString(tag), type);
            }
        }

        public void ReadArray(Type[] types, object[] a, int count) {
            refer.Set(a);
            for (int i = 0; i < count; ++i) {
                a[i] = Unserialize(types[i]);
            }
            stream.ReadByte();
        }

        public object[] ReadArray(int count) {
            object[] a = new object[count];
            refer.Set(a);
            for (int i = 0; i < count; ++i) {
                a[i] = Unserialize();
            }
            stream.ReadByte();
            return a;
        }

        public object[] ReadObjectArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadArray(ReadInt(HproseTags.TagOpenbrace));
                case HproseTags.TagRef: return (object[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofObjectArray);
            }
        }

        public bool[] ReadBooleanArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    bool[] a = new bool[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadBoolean();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (bool[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofBooleanArray);
            }
        }

        public char[] ReadCharArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagUTF8Char: return new char[] { ReadUTF8CharAsChar() };
                case HproseTags.TagString: return ReadCharsWithoutTag();
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    char[] a = new char[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadChar();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: {
                    object obj = ReadRef();
                    if (obj is char[]) return (char[])obj;
                    if (obj is string) return ((string)obj).ToCharArray();
#if !(dotNET10 || dotNET11 || dotNETCF10)
                    if (obj is List<char>) return ((List<char>)obj).ToArray();
#endif
                    throw CastError(obj, HproseHelper.typeofCharArray);
                }
                default: throw CastError(TagToString(tag), HproseHelper.typeofCharArray);
            }
        }

        [CLSCompliantAttribute(false)]
        public sbyte[] ReadSByteArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    sbyte[] a = new sbyte[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadSByte();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (sbyte[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofSByteArray);
            }
        }

        public byte[] ReadByteArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagEmpty: return new byte[0];
                case HproseTags.TagUTF8Char: return ReadUTF8CharAsStream().ToArray();
                case HproseTags.TagString: return ReadCharsAsStream().ToArray();
                case HproseTags.TagGuid: return ReadGuidWithoutTag().ToByteArray();
                case HproseTags.TagBytes: return ReadBytesWithoutTag();
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    byte[] a = new byte[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadByte();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: {
                    object obj = ReadRef();
                    if (obj is byte[]) return (byte[])obj;
                    if (obj is Guid) return ((Guid)obj).ToByteArray();
                    if (obj is MemoryStream) return ((MemoryStream)obj).ToArray();
#if !(dotNET10 || dotNET11 || dotNETCF10)
                    if (obj is List<byte>) return ((List<byte>)obj).ToArray();
#endif
                    throw CastError(obj, HproseHelper.typeofByteArray);
                }
                default: throw CastError(TagToString(tag), HproseHelper.typeofByteArray);
            }
        }

        public short[] ReadInt16Array() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    short[] a = new short[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadInt16();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (short[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt16Array);
            }
        }

        [CLSCompliantAttribute(false)]
        public ushort[] ReadUInt16Array() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    ushort[] a = new ushort[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadUInt16();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (ushort[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt16Array);
            }
        }

        public int[] ReadInt32Array() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    int[] a = new int[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadInt32();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (int[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt32Array);
            }
        }

        [CLSCompliantAttribute(false)]
        public uint[] ReadUInt32Array() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    uint[] a = new uint[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadUInt32();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (uint[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt32Array);
            }
        }

        public long[] ReadInt64Array() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    long[] a = new long[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadInt64();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (long[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt64Array);
            }
        }

        [CLSCompliantAttribute(false)]
        public ulong[] ReadUInt64Array() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    ulong[] a = new ulong[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadUInt64();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (ulong[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt64Array);
            }
        }

        public float[] ReadSingleArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    float[] a = new float[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadSingle();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (float[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofSingleArray);
            }
        }

        public double[] ReadDoubleArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    double[] a = new double[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadDouble();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (double[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofDoubleArray);
            }
        }

        public decimal[] ReadDecimalArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    decimal[] a = new decimal[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadDecimal();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (decimal[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofDecimalArray);
            }
        }

        public DateTime[] ReadDateTimeArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    DateTime[] a = new DateTime[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadDateTime();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (DateTime[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofDateTimeArray);
            }
        }

        public string[] ReadStringArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    string[] a = new string[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadString();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (string[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofStringArray);
            }
        }

        public StringBuilder[] ReadStringBuilderArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    StringBuilder[] a = new StringBuilder[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadStringBuilder();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (StringBuilder[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofStringBuilderArray);
            }
        }

        public Guid[] ReadGuidArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    Guid[] a = new Guid[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadGuid();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (Guid[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofGuidArray);
            }
        }

        public BigInteger[] ReadBigIntegerArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    BigInteger[] a = new BigInteger[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadBigInteger();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (BigInteger[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofBigIntegerArray);
            }
        }

        public TimeSpan[] ReadTimeSpanArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    TimeSpan[] a = new TimeSpan[count];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadTimeSpan();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (TimeSpan[])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofTimeSpanArray);
            }
        }

        [CLSCompliantAttribute(false)]
        public char[][] ReadCharsArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    char[][] a = new char[count][];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadCharArray();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (char[][])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofCharsArray);
            }
        }

        [CLSCompliantAttribute(false)]
        public byte[][] ReadBytesArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    byte[][] a = new byte[count][];
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadByteArray();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (byte[][])ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofBytesArray);
            }
        }

        private Array ReadArray(Type type) {
            int count = ReadInt(HproseTags.TagOpenbrace);
#if !dotNETCF10
            int rank = type.GetArrayRank();
#endif
            Type elementType = type.GetElementType();
            TypeEnum elementTypeEnum = HproseHelper.GetTypeEnum(elementType);
            Array a;
#if !dotNETCF10
            if (rank == 1) {
#endif
                a = Array.CreateInstance(elementType, count);
                refer.Set(a);
                for (int i = 0; i < count; ++i) {
                    a.SetValue(Unserialize(elementType, elementTypeEnum), i);
                }
#if !dotNETCF10
            }
            else {
                int i;
                int[] loc = new int[rank];
                int[] len = new int[rank];
                int maxrank = rank - 1;
                len[0] = count;
                for (i = 1; i < rank; ++i) {
                    stream.ReadByte();
                    //CheckTag(HproseTags.TagList);
                    len[i] = ReadInt(HproseTags.TagOpenbrace);
                }
                a = Array.CreateInstance(elementType, len);
                refer.Set(a);
                for (i = 1; i < rank; ++i) {
                    refer.Set(null);
                }
                while (true) {
                    for (loc[maxrank] = 0;
                         loc[maxrank] < len[maxrank];
                         loc[maxrank]++) {
                        a.SetValue(Unserialize(elementType, elementTypeEnum), loc);
                    }
                    for (i = maxrank; i > 0; i--) {
                        if (loc[i] >= len[i]) {
                            loc[i] = 0;
                            loc[i - 1]++;
                            stream.ReadByte();
                            //CheckTag(HproseTags.TagClosebrace);
                        }
                    }
                    if (loc[0] >= len[0]) {
                        break;
                    }
                    int n = 0;
                    for (i = maxrank; i > 0; i--) {
                        if (loc[i] == 0) {
                            n++;
                        }
                        else {
                            break;
                        }
                    }
                    for (i = rank - n; i < rank; ++i) {
                        stream.ReadByte();
                        //CheckTag(HproseTags.TagList);
                        refer.Set(null);
                        SkipUntil(HproseTags.TagOpenbrace);
                    }
                }
            }
#endif
            stream.ReadByte();
            return a;
        }

        public Array ReadOtherTypeArray(Type type) {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadArray(type);
                case HproseTags.TagRef: return (Array)ReadRef(type);
                default: throw CastError(TagToString(tag), type);
            }
        }

        public BitArray ReadBitArray() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    BitArray a = new BitArray(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a[i] = ReadBoolean();
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (BitArray)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofBitArray);
            }
        }

#if !(SILVERLIGHT || WINDOWS_PHONE || Core)
        public ArrayList ReadArrayList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    ArrayList a = new ArrayList(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(Unserialize());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (ArrayList)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofArrayList);
            }
        }

        public Queue ReadQueue() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    Queue a = new Queue(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Enqueue(Unserialize());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (Queue)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofQueue);
            }
        }

        public Stack ReadStack() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
#if !dotNETCF10
                    Stack a = new Stack(count);
#else
                    Stack a = new Stack();
#endif
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Push(Unserialize());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (Stack)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofStack);
            }
        }
#endif

        public IList ReadIList(Type type) {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    IList a = (IList)HproseHelper.NewInstance(type);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(Unserialize());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (IList)ReadRef(type);
                default: throw CastError(TagToString(tag), type);
            }
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        public List<T> ReadList<T>() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<T> a = new List<T>(count);
                    refer.Set(a);
                    Type type = typeof(T);
                    TypeEnum typeEnum = HproseHelper.GetTypeEnum(type);
                    for (int i = 0; i < count; ++i) {
                        a.Add((T)Unserialize(type, typeEnum));
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<T>)ReadRef();
                default: throw CastError(TagToString(tag), typeof(List<T>));
            }
        }

        public List<object> ReadObjectList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<object> a = new List<object>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(Unserialize());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<object>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofObjectList);
            }
        }

        public List<bool> ReadBooleanList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<bool> a = new List<bool>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadBoolean());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<bool>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofBooleanList);
            }
        }

        public List<char> ReadCharList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<char> a = new List<char>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadChar());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<char>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofCharList);
            }
        }

        [CLSCompliantAttribute(false)]
        public List<sbyte> ReadSByteList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<sbyte> a = new List<sbyte>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadSByte());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<sbyte>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofSByteList);
            }
        }

        public List<byte> ReadByteList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<byte> a = new List<byte>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadByte());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<byte>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofByteList);
            }
        }

        public List<short> ReadInt16List() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<short> a = new List<short>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadInt16());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<short>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt16List);
            }
        }

        [CLSCompliantAttribute(false)]
        public List<ushort> ReadUInt16List() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<ushort> a = new List<ushort>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadUInt16());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<ushort>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt16List);
            }
        }

        public List<int> ReadInt32List() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<int> a = new List<int>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadInt32());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<int>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt32List);
            }
        }

        [CLSCompliantAttribute(false)]
        public List<uint> ReadUInt32List() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<uint> a = new List<uint>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadUInt32());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<uint>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt32List);
            }
        }

        public List<long> ReadInt64List() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<long> a = new List<long>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadInt64());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<long>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofInt64List);
            }
        }

        [CLSCompliantAttribute(false)]
        public List<ulong> ReadUInt64List() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<ulong> a = new List<ulong>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadUInt64());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<ulong>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofUInt64List);
            }
        }

        public List<float> ReadSingleList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<float> a = new List<float>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadSingle());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<float>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofSingleList);
            }
        }

        public List<double> ReadDoubleList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<double> a = new List<double>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadDouble());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<double>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofDoubleList);
            }
        }

        public List<decimal> ReadDecimalList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<decimal> a = new List<decimal>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadDecimal());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<decimal>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofDecimalList);
            }
        }

        public List<DateTime> ReadDateTimeList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<DateTime> a = new List<DateTime>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadDateTime());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<DateTime>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofDateTimeList);
            }
        }

        public List<string> ReadStringList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<string> a = new List<string>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadString());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<string>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofStringList);
            }
        }

        public List<StringBuilder> ReadStringBuilderList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<StringBuilder> a = new List<StringBuilder>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadStringBuilder());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<StringBuilder>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofStringBuilderList);
            }
        }

        public List<Guid> ReadGuidList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<Guid> a = new List<Guid>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadGuid());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<Guid>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofGuidList);
            }
        }

        public List<BigInteger> ReadBigIntegerList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<BigInteger> a = new List<BigInteger>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadBigInteger());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<BigInteger>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofBigIntegerList);
            }
        }

        public List<TimeSpan> ReadTimeSpanList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<TimeSpan> a = new List<TimeSpan>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadTimeSpan());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<TimeSpan>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofTimeSpanList);
            }
        }

        public List<char[]> ReadCharsList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<char[]> a = new List<char[]>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadCharArray());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<char[]>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofCharsList);
            }
        }

        public List<byte[]> ReadBytesList() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    List<byte[]> a = new List<byte[]>(count);
                    refer.Set(a);
                    for (int i = 0; i < count; ++i) {
                        a.Add(ReadByteArray());
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (List<byte[]>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofBytesList);
            }
        }

        public Queue<T> ReadQueue<T>() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    Queue<T> a = new Queue<T>(count);
                    refer.Set(a);
                    Type type = typeof(T);
                    TypeEnum typeEnum = HproseHelper.GetTypeEnum(type);
                    for (int i = 0; i < count; ++i) {
                        a.Enqueue((T)Unserialize(type, typeEnum));
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (Queue<T>)ReadRef();
                default: throw CastError(TagToString(tag), typeof(Queue<T>));
            }
        }

        public Stack<T> ReadStack<T>() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    Stack<T> a = new Stack<T>(count);
                    refer.Set(a);
                    Type type = typeof(T);
                    TypeEnum typeEnum = HproseHelper.GetTypeEnum(type);
                    for (int i = 0; i < count; ++i) {
                        a.Push((T)Unserialize(type, typeEnum));
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (Stack<T>)ReadRef();
                default: throw CastError(TagToString(tag), typeof(Stack<T>));
            }
        }

        public IList<T> ReadIList<T>(Type type) {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    if (type == typeof(IList<T>)) type = typeof(List<T>);
                    IList<T> a = (IList<T>)HproseHelper.NewInstance(type);
                    refer.Set(a);
                    Type t = typeof(T);
                    TypeEnum typeEnum = HproseHelper.GetTypeEnum(t);
                    for (int i = 0; i < count; ++i) {
                        a.Add((T)Unserialize(t, typeEnum));
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (IList<T>)ReadRef(type);
                default: throw CastError(TagToString(tag), type);
            }
        }

        public ICollection<T> ReadICollection<T>(Type type) {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    if (type == typeof(ICollection<T>)) type = typeof(List<T>);
                    ICollection<T> a = (ICollection<T>)HproseHelper.NewInstance(type);
                    refer.Set(a);
                    Type t = typeof(T);
                    TypeEnum typeEnum = HproseHelper.GetTypeEnum(t);
                    for (int i = 0; i < count; ++i) {
                        a.Add((T)Unserialize(t, typeEnum));
                    }
                    stream.ReadByte();
                    return a;
                }
                case HproseTags.TagRef: return (ICollection<T>)ReadRef(type);
                default: throw CastError(TagToString(tag), type);
            }
        }
#endif

#if !(SILVERLIGHT || WINDOWS_PHONE || Core)
        public HashMap ReadHashMap() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    HashMap map = new HashMap(count);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        object key = i;
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    HashMap map = new HashMap(count);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        object key = Unserialize();
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagClass: {
                    ReadClass();
                    return ReadHashMap();
                }
                case HproseTags.TagObject: return (HashMap)ReadObjectAsMap(new HashMap());
                case HproseTags.TagRef: return (HashMap)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofHashMap);
            }
        }
#endif

        public IDictionary ReadIDictionary(Type type) {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    IDictionary map = (IDictionary)HproseHelper.NewInstance(type);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        object key = i;
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    IDictionary map = (IDictionary)HproseHelper.NewInstance(type);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        object key = Unserialize();
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagClass: {
                    ReadClass();
                    return ReadIDictionary(type);
                }
                case HproseTags.TagObject: {
                    IDictionary map = (IDictionary)HproseHelper.NewInstance(type);
                    return (IDictionary)ReadObjectAsMap(map);
                }
                case HproseTags.TagRef: return (IDictionary)ReadRef(type);
                default: throw CastError(TagToString(tag), type);
            }
        }

#if !(dotNET10 || dotNET11 || dotNETCF10)
        private HashMap<TKey, TValue> ReadListAsHashMap<TKey, TValue>() {
            int count = ReadInt(HproseTags.TagOpenbrace);
            HashMap<TKey, TValue> map = new HashMap<TKey, TValue>(count);
            refer.Set(map);
            Type keyType = typeof(TKey);
            Type valueType = typeof(TValue);
            TypeEnum valueTypeEnum = HproseHelper.GetTypeEnum(valueType);
            for (int i = 0; i < count; ++i) {
                TKey key = (TKey)Convert.ChangeType(i, keyType, null);
                TValue value = (TValue)Unserialize(valueType, valueTypeEnum);
                map[key] = value;
            }
            stream.ReadByte();
            return map;
        }

        public HashMap<TKey, TValue> ReadHashMap<TKey, TValue>() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsHashMap<TKey, TValue>();
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    HashMap<TKey, TValue> map = new HashMap<TKey, TValue>(count);
                    refer.Set(map);
                    Type keyType = typeof(TKey);
                    TypeEnum keyTypeEnum = HproseHelper.GetTypeEnum(keyType);
                    Type valueType = typeof(TValue);
                    TypeEnum valueTypeEnum = HproseHelper.GetTypeEnum(valueType);
                    for (int i = 0; i < count; ++i) {
                        TKey key = (TKey)Unserialize(keyType, keyTypeEnum);
                        TValue value = (TValue)Unserialize(valueType, valueTypeEnum);
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagRef: return (HashMap<TKey, TValue>)ReadRef();
                default: throw CastError(TagToString(tag), typeof(HashMap<TKey, TValue>));
            }
        }

        public HashMap<string, object> ReadStringObjectHashMap() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsHashMap<string, object>();
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    HashMap<string, object> map = new HashMap<string, object>(count);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        string key = ReadString();
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagClass: {
                    ReadClass();
                    return ReadStringObjectHashMap();
                }
                case HproseTags.TagObject: return (HashMap<string, object>)ReadObjectAsMap(new HashMap<string, object>());
                case HproseTags.TagRef: return (HashMap<string, object>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofStringObjectHashMap);
            }
        }

        public HashMap<object, object> ReadObjectObjectHashMap() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsHashMap<object, object>();
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    HashMap<object, object> map = new HashMap<object, object>(count);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        object key = Unserialize();
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagClass: {
                    ReadClass();
                    return ReadObjectObjectHashMap();
                }
                case HproseTags.TagObject: return (HashMap<object, object>)ReadObjectAsMap(new HashMap<object, object>());
                case HproseTags.TagRef: return (HashMap<object, object>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofObjectObjectHashMap);
            }
        }

        public HashMap<int, object> ReadIntObjectHashMap() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsHashMap<int, object>();
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    HashMap<int, object> map = new HashMap<int, object>(count);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        int key = ReadInt32();
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagRef: return (HashMap<int, object>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofIntObjectHashMap);
            }
        }

        private Dictionary<TKey, TValue> ReadListAsDictionary<TKey, TValue>() {
            int count = ReadInt(HproseTags.TagOpenbrace);
            Dictionary<TKey, TValue> map = new Dictionary<TKey, TValue>(count);
            refer.Set(map);
            Type keyType = typeof(TKey);
            Type valueType = typeof(TValue);
            TypeEnum valueTypeEnum = HproseHelper.GetTypeEnum(valueType);
            for (int i = 0; i < count; ++i) {
                TKey key = (TKey)Convert.ChangeType(i, keyType, null);
                TValue value = (TValue)Unserialize(valueType, valueTypeEnum);
                map[key] = value;
            }
            stream.ReadByte();
            return map;
        }

        public Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsDictionary<TKey, TValue>();
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    Dictionary<TKey, TValue> map = new Dictionary<TKey, TValue>(count);
                    refer.Set(map);
                    Type keyType = typeof(TKey);
                    TypeEnum keyTypeEnum = HproseHelper.GetTypeEnum(keyType);
                    Type valueType = typeof(TValue);
                    TypeEnum valueTypeEnum = HproseHelper.GetTypeEnum(valueType);
                    for (int i = 0; i < count; ++i) {
                        TKey key = (TKey)Unserialize(keyType, keyTypeEnum);
                        TValue value = (TValue)Unserialize(valueType, valueTypeEnum);
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagRef: return (Dictionary<TKey, TValue>)ReadRef();
                default: throw CastError(TagToString(tag), typeof(Dictionary<TKey, TValue>));
            }
        }

        public Dictionary<string, object> ReadStringObjectDictionary() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsDictionary<string, object>();
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    Dictionary<string, object> map = new Dictionary<string, object>(count);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        string key = ReadString();
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagClass: {
                    ReadClass();
                    return ReadStringObjectDictionary();
                }
                case HproseTags.TagObject: return (Dictionary<string, object>)ReadObjectAsMap(new Dictionary<string, object>());
                case HproseTags.TagRef: return (Dictionary<string, object>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofStringObjectDictionary);
            }
        }

        public Dictionary<object, object> ReadObjectObjectDictionary() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsDictionary<object, object>();
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    Dictionary<object, object> map = new Dictionary<object, object>(count);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        object key = Unserialize();
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagClass: {
                    ReadClass();
                    return ReadObjectObjectDictionary();
                }
                case HproseTags.TagObject: return (Dictionary<object, object>)ReadObjectAsMap(new Dictionary<object, object>());
                case HproseTags.TagRef: return (Dictionary<object, object>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofObjectObjectDictionary);
            }
        }

        public Dictionary<int, object> ReadIntObjectDictionary() {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsDictionary<int, object>();
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    Dictionary<int, object> map = new Dictionary<int, object>(count);
                    refer.Set(map);
                    for (int i = 0; i < count; ++i) {
                        int key = ReadInt32();
                        object value = Unserialize();
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagRef: return (Dictionary<int, object>)ReadRef();
                default: throw CastError(TagToString(tag), HproseHelper.typeofIntObjectDictionary);
            }
        }

        private IDictionary<TKey, TValue> ReadListAsIDictionary<TKey, TValue>(Type type) {
            int count = ReadInt(HproseTags.TagOpenbrace);
            if (type == typeof(IDictionary<TKey, TValue>)) type = typeof(Dictionary<TKey, TValue>);
            IDictionary<TKey, TValue> map = (IDictionary<TKey, TValue>)HproseHelper.NewInstance(type);
            refer.Set(map);
            Type keyType = typeof(TKey);
            Type valueType = typeof(TValue);
            TypeEnum valueTypeEnum = HproseHelper.GetTypeEnum(valueType);
            for (int i = 0; i < count; ++i) {
                TKey key = (TKey)Convert.ChangeType(i, keyType, null);
                TValue value = (TValue)Unserialize(valueType, valueTypeEnum);
                map[key] = value;
            }
            stream.ReadByte();
            return map;
        }

        public IDictionary<TKey, TValue> ReadIDictionary<TKey, TValue>(Type type) {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagList: return ReadListAsIDictionary<TKey, TValue>(type);
                case HproseTags.TagMap: {
                    int count = ReadInt(HproseTags.TagOpenbrace);
                    if (type == typeof(IDictionary<TKey, TValue>)) type = typeof(Dictionary<TKey, TValue>);
                    IDictionary<TKey, TValue> map = (IDictionary<TKey, TValue>)HproseHelper.NewInstance(type);
                    refer.Set(map);
                    Type keyType = typeof(TKey);
                    TypeEnum keyTypeEnum = HproseHelper.GetTypeEnum(keyType);
                    Type valueType = typeof(TValue);
                    TypeEnum valueTypeEnum = HproseHelper.GetTypeEnum(valueType);
                    for (int i = 0; i < count; ++i) {
                        TKey key = (TKey)Unserialize(keyType, keyTypeEnum);
                        TValue value = (TValue)Unserialize(valueType, valueTypeEnum);
                        map[key] = value;
                    }
                    stream.ReadByte();
                    return map;
                }
                case HproseTags.TagRef: return (IDictionary<TKey, TValue>)ReadRef(type);
                default: throw CastError(TagToString(tag), type);
            }
        }

#endif

        public object ReadObject(Type type) {
            int tag = stream.ReadByte();
            switch (tag) {
                case HproseTags.TagNull: return null;
                case HproseTags.TagMap: return ReadMapAsObject(type);
                case HproseTags.TagClass: ReadClass(); return ReadObject(type);
                case HproseTags.TagObject: return ReadObjectWithoutTag(type);
                case HproseTags.TagRef: return ReadRef(type);
                default: throw CastError(TagToString(tag), type);
            }
        }

        public object Unserialize(Type type) {
            return Unserialize(type, HproseHelper.GetTypeEnum(type));
        }

        private object Unserialize(Type type, TypeEnum typeEnum) {
            switch (typeEnum) {
                case TypeEnum.Null: return Unserialize();
                case TypeEnum.Object: return ReadObject();
#if !Core
                case TypeEnum.DBNull: return ReadDBNull();
#endif
                case TypeEnum.Boolean: return ReadBoolean();
                case TypeEnum.Char: return ReadChar();
                case TypeEnum.SByte: return ReadSByte();
                case TypeEnum.Byte: return ReadByte();
                case TypeEnum.Int16: return ReadInt16();
                case TypeEnum.UInt16: return ReadUInt16();
                case TypeEnum.Int32: return ReadInt32();
                case TypeEnum.UInt32: return ReadUInt32();
                case TypeEnum.Int64: return ReadInt64();
                case TypeEnum.UInt64: return ReadUInt64();
                case TypeEnum.Single: return ReadSingle();
                case TypeEnum.Double: return ReadDouble();
                case TypeEnum.Decimal: return ReadDecimal();
                case TypeEnum.DateTime: return ReadDateTime();
                case TypeEnum.String: return ReadString();
                case TypeEnum.StringBuilder: return ReadStringBuilder();
                case TypeEnum.Guid: return ReadGuid();
                case TypeEnum.BigInteger: return ReadBigInteger();
                case TypeEnum.TimeSpan: return ReadTimeSpan();
                case TypeEnum.Stream:
                case TypeEnum.MemoryStream: return ReadStream();
                case TypeEnum.Enum: return ReadEnum(type);
                case TypeEnum.ObjectArray: return ReadObjectArray();
                case TypeEnum.BooleanArray: return ReadBooleanArray();
                case TypeEnum.CharArray: return ReadCharArray();
                case TypeEnum.SByteArray: return ReadSByteArray();
                case TypeEnum.ByteArray: return ReadByteArray();
                case TypeEnum.Int16Array: return ReadInt16Array();
                case TypeEnum.UInt16Array: return ReadUInt16Array();
                case TypeEnum.Int32Array: return ReadInt32Array();
                case TypeEnum.UInt32Array: return ReadUInt32Array();
                case TypeEnum.Int64Array: return ReadInt64Array();
                case TypeEnum.UInt64Array: return ReadUInt64Array();
                case TypeEnum.SingleArray: return ReadSingleArray();
                case TypeEnum.DoubleArray: return ReadDoubleArray();
                case TypeEnum.DecimalArray: return ReadDecimalArray();
                case TypeEnum.DateTimeArray: return ReadDateTimeArray();
                case TypeEnum.StringArray: return ReadStringArray();
                case TypeEnum.StringBuilderArray: return ReadStringBuilderArray();
                case TypeEnum.GuidArray: return ReadGuidArray();
                case TypeEnum.BigIntegerArray: return ReadBigIntegerArray();
                case TypeEnum.TimeSpanArray: return ReadTimeSpanArray();
                case TypeEnum.CharsArray: return ReadCharsArray();
                case TypeEnum.BytesArray: return ReadBytesArray();
                case TypeEnum.OtherTypeArray: return ReadOtherTypeArray(type);
                case TypeEnum.BitArray: return ReadBitArray();
#if !(SILVERLIGHT || WINDOWS_PHONE || Core)
                case TypeEnum.ArrayList: return ReadArrayList();
                case TypeEnum.Queue: return ReadQueue();
                case TypeEnum.Stack: return ReadStack();
                case TypeEnum.Hashtable:
                case TypeEnum.HashMap: return ReadHashMap();
#endif
#if !(dotNET10 || dotNET11 || dotNETCF10)
                case TypeEnum.NullableBoolean: return ReadNullableBoolean();
                case TypeEnum.NullableChar: return ReadNullableChar();
                case TypeEnum.NullableSByte: return ReadNullableSByte();
                case TypeEnum.NullableByte: return ReadNullableByte();
                case TypeEnum.NullableInt16: return ReadNullableInt16();
                case TypeEnum.NullableUInt16: return ReadNullableUInt16();
                case TypeEnum.NullableInt32: return ReadNullableInt32();
                case TypeEnum.NullableUInt32: return ReadNullableUInt32();
                case TypeEnum.NullableInt64: return ReadNullableInt64();
                case TypeEnum.NullableUInt64: return ReadNullableUInt64();
                case TypeEnum.NullableSingle: return ReadNullableSingle();
                case TypeEnum.NullableDouble: return ReadNullableDouble();
                case TypeEnum.NullableDecimal: return ReadNullableDecimal();
                case TypeEnum.NullableDateTime: return ReadNullableDateTime();
                case TypeEnum.NullableGuid: return ReadNullableGuid();
                case TypeEnum.NullableBigInteger: return ReadNullableBigInteger();
                case TypeEnum.NullableTimeSpan: return ReadNullableTimeSpan();
                case TypeEnum.ObjectList:
                case TypeEnum.ObjectIList: return ReadObjectList();
                case TypeEnum.BooleanList:
                case TypeEnum.BooleanIList: return ReadBooleanList();
                case TypeEnum.CharList:
                case TypeEnum.CharIList: return ReadCharList();
                case TypeEnum.SByteList:
                case TypeEnum.SByteIList: return ReadSByteList();
                case TypeEnum.ByteList:
                case TypeEnum.ByteIList: return ReadByteList();
                case TypeEnum.Int16List:
                case TypeEnum.Int16IList: return ReadInt16List();
                case TypeEnum.UInt16List:
                case TypeEnum.UInt16IList: return ReadUInt16List();
                case TypeEnum.Int32List:
                case TypeEnum.Int32IList: return ReadInt32List();
                case TypeEnum.UInt32List:
                case TypeEnum.UInt32IList: return ReadUInt32List();
                case TypeEnum.Int64List:
                case TypeEnum.Int64IList: return ReadInt64List();
                case TypeEnum.UInt64List:
                case TypeEnum.UInt64IList: return ReadUInt64List();
                case TypeEnum.SingleList:
                case TypeEnum.SingleIList: return ReadSingleList();
                case TypeEnum.DoubleList:
                case TypeEnum.DoubleIList: return ReadDoubleList();
                case TypeEnum.DecimalList:
                case TypeEnum.DecimalIList: return ReadDecimalList();
                case TypeEnum.DateTimeList:
                case TypeEnum.DateTimeIList: return ReadDateTimeList();
                case TypeEnum.StringList:
                case TypeEnum.StringIList: return ReadStringList();
                case TypeEnum.StringBuilderList:
                case TypeEnum.StringBuilderIList: return ReadStringBuilderList();
                case TypeEnum.GuidList:
                case TypeEnum.GuidIList: return ReadGuidList();
                case TypeEnum.BigIntegerList:
                case TypeEnum.BigIntegerIList: return ReadBigIntegerList();
                case TypeEnum.TimeSpanList:
                case TypeEnum.TimeSpanIList: return ReadTimeSpanList();
                case TypeEnum.CharsList:
                case TypeEnum.CharsIList: return ReadCharsList();
                case TypeEnum.BytesList:
                case TypeEnum.BytesIList: return ReadBytesList();
                case TypeEnum.StringObjectHashMap: return ReadStringObjectHashMap();
                case TypeEnum.ObjectObjectHashMap: return ReadObjectObjectHashMap();
                case TypeEnum.IntObjectHashMap: return ReadIntObjectHashMap();
                case TypeEnum.StringObjectDictionary: return ReadStringObjectDictionary();
                case TypeEnum.ObjectObjectDictionary: return ReadObjectObjectDictionary();
                case TypeEnum.IntObjectDictionary: return ReadIntObjectDictionary();
                case TypeEnum.GenericList: return HproseHelper.GetIGListReader(type).ReadList(this);
                case TypeEnum.GenericDictionary: return HproseHelper.GetIGDictionaryReader(type).ReadDictionary(this);
                case TypeEnum.GenericQueue: return HproseHelper.GetIGQueueReader(type).ReadQueue(this);
                case TypeEnum.GenericStack: return HproseHelper.GetIGStackReader(type).ReadStack(this);
                case TypeEnum.GenericIList: return HproseHelper.GetIGIListReader(type).ReadIList(this, type);
                case TypeEnum.GenericICollection: return HproseHelper.GetIGICollectionReader(type).ReadICollection(this, type);
                case TypeEnum.GenericIDictionary: return HproseHelper.GetIGIDictionaryReader(type).ReadIDictionary(this, type);
                case TypeEnum.ICollection:
                case TypeEnum.IList: return ReadObjectList();
                case TypeEnum.IDictionary: return ReadObjectObjectHashMap();
#else
                case TypeEnum.ICollection:
                case TypeEnum.IList: return ReadArrayList();
                case TypeEnum.IDictionary: return ReadHashMap();
#endif
                case TypeEnum.List: return ReadIList(type);
                case TypeEnum.Dictionary: return ReadIDictionary(type);
                case TypeEnum.OtherType: return ReadObject(type);
            }
            throw new HproseException("Can not unserialize this type: " + type.FullName);
        }

        public MemoryStream ReadRaw() {
            MemoryStream ostream = new MemoryStream(4096);
            ReadRaw(ostream);
            return ostream;
        }

        public void ReadRaw(Stream ostream) {
            ReadRaw(ostream, stream.ReadByte());
        }

        private void ReadRaw(Stream ostream, int tag) {
            ostream.WriteByte((byte)tag);
            switch (tag) {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case HproseTags.TagNull:
                case HproseTags.TagEmpty:
                case HproseTags.TagTrue:
                case HproseTags.TagFalse:
                case HproseTags.TagNaN:
                    break;
                case HproseTags.TagInfinity:
                    ostream.WriteByte((byte)stream.ReadByte());
                    break;
                case HproseTags.TagInteger:
                case HproseTags.TagLong:
                case HproseTags.TagDouble:
                case HproseTags.TagRef:
                    ReadNumberRaw(ostream);
                    break;
                case HproseTags.TagDate:
                case HproseTags.TagTime:
                    ReadDateTimeRaw(ostream);
                    break;
                case HproseTags.TagUTF8Char:
                    ReadUTF8CharRaw(ostream);
                    break;
                case HproseTags.TagBytes:
                    ReadBytesRaw(ostream);
                    break;
                case HproseTags.TagString:
                    ReadStringRaw(ostream);
                    break;
                case HproseTags.TagGuid:
                    ReadGuidRaw(ostream);
                    break;
                case HproseTags.TagList:
                case HproseTags.TagMap:
                case HproseTags.TagObject:
                    ReadComplexRaw(ostream);
                    break;
                case HproseTags.TagClass:
                    ReadComplexRaw(ostream);
                    ReadRaw(ostream);
                    break;
                case HproseTags.TagError:
                    ReadRaw(ostream);
                    break;
                default: throw UnexpectedTag(tag);
            }
        }

        private void ReadNumberRaw(Stream ostream) {
            int tag;
            do {
                tag = stream.ReadByte();
                ostream.WriteByte((byte)tag);
            } while (tag != HproseTags.TagSemicolon);
        }

        private void ReadDateTimeRaw(Stream ostream) {
            int tag;
            do {
                tag = stream.ReadByte();
                ostream.WriteByte((byte)tag);
            } while (tag != HproseTags.TagSemicolon &&
                     tag != HproseTags.TagUTC);
        }

        private void ReadUTF8CharRaw(Stream ostream) {
            int tag = stream.ReadByte();
            switch (tag >> 4) {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7: {
                    // 0xxx xxxx
                    ostream.WriteByte((byte)tag);
                    break;
                }
                case 12:
                case 13: {
                    // 110x xxxx   10xx xxxx
                    ostream.WriteByte((byte)tag);
                    ostream.WriteByte((byte)stream.ReadByte());
                    break;
                }
                case 14: {
                    // 1110 xxxx  10xx xxxx  10xx xxxx
                    ostream.WriteByte((byte)tag);
                    ostream.WriteByte((byte)stream.ReadByte());
                    ostream.WriteByte((byte)stream.ReadByte());
                    break;
                }
                default:
                    throw new HproseException("bad utf-8 encoding at " +
                                              ((tag < 0) ? "end of stream" :
                                                  "0x" + (tag & 0xff).ToString("x2")));
            }
        }

        private void ReadBytesRaw(Stream ostream) {
            int len = 0;
            int tag = '0';
            do {
                len *= 10;
                len += tag - '0';
                tag = stream.ReadByte();
                ostream.WriteByte((byte)tag);
            } while (tag != HproseTags.TagQuote);
            int off = 0;
            byte[] b = new byte[len];
            while (len > 0) {
                int size = stream.Read(b, off, len);
                off += size;
                len -= size;
            }
            ostream.Write(b, 0, b.Length);
            ostream.WriteByte((byte)stream.ReadByte());
        }

        private void ReadStringRaw(Stream ostream) {
            int count = 0;
            int tag = '0';
            do {
                count *= 10;
                count += tag - '0';
                tag = stream.ReadByte();
                ostream.WriteByte((byte)tag);
            } while (tag != HproseTags.TagQuote);
            for (int i = 0; i < count; ++i) {
                tag = stream.ReadByte();
                switch (tag >> 4) {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                    case 6:
                    case 7: {
                        // 0xxx xxxx
                        ostream.WriteByte((byte)tag);
                        break;
                    }
                    case 12:
                    case 13: {
                        // 110x xxxx   10xx xxxx
                        ostream.WriteByte((byte)tag);
                        ostream.WriteByte((byte)stream.ReadByte());
                        break;
                    }
                    case 14: {
                        // 1110 xxxx  10xx xxxx  10xx xxxx
                        ostream.WriteByte((byte)tag);
                        ostream.WriteByte((byte)stream.ReadByte());
                        ostream.WriteByte((byte)stream.ReadByte());
                        break;
                    }
                    case 15: {
                        // 1111 0xxx  10xx xxxx  10xx xxxx  10xx xxxx
                        if ((tag & 0xf) <= 4) {
                            ostream.WriteByte((byte)tag);
                            ostream.WriteByte((byte)stream.ReadByte());
                            ostream.WriteByte((byte)stream.ReadByte());
                            ostream.WriteByte((byte)stream.ReadByte());
                            ++i;
                            break;
                        }
                        goto default;
                    // no break here!! here need throw exception.
                    }
                    default:
                        throw new HproseException("bad utf-8 encoding at " +
                                                  ((tag < 0) ? "end of stream" :
                                                      "0x" + (tag & 0xff).ToString("x2")));
                }
            }
            ostream.WriteByte((byte)stream.ReadByte());
        }

        private void ReadGuidRaw(Stream ostream) {
            int len = 38;
            int off = 0;
            byte[] b = new byte[len];
            while (len > 0) {
                int size = stream.Read(b, off, len);
                off += size;
                len -= size;
            }
            ostream.Write(b, 0, b.Length);
        }

        private void ReadComplexRaw(Stream ostream) {
            int tag;
            do {
                tag = stream.ReadByte();
                ostream.WriteByte((byte)tag);
            } while (tag != HproseTags.TagOpenbrace);
            while ((tag = stream.ReadByte()) != HproseTags.TagClosebrace) {
                ReadRaw(ostream, tag);
            }
            ostream.WriteByte((byte)tag);
        }

        public void Reset() {
            refer.Reset();
            classref.Clear();
            membersref.Clear();
        }
    }
}