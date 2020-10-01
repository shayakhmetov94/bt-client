using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace commons_bittorrent.Base.Bencoding
{
    /// <summary>
    /// Non DLR bencoder
    /// </summary>
    public class Bencoder
    {
        private string __curField = null;

        public Encoding Encoding { get; set; }

        public Bencoder() {
            Encoding = Encoding.UTF8;
        }

        public Bencoder(Encoding encoding) {
            Encoding = encoding;
        }

        /// <summary>
        /// Bencode as Dictionary<>, List<>, string etc
        /// </summary>
        /// <param name="buf">Raw bencode</param>
        /// <returns>Structured bencode</returns>
        public object DecodeElement(byte[] buf) {
            int offset = 0;
            return DecodeElement(buf, ref offset);
        }

        private object DecodeElement(byte[] buf, ref int offset) {

            object decodedVal = null;
            try {
                switch ((char)buf[offset]) {
                    case 'i': {
                            StringBuilder intBuilder = new StringBuilder(16);
                            int pos = offset;
                            pos++;
                            while (buf[pos] != 'e') {
                                intBuilder.Append((char)buf[pos++]);
                            }

                            pos++; //skip 'e'
                            offset = pos;
                            decodedVal = int.Parse(intBuilder.ToString());
                            break;
                        }
                    case 'l': {
                            offset++;
                            decodedVal = DecodeList(buf, ref offset);
                            break;
                        }
                    case 'd': {
                            offset++;
                            decodedVal = DecodeDictionary(buf, ref offset);
                            break;
                        }
                    case 'e': { //got something empty
                            offset--; //push back 'e'
                            return null;
                        }
                    default: { //string
                            StringBuilder stringBuilder = new StringBuilder(4);
                            int pos = offset;
                            while (buf[pos] != ':') {
                                stringBuilder.Append((char)buf[pos]);
                                pos++;
                            }
                            pos++;
                            int length = -1;
                            if (!Int32.TryParse(stringBuilder.ToString(), out length)) {
                                throw new InvalidFieldException(__curField);
                            }

                            if (length > buf.Length) {
                                throw new InvalidFieldException(__curField);
                            }

                            MemoryStream bytesBuilder = new MemoryStream(length);
                            bytesBuilder.Write(buf, pos, length);

                            pos += length;
                            offset = pos;
                            decodedVal = bytesBuilder.ToArray();
                            break;
                        }
                }
            } catch (Exception) {
                throw new InvalidFieldException(__curField);
            }

            return decodedVal;
        }

        private List<object> DecodeList(byte[] buf, ref int offset) {
            int pos = offset;
            List<object> list = new List<object>();
            while (buf[pos] != 'e') {
                list.Add(DecodeElement(buf, ref pos));
            }

            pos++; //skip 'e'
            offset = pos;
            return list;
        }

        private Dictionary<string, object> DecodeDictionary(byte[] buf, ref int offset) {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            string key;
            int pos = offset;
            while (buf[pos] != 'e') {
                key = Encoding.GetString((byte[])DecodeElement(buf, ref pos));
                __curField = key;
                dict.Add(key, DecodeElement(buf, ref pos));
            }
            pos++; //skip 'e'
            offset = pos;
            return dict;
        }

        public byte[] EncodeElement(object obj) {
            MemoryStream bytesBuilder = new MemoryStream(128);
            if (obj is IList<KeyValuePair<string, object>> kvPairsList) {
                bytesBuilder.WriteByte((byte)'d');
                foreach (var kv in kvPairsList) {
                    byte[] keyBytes = Encoding.GetBytes((kv.Key.Length).ToString() + ':' + kv.Key);
                    bytesBuilder.Write(keyBytes, 0, keyBytes.Length);
                    byte[] valBytes = EncodeElement(kv.Value);
                    bytesBuilder.Write(valBytes, 0, valBytes.Length);
                }
                bytesBuilder.WriteByte((byte)'e');

                return bytesBuilder.ToArray();
            }

            if (obj is Dictionary<string, object> dictionary) {
                bytesBuilder.WriteByte((byte)'d');
                foreach (var kv in dictionary) {
                    byte[] keyBytes = Encoding.GetBytes((kv.Key.Length).ToString() + ':' + kv.Key);
                    bytesBuilder.Write(keyBytes, 0, keyBytes.Length);
                    byte[] valBytes = EncodeElement(kv.Value);
                    bytesBuilder.Write(valBytes, 0, valBytes.Length);
                }
                bytesBuilder.WriteByte((byte)'e');

                return bytesBuilder.ToArray();
            }

            if (obj is IEnumerable<object>) {
                IEnumerable<object> list = (IEnumerable<object>)obj;

                bytesBuilder.WriteByte((byte)'l');
                foreach (var val in list) {
                    byte[] valBytes = EncodeElement(val);
                    bytesBuilder.Write(valBytes, 0, valBytes.Length);
                }
                bytesBuilder.WriteByte((byte)'e');
                return bytesBuilder.ToArray();
            }
            if (obj is int || obj is long) {
                bytesBuilder.WriteByte((byte)'i');
                byte[] intBytes = Encoding.GetBytes(Convert.ToString(obj));
                bytesBuilder.Write(intBytes, 0, intBytes.Length);
                bytesBuilder.WriteByte((byte)'e');
                return bytesBuilder.ToArray();
            }
            if (obj is string) {
                return EncodeString(bytesBuilder, (string)obj);
            }

            if (obj is byte[]) {
                return EncodeAsString(bytesBuilder, (byte[])obj);
            }

            throw new ArgumentException("Type " + obj.GetType() + " does not supported.");
        }

        private byte[] EncodeAsString(MemoryStream ms, byte[] strBytes) {
            byte[] lengthBytes = Encoding.GetBytes(strBytes.Length.ToString());

            ms.Write(lengthBytes, 0, lengthBytes.Length);
            ms.WriteByte((byte)':');

            //strBytes[strBytes.Length - 1] = (byte)(strBytes[strBytes.Length - 1] >> 8);
            ms.Write(strBytes, 0, strBytes.Length);
            return ms.ToArray();
        }


        private byte[] EncodeString(MemoryStream ms, string str) {
            byte[] lengthBytes = Encoding.GetBytes(Convert.ToString(str.Length));

            ms.Write(lengthBytes, 0, lengthBytes.Length);
            ms.WriteByte((byte)':');

            byte[] strBytes = Encoding.GetBytes(str);
            //strBytes[strBytes.Length - 1] = (byte)(strBytes[strBytes.Length - 1] >> 8);
            ms.Write(strBytes, 0, strBytes.Length);
            return ms.ToArray();
        }
    }
}
