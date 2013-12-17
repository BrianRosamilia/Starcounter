// ***********************************************************************
// <copyright file="HttpPatchBuilder.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Starcounter.Internal;
using Starcounter.Templates;
using Starcounter.Internal.XSON;

namespace Starcounter.Internal.JsonPatch {
	/// <summary>
	/// Class HttpPatchBuilder
	/// </summary>
	internal class HttpPatchBuilder {
		/// <summary>
		/// Creates the content from change log.
		/// </summary>
		/// <param name="changeLog">The change log.</param>
		/// <param name="buffer">The buffer.</param>
		/// <returns>Int32.</returns>
		public static Int32 CreateContentFromChangeLog(Session changeLog, List<Byte> buffer) {
			// TODO: 
			// Change so that we can send in a buffer into the function that created 
			// the patch instead of creating a string and then convert it to a byte array
			// and then copy it to the response buffer...
			Int32 startIndex;
			String patch;
			Template template;
//			Object obj;

			// if (changeLog.Count == 0)
			// {
			//     return 0;
			// }

			startIndex = buffer.Count;
			buffer.Add((byte)'[');
			foreach (Change change in changeLog) {
				template = change.Property;

				//if (change.ChangeType != Change.REMOVE) {
				//	obj = GetValueFromChange(change);
				//} else {
				//	obj = null;
				//}

				patch = JsonPatch.BuildJsonPatch(change.ChangeType, change.Obj, change.Property, change.Index);
				Byte[] patchArr = Encoding.UTF8.GetBytes(patch);
				buffer.AddRange(patchArr);

				buffer.Add((byte)',');
				buffer.Add((byte)'\n');
			}

			// Remove the ',' and new-line chars.
			if (changeLog.Count > 0) {
				buffer.RemoveAt(buffer.Count - 1);
				buffer.RemoveAt(buffer.Count - 1);
			}
			buffer.Add((byte)']');

			return buffer.Count - startIndex;
		}
	}
}

