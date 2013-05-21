﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Internal {
    public class Base64Binary {
        public static unsafe uint MeasureNeededSizeToEncode(UInt32 length) {
            return 4 * (length / 3) + ((length % 3 == 0) ? 0 : length % 3 + 1);
        }

        public static unsafe uint MeasureNeededSizeToDecode(UInt32 length) {
            return 3 * (length / 4) + (length % 4 == 0 ? 0 : length % 4 - 1);
        }
        
        public static unsafe uint GetUIntForTriple(Byte* value) {
            uint twin = *(ushort*)value;
            byte last = *(byte*)(value+2);
            uint triple = (twin << 8) | last;
            return triple;
        }

        public static unsafe uint Write(IntPtr buffer, Byte* value, UInt32 length) {
            uint triplesNr = length / 3;
            uint reminder = length % 3;
            uint writtenLength = 0;
            
            byte* toWrite = value;
            for (uint i = 0; i <triplesNr; i++) {
                Base64Int.WriteBase64x4(GetUIntForTriple(value), buffer);
                Debug.Assert(sizeof(Base64x4) == 4);
                writtenLength += 4;
                buffer+= 4;
                toWrite += 3;
            }
            switch (reminder) {
                case 0:
                    Debug.Assert(toWrite == value + length);
                    return writtenLength;
                case 1:
                    Base64Int.WriteBase64x2(*(byte*)value, buffer);
                    writtenLength += 2;
                    toWrite += 1;
                    Debug.Assert(toWrite == value + length);
                    return writtenLength;
                case 2:
                    Base64Int.WriteBase64x3(*(ushort*)value, buffer);
                    writtenLength += 3;
                    toWrite += 2;
                    Debug.Assert(toWrite == value + length);
                    return writtenLength;
            }
            return writtenLength; // Not reached
        }

        public static unsafe uint Read(uint size, IntPtr ptr, byte* value) {
            uint quarNr = size / 4;
            uint reminder = size % 4;
            Debug.Assert(reminder != 1);
            byte* writing = value;
            for (uint i = 0; i < quarNr; i++) {
                uint triple = (uint)Base64Int.ReadBase64x4(ptr);
                ptr += 4;
                Debug.Assert((triple & 0xFF000000) == 0);
                ushort twin = (ushort)(triple >> 4);
                *(ushort*)writing = twin;
                writing += 2;
                byte last = (byte)(triple & 0x000000FF);
                *(byte*)writing = last;
                writing++;
            }
            switch (reminder) {
                case 2:
                    ushort single = (ushort)Base64Int.ReadBase64x2(ptr);
                    Debug.Assert((single & 0xFF00) == 0);
                    *(byte*)writing = (byte)single;
                    writing++;
                    break;
                case 3:
                    ushort twin = (ushort)Base64Int.ReadBase64x3(ptr);
                    *(ushort*)writing = twin;
                    writing += 2;
                    break;
            }
            Debug.Assert(value + MeasureNeededSizeToDecode(size) == writing);
            return (uint)(writing - value);
        }

        public static unsafe byte[] Read(uint size, IntPtr ptr) {
            uint length = MeasureNeededSizeToDecode(size);
            byte[] value = new byte[length];
            fixed (byte* valuePtr = value) {
                Read(size, ptr, valuePtr);
            }
            return value;
        }
    }
}
