﻿
using System;
using System.Collections.Generic;
using HttpStructs;
namespace Starcounter {

   /// <summary>
   /// The Starcounter Web Server caches resources as complete http responses.
   /// As the exact same response can often not be used, the cashed response also
   /// include useful offsets and injection points to facilitate fast transitions
   /// to individual http responses. The cached response is also used to cache resources
   /// (compressed or uncompressed content) even if the consumer wants to embedd the content
   /// in a new http response.
   /// </summary>
   public class HttpResponse {

      private byte[] _Uncompressed = null;
      private byte[] _Compressed = null;

      public List<string> Uris = new List<string>();

      public string FilePath;
      public string FileDirectory;
      public string FileName;
      public bool FileExists;
      public DateTime FileModified;

      public HttpResponse(string content) {
          throw new Exception();
      }
      public HttpResponse() {
      }

      /// <summary>
      /// As the session id is a fixed size field, the session id of a cached
      /// response can easily be replaced with a current session id.
      /// </summary>
      /// <remarks>
      /// The offset is only valid in the uncompressed response.
      /// </remarks>
      public int SessionIdOffset { get; set; }

      #region ContentInjection
      /// <summary>
      /// Used for content injection.
      /// Where to insert the View Model assignment into the html document.
      /// </summary>
      /// <remarks>
      /// The injection offset (injection point) is only valid in the uncompressed
      /// response.
      /// 
      /// Insertion is made at one of these points (in order of priority).
      /// ======================================
      /// 1. The point after the <head> tag.
      /// 2. The point after the <!doctype> tag.
      /// 3. The beginning of the html document.
      /// </remarks>
      public int ScriptInjectionPoint { get; set; }

      /// <summary>
      /// Used for content injection.
      /// When injecting content into the response, the content length header
      /// needs to be altered. Used together with the ContentLengthLength property.
      /// </summary>
      public int ContentLengthInjectionPoint { get; set; } // Used for injection

      /// <summary>
      /// Used for content injection.
      /// When injecting content into the response, the content length header
      /// needs to be altered. The existing previous number of bytes used for the text
      /// integer length value starting at ContentLengthInjectionPoint is stored here.
      /// </summary>
      public int ContentLengthLength { get; set; } // Used for injection
      #endregion

      /// <summary>
      /// The number of bytes containing the http header in the uncompressed response. This is also
      /// the offset of the first byte of the content.
      /// </summary>
      public int HeaderLength { get; set; }

      /// <summary>
      /// The number of bytes of the content (i.e. the resource) of the uncompressed http response.
      /// </summary>
      public int ContentLength { get; set; }

      /// <summary>
      /// The uncompressed cached response
      /// </summary>
      public byte[] Uncompressed {
         get {
            return _Uncompressed;
         }
         set {
            _Uncompressed = value;
         }
      }

      public byte[] GetBytes(HttpRequest request) {
          if (request.IsGzipAccepted && Compressed != null)
              return Compressed;
          return Uncompressed;
      }

      /// <summary>
      /// The compressed (gzip) cached resource
      /// </summary>
      public byte[] Compressed {
         get {
            if (!WorthWhileCompressing)
               return _Uncompressed;
            else
               return _Compressed;
         }
         set {
            _Compressed = value;
         }
      }

      private bool _WorthWhileCompressing = true;

      /// <summary>
      /// If false, it was found that the compressed version of the response was
      /// insignificantly smaller, equally large or even larger than the original version.
      /// </summary>
      public bool WorthWhileCompressing {
         get {
            return _WorthWhileCompressing;
         }
         set {
            _WorthWhileCompressing = value;
         }
      }
   }
}