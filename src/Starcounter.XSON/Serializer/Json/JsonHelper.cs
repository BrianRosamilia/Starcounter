using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Starcounter.Internal;
using Starcounter.Templates;

namespace Starcounter.Advanced.XSON {
    /// <summary>
    /// 
    /// </summary>
    public static class JsonHelper {
        private static byte[] null_value = { (byte)'n', (byte)'u', (byte)'l', (byte)'l' };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfrag"></param>
        /// <param name="fragmentSize"></param>
        /// <returns></returns>
        private static unsafe int SizeToDelimiterOrEnd(byte* pfrag, int fragmentSize) {
            byte current;
            int index = 0;

            while (index < fragmentSize) {
                current = pfrag[index];

                if (current == ',' || current == ' '
                    || current == '}' || current == '\n'
                    || current == '\r' || current == ':')
                    break;
                index++;
            }
            return index;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfrag"></param>
        /// <param name="fragmentSize"></param>
        /// <param name="needsJsonDecoding"></param>
        /// <returns></returns>
        private static unsafe int SizeToDelimiterOrEndString(byte* pfrag, int fragmentSize, out bool needsJsonDecoding) {
            byte current;
            int index = 0;


            needsJsonDecoding = false;
            while (index < fragmentSize) {
                current = pfrag[index];

                if (current == '\\') {
                    needsJsonDecoding = true;
                    index += 2;
                    continue;
                }
                if (pfrag[index] == '"')
                    break;
                index++;
            }
            return index;
        }

        /// <summary>
        /// Checks the size of the value and try to parse it as an int.
        /// </summary>
        /// <param name="ptr">Pointer to the start of the value.</param>
        /// <param name="size">The size to the end of the buf.</param>
        /// <param name="value">The parsed value.</param>
        /// <param name="valueSize">The size of the unparsed value in bytes</param>
        /// <returns><c>true</c> if value was succesfully parsed, <c>false</c> otherwise</returns>
        public static bool ParseInt(IntPtr ptr, int size, out long value, out int valueSize) {
            long result;

            unsafe {
                byte* pfrag = (byte*)ptr;
                valueSize = SizeToDelimiterOrEnd(pfrag, size);
                if (IsNullValue(pfrag, valueSize)) {
                    value = 0;
                    return true;
                }
            }

            if (Utf8Helper.IntFastParseFromAscii(ptr, valueSize, out result)) {
                value = result;
                return true;
            }
            value = -1;
            return false;
        }

        /// <summary>
        /// Checks the size of the value and try to parse it as a decimal.
        /// </summary>
        /// <param name="ptr">Pointer to the start of the value.</param>
        /// <param name="size">The size to the end of the buf.</param>
        /// <param name="value">The parsed value.</param>
        /// <param name="valueSize">The size of the unparsed value in bytes</param>
        /// <returns><c>true</c> if value was succesfully parsed, <c>false</c> otherwise</returns>
        public static bool ParseDecimal(IntPtr ptr, int size, out decimal value, out int valueSize) {
            bool success;
            string valAsStr;

            // TODO:
            // Need to optimize this parsing. This method is superslow.

            value = 0.0m;
            success = false;
            if (ParseString(ptr, size, out valAsStr, out valueSize)) {
                success = decimal.TryParse(valAsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
            }
            return success;
        }

        /// <summary>
        /// Checks the size of the value and try to parse it as a double.
        /// </summary>
        /// <param name="ptr">Pointer to the start of the value.</param>
        /// <param name="size">The size to the end of the buf.</param>
        /// <param name="value">The parsed value.</param>
        /// <param name="valueSize">The size of the unparsed value in bytes</param>
        /// <returns><c>true</c> if value was succesfully parsed, <c>false</c> otherwise</returns>
        public static bool ParseDouble(IntPtr ptr, int size, out double value, out int valueSize) {
            bool success;
            string valAsStr;

            // TODO:
            // Need to optimize this parsing. This method is superslow.

            value = 0.0d;
            success = false;
            if (ParseString(ptr, size, out valAsStr, out valueSize)) {
                success = double.TryParse(valAsStr, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }
            return success;
        }

        /// <summary>
        /// Checks the size of the value and try to parse it as a boolean.
        /// </summary>
        /// <param name="ptr">Pointer to the start of the value.</param>
        /// <param name="size">The size to the end of the buf.</param>
        /// <param name="value">The parsed value.</param>
        /// <param name="valueSize">The size of the unparsed value in bytes</param>
        /// <returns><c>true</c> if value was succesfully parsed, <c>false</c> otherwise</returns>
        public static bool ParseBoolean(IntPtr ptr, int size, out bool value, out int valueSize) {
            bool success = false;

            value = false;
            valueSize = -1;
            if (size != 0) {
                unsafe {
                    byte* p = (byte*)ptr;

                    switch (*p) {
                        case (byte)'t':
                            if (size < 4)
                                break;

                            if (p[1] == 'r' && p[2] == 'u' && p[3] == 'e') {
                                value = true;
                                valueSize = 4;
                                success = true;
                            }
                            break;
                        case (byte)'f':
                            if (size < 5)
                                break;

                            if (p[1] == 'a' && p[2] == 'l' && p[3] == 's' && p[4] == 'e') {
                                valueSize = 5;
                                success = true;
                            }
                            break;
                        case (byte)'n':
                            if (size < 4)
                                break;
                            if (p[1] == 'u' && p[2] == 'l' && p[3] == 'l') {
                                valueSize = 4;
                                success = true;
                            }
                            break;
                    }
                }
            }
            return success;
        }

        /// <summary>
        /// Checks the size of the value and try to parse it as a DateTime.
        /// </summary>
        /// <param name="ptr">Pointer to the start of the value.</param>
        /// <param name="size">The size to the end of the buf.</param>
        /// <param name="value">The parsed value.</param>
        /// <param name="valueSize">The size of the unparsed value in bytes</param>
        /// <returns><c>true</c> if value was succesfully parsed, <c>false</c> otherwise</returns>
        public static bool ParseDateTime(IntPtr ptr, int size, out DateTime value, out int valueSize) {
            bool success;
            string valAsStr;

            // TODO:
            // Need to optimize this parsing. This method is superslow.

            value = DateTime.MinValue;
            success = false;
            if (ParseString(ptr, size, out valAsStr, out valueSize)) {
                success = DateTime.TryParse(valAsStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
            }
            return success;
        }

        /// <summary>
        /// Parses the string and decodes it if needed.
        /// </summary>
        /// <param name="ptr">A pointer to the current position in the buf.</param>
        /// <param name="size">The size left to the end of the buf.</param>
        /// <param name="value">The parsed value.</param>
        /// <param name="valueSize">The size of the unparsed value in bytes</param>
        /// <returns><c>true</c> if parsing was succesful, <c>false</c> otherwise</returns>
        public static bool ParseString(IntPtr ptr, int size, out string value, out int valueSize) {
            bool needsDecoding;
            byte[] buffer;
            int extraSize = 0;

            unsafe {
                byte* pfrag = (byte*)ptr;

                if (*pfrag == '"') { // Value is enclosed in start and stop quote, "value".
                    pfrag++;
                    size--;
                    extraSize = 2;
                    valueSize = SizeToDelimiterOrEndString(pfrag, size, out needsDecoding);
                } else {
                    needsDecoding = false;
                    valueSize = SizeToDelimiterOrEnd(pfrag, size);
                    if (IsNullValue(pfrag, valueSize)) {
                        value = null;
                        return true;
                    }
                }

                if (!needsDecoding) {
                    buffer = new byte[valueSize];
                    Marshal.Copy((IntPtr)pfrag, buffer, 0, valueSize);
                    value = Encoding.UTF8.GetString(buffer, 0, valueSize);
                } else {
                    value = DecodeString(pfrag, size, valueSize);
                }
            }

            valueSize += extraSize;
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfrag"></param>
        /// <param name="srcSize"></param>
        /// <param name="valueSize"></param>
        /// <returns></returns>
        internal static unsafe string DecodeString(byte* pfrag, int srcSize, int valueSize) {
            byte current;
            int bufferOffset = 0;
            byte[] buffer = new byte[valueSize];
            for (int i = 0; i < valueSize; i++) {
                current = pfrag[i];

                if (current != '\\') {
                    buffer[bufferOffset++] = current;
                } else {
                    i++;
                    current = pfrag[i];
                    switch (current) {
                        case (byte)'\\':
                        case (byte)'"':
                            buffer[bufferOffset++] = current;
                            break;
                        case (byte)'b':
                            buffer[bufferOffset++] = (byte)'\b';
                            break;
                        case (byte)'f':
                            buffer[bufferOffset++] = (byte)'\f';
                            break;
                        case (byte)'n':
                            buffer[bufferOffset++] = (byte)'\n';
                            break;
                        case (byte)'r':
                            buffer[bufferOffset++] = (byte)'\r';
                            break;
                        case (byte)'t':
                            buffer[bufferOffset++] = (byte)'\t';
                            break;
                        case (byte)'u':
                            int unicode = 0;
                            unicode += (HexToInt(pfrag[i + 1]) << 12);
                            unicode += (HexToInt(pfrag[i + 2]) << 8);
                            unicode += (HexToInt(pfrag[i + 3]) << 4);
                            unicode += (HexToInt(pfrag[i + 4]));
                            i += 4;
                            buffer[bufferOffset++] = (byte)unicode;
                            break;
                    }
                }
                continue;
            }
            return Encoding.UTF8.GetString(buffer, 0, bufferOffset);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <param name="tmpArr"></param>
        /// <returns></returns>
        public static int WriteString(IntPtr buffer, int bufferSize, string value) {
            byte c;
            byte[] valueArr;
            int usedSize;

            unsafe {
                byte* pfrag = (byte*)buffer;

                if (value != null) {
                    valueArr = Encoding.UTF8.GetBytes(value);
                    usedSize = valueArr.Length + 2;
                    if (bufferSize < usedSize)
                        return -1;

                    *pfrag++ = (byte)'"';
                    for (int i = 0; i < valueArr.Length; i++) {
                        c = valueArr[i];

                        if (c >= ' ' && c < 128 && c != '\\' && c != '"') {
                            *pfrag++ = c;
                            continue;
                        }

                        switch (c){
                            case (byte)'\\':
                            case (byte)'"':
                                // c is already the character to write.
                                break;
                            case (byte)'\b':
                                c = (byte)'b';
                                break;
                            case (byte)'\f':
                                c = (byte)'f';
                                break;
                            case (byte)'\n':
                                c = (byte)'n';
                                break;
                            case (byte)'\r':
                                c = (byte)'r';
                                break;
                            case (byte)'\t':
                                c = (byte)'t';
                                break;
                            default:
                                if (c <= '\u001f') {
                                    usedSize += 5;
                                    if (usedSize > bufferSize)
                                        return -1;

                                    *pfrag++ = (byte)'\\';
                                    *pfrag++ = (byte)'u';
                                    *pfrag++ = IntToHex((c >> 12) & '\x000f');
                                    *pfrag++ = IntToHex((c >> 8) & '\x000f');
                                    *pfrag++ = IntToHex((c >> 4) & '\x000f');
                                    *pfrag++ = IntToHex(c & '\x000f');
                                } else {
                                    *pfrag++ = c;
                                }
                                continue;
                        }

                        // Check buffersize and write special character (except unicode)
                        usedSize++;
                        if (usedSize > bufferSize)
                            return -1;
                        *pfrag++ = (byte)'\\';
                        *pfrag++ = c;
                    }
                    *pfrag = (byte)'"';
                    return usedSize;
                } else {
					if (bufferSize < 2)
						return -1;

					*pfrag++ = (byte)'"';
					*pfrag = (byte)'"';
					return 2;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <param name="tmpArr"></param>
        /// <returns></returns>
        public static int WriteDouble(IntPtr ptr, int size, double value) {
            unsafe {
                byte* pfrag = (byte*)ptr;
                return WriteStringNoQuotations(pfrag, size, value.ToString("0.0###########################", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <param name="tmpArr"></param>
        /// <returns></returns>
        public static int WriteDecimal(IntPtr ptr, int size, decimal value) {
            unsafe {
                byte* pfrag = (byte*)ptr;
                return WriteStringNoQuotations(pfrag, size, value.ToString("0.0###########################", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfrag"></param>
        /// <param name="size"></param>
        /// <param name="valueAsStr"></param>
        /// <returns></returns>
        private unsafe static int WriteStringNoQuotations(byte* pfrag, int size, string valueAsStr) {
            byte[] valueArr = Encoding.UTF8.GetBytes(valueAsStr);
            if (size < valueArr.Length)
                return -1;
            fixed (byte* src = valueArr) {
                BitsAndBytes.MemCpy(pfrag, src, (uint)valueArr.Length);
            }
            pfrag += valueArr.Length;
            return valueArr.Length;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int WriteInt(IntPtr ptr, int size, long value) {
            unsafe {
                byte* p = (byte*)ptr;
                return (int)Utf8Helper.WriteIntAsUtf8(p, value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int WriteBool(IntPtr ptr, int size, bool value) {
            unsafe {
                byte* p = (byte*)ptr;

                if (value) {
                    if (size < 4)
                        return -1;
                    *p++ = (byte)'t';
                    *p++ = (byte)'r';
                    *p++ = (byte)'u';
                    *p = (byte)'e';
                    return 4;
                } else {
                    if (size < 5)
                        return -1;
                    *p++ = (byte)'f';
                    *p++ = (byte)'a';
                    *p++ = (byte)'l';
                    *p++ = (byte)'s';
                    *p = (byte)'e';
                    return 5;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static int WriteNull(IntPtr ptr, int size) {
            if (size < 4)
                return -1;
            unsafe {
                return WriteNull((byte*)ptr, size);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <param name="valueSize"></param>
        /// <returns></returns>
        public static bool IsNullValue(IntPtr ptr, int size, out int valueSize) {
            unsafe {
                byte* pfrag = (byte*)ptr;
                valueSize = SizeToDelimiterOrEnd(pfrag, size);
                return IsNullValue(pfrag, valueSize);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfrag"></param>
        /// <param name="valueSize"></param>
        /// <returns></returns>
        private unsafe static bool IsNullValue(byte* pfrag, int valueSize) {
            if (valueSize == 4 && pfrag[0] == 'n') {
                if (pfrag[1] == 'u' && pfrag[2] == 'l' && pfrag[3] == 'l') {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfrag"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private static unsafe int WriteNull(byte* pfrag, int size) {
            *pfrag++ = (byte)'n';
            *pfrag++ = (byte)'u';
            *pfrag++ = (byte)'l';
            *pfrag++ = (byte)'l';
            return 4;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private static byte IntToHex(int n) {
            if (n <= 9)
                return (byte)(n + 48);
            return (byte)((n - 10) + 97);
        }

        private static byte HexToInt(byte h) {
            if (h >= 97)
                return (byte)((h + 10) - 97);
            return (byte)(h - 48);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="innerException"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public static void ThrowWrongValueTypeException(Exception innerException, string name, string type, string value) {
            throw ErrorCode.ToException(
                            Error.SCERRJSONVALUEWRONGTYPE,
                            innerException,
                            string.Format("Property=\"{0} ({1})\", Value=\"{2}\"", name, type, value),
                            (msg, e) => {
                                return new FormatException(msg, e);
                            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        public static void ThrowPropertyNotFoundException(string name) {
            throw ErrorCode.ToException(Error.SCERRJSONPROPERTYNOTFOUND, string.Format("Property=\"{0}\"", name));
        }

        public static void ThrowPropertyNotFoundException(IntPtr ptr, int size) {
            string property = "";
            int valueSize;
            JsonHelper.ParseString(ptr, size, out property, out valueSize);
            ThrowPropertyNotFoundException(property);
        }

        /// <summary>
        /// 
        /// </summary>
        public static void ThrowUnexpectedEndOfContentException() {
            throw ErrorCode.ToException(
                            Error.SCERRJSONUNEXPECTEDENDOFCONTENT,
                            "",
                            (msg, e) => {
                                return new FormatException(msg, e);
                            });
        }

        /// <summary>
        /// 
        /// </summary>
        public static void ThrowInvalidJsonException(string message) {
            throw ErrorCode.ToException(Error.SCERRINVALIDJSONFORINPUT, message);
        }
    }
}
