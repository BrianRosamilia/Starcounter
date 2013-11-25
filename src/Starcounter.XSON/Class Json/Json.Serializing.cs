// ***********************************************************************
// <copyright file="App.Json.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using Starcounter.Internal;
using Starcounter.Templates;
using System.Text;

namespace Starcounter {
    /// <summary>
    /// Class App
    /// </summary>
    public partial class Json {
		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public string ToJson() {
			if (Template == null) {
				return "{}";
			}

            if (IsArray) {
                byte[] buf;
                int size = ToJsonUtf8(out buf);
                return Encoding.UTF8.GetString(buf, 0, size);
            }
            else {
                var template = Template as TContainer;
                if (template == null)
                    throw new NotImplementedException("Cannot currently serialize JSON for single value JSON");
                return template.ToJson(this);
            }
		}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] ToJsonUtf8() {
			if (Template == null) {
				var buffer = new byte[2];
				buffer[0] = (byte)'{';
				buffer[1] = (byte)'}';
                return buffer;
			}
            if (IsArray) {
                byte[] buf;
                int size = ToJsonUtf8(out buf);

                byte[] ret = new byte[size];
                Buffer.BlockCopy(buf, 0, ret, 0, size);
                return ret;
            }
            else {

                var template = Template as TContainer;
                if (template == null)
                    throw new NotImplementedException("Cannot currently serialize JSON for single value JSON");
                return template.ToJsonUtf8(this);
            }
        }

        /// <summary>
        /// Serializes this object and sets the out parameter to the buffer containing 
        /// the UTF8 encoded characters. Returns the size used in the buffer.
        /// </summary>
        /// <remarks>
        /// The actual returned buffer might be larger than the amount used.
        /// </remarks>
        /// <param name="buf"></param>
        /// <returns></returns>
        public int ToJsonUtf8(out byte[] buffer) {
            if (Template == null) {
                buffer = new byte[2];
				buffer[0] = (byte)'{';
				buffer[1] = (byte)'}';
                return 2;
            }
            if (IsArray) {
                bool expandBuffer = false;
                byte[] itemJson = null;
                int size = 512;
                byte[] buf = new byte[size];
                int itemSize = 0;
                int offset = 0;
                int lastArrayPos = 0;

                buf[offset++] = (byte)'[';

            restart:
                if (expandBuffer) {
                    while (size < (offset + itemSize))
                        size *= 2;
                    byte[] buf2 = new byte[size];
                    Buffer.BlockCopy(buf, 0, buf2, 0, offset);
                    buf = buf2;
                }
                expandBuffer = true;

                for (int i = lastArrayPos; i < Count; i++) {
                    if (itemJson == null) {
                        itemSize = (this._GetAt(i) as Json).ToJsonUtf8(out itemJson);
                        if ((buf.Length - offset - 2) < itemSize)
                            goto restart;
                    }
                    Buffer.BlockCopy(itemJson, 0, buf, offset, itemSize);
                    itemJson = null;
                    offset += itemSize;
                    lastArrayPos++;
                    if ((i + 1) < Count)
                        buf[offset++] = (byte)',';
                }
                buf[offset++] = (byte)']';

                buffer = buf;
                return offset;
            }
            else {
                if (!(Template is TContainer)) {
                    throw new NotImplementedException("Cannot currently serialize JSON for single value JSON");
                }
                var template = Template as TContainer;
                return template.ToJsonUtf8(this, out buffer);
            }
        }

		/// <summary>
		/// 
		/// </summary>
        internal void CreateDynamicTemplate() {
            var t = new TObject();
            t.IsDynamic = true;
            Template = t; // IMPORTANT! It is important that the dynamic flag is set _before_ it is assigned to the Template property.
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="jsonSize"></param>
        /// <returns></returns>
        public int PopulateFromJson(IntPtr buffer, int jsonSize) {
            if (Template == null) {
                CreateDynamicTemplate();
            }
            if (jsonSize == 0) return 0;
			var template = Template as TContainer;
			return template.PopulateFromJson(this, buffer, jsonSize);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public int PopulateFromJson(byte[] buffer, int bufferSize) {
			if (Template == null) {
				CreateDynamicTemplate();
			}
			var template = Template as TContainer;
			return template.PopulateFromJson(this, buffer, bufferSize);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="json"></param>
        public void PopulateFromJson(string json) {
			if (Template == null) {
				CreateDynamicTemplate();
			}
			if (string.IsNullOrEmpty(json))
				return;
			var template = Template as TContainer;
			template.PopulateFromJson(this, json);
        }		
    }
}
