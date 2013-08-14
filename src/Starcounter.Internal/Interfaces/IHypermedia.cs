﻿
using System;
namespace Starcounter {

    /// <summary>
    /// Implemented by resources to allow them to be converted into a
    /// binary representations. Used when responding to REST requests such as
    /// HTTP requests.
    /// </summary>
    /// <remarks>
    /// Text types are represented using an UTF-8 encoding.
    /// </remarks>
    public interface IHypermedia {

        /// <summary>
        /// Returns a binary representation of the resource using the specified MIME type.
        /// </summary>
        /// <remarks>
        /// Throws a System.NotSupportedException if the MIME type is not supported.
        /// </remarks>
        /// <param name="type">A common mime type. If the desired mime type is not available,
        ///  the method AsMimeType( string mimeType ) should be used.</param>
        /// <returns></returns>
        byte[] AsMimeType(MimeType mimeType, out MimeType resultingMimeType);

        /// <summary>
        /// Returns a binary representation of the resource using the specified MIME type.
        /// </summary>
        /// <remarks>
        /// Throws a System.NotSupportedException if the MIME type is not supported.
        /// </remarks>
        /// <param name="type">The mime type. If the desired mime type is available in the
        /// MimeType enumeration, the method AsMimeType( MimeType mimeType ) should be used instead.</param>
        /// <returns></returns>
        byte[] AsMimeType(string mimeType, out MimeType resultingMimeType );
    }

    /// <summary>
    /// Common mime types
    /// </summary>
    public enum MimeType {
        Unspecified = 0,
        Other = 1,
        Text_Plain = 2,
        Text_Html = 3,
        Application_Json = 4,
        Application_JsonPatch__Json = 5
    }

    /// <summary>
    /// Mime type related functions
    /// </summary>
    public class MimeTypeHelper {
        /// <summary>
        /// </summary>
        /// <param name="mimeType">The mime type constant to convert to a string</param>
        /// <returns>The standard mime type text</returns>
        public static string MimeTypeAsString(MimeType mimeType) {
            switch (mimeType) {
                case MimeType.Unspecified:
                    return "*/*";
                case MimeType.Text_Plain:
                    return "text/plain";
                case MimeType.Text_Html:
                    return "text/html";
                case MimeType.Application_Json:
                    return "application/json";
                case MimeType.Application_JsonPatch__Json:
                    return "application/json-patch+json";
                case MimeType.Other:
                    break;
            }
            throw new Exception(String.Format("Cannot convert mime type constant {0} to text", mimeType));
        }
    }
}