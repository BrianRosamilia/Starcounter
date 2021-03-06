﻿// ***********************************************************************
// <copyright file="DbHelper.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Advanced;
using Starcounter.Binding;
using Starcounter.Internal;
using System;
using System.Diagnostics;
using System.Text;
using System.Web;

namespace Starcounter {

    /// <summary>
    /// Class DbHelper
    /// </summary>
    public static class DbHelper {

        /// <summary>
        /// Strings the compare.
        /// </summary>
        /// <param name="str1">The STR1.</param>
        /// <param name="str2">The STR2.</param>
        /// <returns>System.Int32.</returns>
        /// <exception cref="System.ArgumentNullException">str1</exception>
        public static int StringCompare(string str1, string str2) {
            // TODO: Implement efficient string comparison.
            UInt32 ec;
            Int32 result;
            if (str1 == null) {
                throw new ArgumentNullException("str1");
            }
            if (str2 == null) {
                throw new ArgumentNullException("str2");
            }
            ec = sccoredb.SCCompareUTF16Strings(str1, str2, out result);
            if (ec == 0) {
                return result;
            }
            throw ErrorCode.ToException(ec);
        }

        /// <summary>
        /// Froms the ID.
        /// </summary>
        /// <param name="oid">The oid.</param>
        /// <returns>Entity.</returns>
        public static IObjectView FromID(ulong oid) {
            Boolean br;
            UInt16 codeClassIdx;
            ulong addr;
            unsafe {
                br = sccoredb.Mdb_OIDToETIEx(oid, &addr, &codeClassIdx);
            }
            if (br) {
                if (addr != sccoredb.INVALID_RECORD_ADDR) {
                    return Bindings.GetTypeBinding(codeClassIdx).NewInstance(addr, oid);
                }
                return null;
            }
            throw ErrorCode.ToException(sccoredb.star_get_last_error());
        }

        /// <summary>
        /// Returns the object identifier of the specified object.
        /// </summary>
        /// <remarks>
        /// Note that all this method does is to read the object identifier from
        /// the proxy. It doesn't check if the proxy is valid in any way, the
        /// underlying object may for example be have been deleted in which
        /// case the method returns the identifier the object had.
        /// </remarks>
        /// <param name="obj">The object to get the identity from.</param>
        /// <exception cref="ArgumentNullException">
        /// Raised if <paramref name="obj"/> is null.</exception>
        /// <exception cref="InvalidCastException">Raise if Starcounter don't
        /// recognize the type of <paramref name="obj"/> as a type it knows
        /// how to get a database identity from.</exception>
        /// <returns>
        /// The unique object identity of the given object.
        /// </returns>
        public static ulong GetObjectNo(this object obj) {
            var bindable = obj as IBindable;
            if (bindable == null) {
                if (obj == null) {
                    throw new ArgumentNullException("obj");
                } else {
                    throw ErrorCode.ToException(
                        Error.SCERRCODENOTENHANCED,
                        string.Format("Don't know how to get the identity from objects with type {0}.", obj.GetType()),
                        (msg, inner) => { return new InvalidCastException(msg, inner); });
                }
            }

            return bindable.Identity;
        }

        /// <summary>
        /// Returns web friendly string of object identity.
        /// </summary>
        /// <param name="obj">The object to get the identity from.</param>
        /// <returns>The string</returns>
        public static string GetObjectID(this object obj) {
            var bindable = obj as IBindable;
            if (bindable == null) {
                if (obj == null) {
                    throw new ArgumentNullException("obj");
                } else {
                    throw ErrorCode.ToException(
                        Error.SCERRCODENOTENHANCED,
                        string.Format("Don't know how to get the identity from objects with type {0}.", obj.GetType()),
                        (msg, inner) => { return new InvalidCastException(msg, inner); });
                }
            }

            return Base64EncodeObjectNo(bindable.Identity);
        }

        /// <summary>
        /// Returns the object identifier of the given <see cref="IBindable"/>
        /// instance.
        /// </summary>
        /// <param name="obj">The object to get the identity from.</param>
        /// <exception cref="ArgumentNullException">
        /// Raised if <paramref name="obj"/> is null.</exception>
        /// <returns>
        /// The unique object identity of the given object.
        /// </returns>
        public static ulong GetObjectNo(this IBindable obj) {
            if (obj == null) {
                throw new ArgumentNullException("obj");
            }
            return obj.Identity;
        }

        /// <summary>
        /// Returns web friendly string of the object identity of the given 
        /// <see cref="IBindable"/> instance.
        /// </summary>
        /// <param name="obj">The object to get the identity from.</param>
        /// <returns>The string</returns>
        public static string GetObjectID(this IBindable obj) {
            if (obj == null) {
                throw new ArgumentNullException("obj");
            }
            return Base64EncodeObjectNo(obj.Identity);
        }

        internal const string ObjectNoName = "ObjectNo";
        internal static Type ObjectNoType = typeof(UInt64);
        internal const string ObjectIDName = "ObjectID";
        internal static Type ObjectIDType = typeof(String);

        ///<summary>
        /// Base 64 Encoding with URL and Filename Safe Alphabet using UTF-8 character set.
        ///</summary>
        ///<param name="objectNo">The original string</param>
        ///<returns>The Base64 encoded string</returns>
        public static string Base64ForUrlEncode(UInt64 objectNo) {
            byte[] encbuff = Encoding.UTF8.GetBytes(objectNo.ToString());
            return HttpServerUtility.UrlTokenEncode(encbuff);
        }
        ///<summary>
        /// Decode Base64 encoded string with URL and Filename Safe Alphabet using UTF-8.
        ///</summary>
        ///<param name="objectID">Base64 code</param>
        ///<returns>The decoded string.</returns>
        public static UInt64 Base64ForUrlDecode(string objectID) {
            byte[] decbuff = HttpServerUtility.UrlTokenDecode(objectID);
            return Convert.ToUInt64(Encoding.UTF8.GetString(decbuff));
        }

        public unsafe static string Base64EncodeObjectNo(UInt64 objectNo) {
#if true
            byte[] buffer = new byte[11];
            int len;
            fixed (byte* start = buffer)
                len = Base64Int.Write(start, objectNo);
            String objID = Encoding.UTF8.GetString(buffer, 0, len);
            Debug.Assert(objID.Length == len);
#else
            byte* buffer = stackalloc byte[11];
            int len = Base64Int.Write(buffer, objectNo);
            String objID = new String((sbyte*)buffer, 0, len);
            Debug.Assert(objID.Length == len);
#endif
            return objID;
        }

        public unsafe static UInt64 Base64DecodeObjectID(String objectID) {
            if (!Base64Int.IsValidLength(objectID.Length))
                throw ErrorCode.ToException(Error.SCERRBADARGUMENTS, "Encoded string of ObjectID is of invalid size");
            byte[] buffer = Encoding.UTF8.GetBytes(objectID);
            fixed (byte* ptr = buffer) {
                UInt64 objNo = Base64Int.Read(objectID.Length, (byte*)ptr);
                return objNo;
            }
        }

        #region Extending FasterThanJson with writing and reading methods on Binary
        /// <summary>
        /// Adds given binary as byte array to the tuple.
        /// </summary>
        /// <param name="tuple">The tuple</param>
        /// <param name="value">The value</param>
        public static void WriteBinary(ref TupleWriterBase64 tuple, Binary value) {
            value.WriteToTuple(ref tuple);
        }

        /// <summary>
        /// Adds given binary as byte array to the tuple.
        /// </summary>
        /// <param name="tuple">The tuple</param>
        /// <param name="value">The value</param>
        public static void WriteBinary(ref SafeTupleWriterBase64 tuple, Binary value) {
            value.WriteToTuple(ref tuple);
        }

        /// <summary>
        /// Reads next byte array value in the tuple into new binary.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <returns>New binary</returns>
        public static Binary ReadBinary(ref TupleReaderBase64 tuple) {
            return new Binary(ref tuple);
        }

        /// <summary>
        /// Reads byte array value at the given position in the tuple into new binary.
        /// </summary>
        /// <param name="tuple">The tuple</param>
        /// <param name="index">The position</param>
        /// <returns>New binary</returns>
        public static Binary ReadBinary(ref SafeTupleReaderBase64 tuple, int index) {
            return new Binary(ref tuple, index);
        }

        /// <summary>
        /// Adds given binary as byte array to the tuple.
        /// </summary>
        /// <param name="tuple">The tuple</param>
        /// <param name="value">The value</param>
        public static void WriteLargeBinary(ref TupleWriterBase64 tuple, LargeBinary value) {
            value.WriteToTuple(ref tuple);
        }

        /// <summary>
        /// Adds given binary as byte array to the tuple.
        /// </summary>
        /// <param name="tuple">The tuple</param>
        /// <param name="value">The value</param>
        public static void WriteLargeBinary(ref SafeTupleWriterBase64 tuple, LargeBinary value) {
            value.WriteToTuple(ref tuple);
        }

        /// <summary>
        /// Reads next byte array value in the tuple into new binary.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <returns>New binary</returns>
        public static LargeBinary ReadLargeBinary(ref TupleReaderBase64 tuple) {
            return new LargeBinary(ref tuple);
        }

        /// <summary>
        /// Reads byte array value at the given position in the tuple into new binary.
        /// </summary>
        /// <param name="tuple">The tuple</param>
        /// <param name="index">The position</param>
        /// <returns>New binary</returns>
        public static LargeBinary ReadLargeBinary(ref SafeTupleReaderBase64 tuple, int index) {
            return new LargeBinary(ref tuple, index);
        }
        #endregion
    }
}