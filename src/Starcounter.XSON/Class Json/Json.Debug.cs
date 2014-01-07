// ***********************************************************************
// <copyright file="App.Json.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************


using Starcounter.Templates;
using System;
using System.Text;

namespace Starcounter {
	public partial class Json {
		internal void WriteToDebugString(StringBuilder sb, int indentation) {
			if (Data != null) {
				sb.Append("(data=" + Data.GetType().Name + ")");
			}
			if (_Dirty) {
				sb.Append("(dirty)");
			}
			if (Parent != null) {
				Parent.WriteChildStatus(sb, this.IndexInParent);
			}
			if (IsArray) {
				WriteArrayToDebugString(sb, indentation);
			} else {
				WriteObjectToDebugString(sb, indentation);
			}
		}

		private void WriteArrayToDebugString(StringBuilder sb, int indentation) {

			sb.Append("[");
			indentation += 3;
			int t = 0;
			var values = list;
			foreach (var e in values) {
				if (t > 0) {
					sb.AppendLine(",");
					sb.Append(' ', indentation);
				}
				(e as Json).WriteToDebugString(sb, indentation);
				t++;
			}
			indentation -= 3;
			sb.AppendLine();
			sb.Append(' ', indentation);
			sb.Append("]");
		}
		internal string DebugString {
			get {
				var sb = new StringBuilder();
				WriteToDebugString(sb, 0);
				return sb.ToString();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sb"></param>
		/// <param name="i"></param>
		private void WriteObjectToDebugString(StringBuilder sb, int i) {
			if (this.IsArray) {
				throw new NotImplementedException();
				//                WriteToDebugString(sb, i, (ArrSchema<Json>)Template);
			} else {
				WriteObjectToDebugString(sb, i, (TObject)Template);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sb"></param>
		/// <param name="i"></param>
		/// <param name="template"></param>
		private void WriteObjectToDebugString(StringBuilder sb, int i, TObject template) {

			//_WriteDebugProperty(sb);

			if (Template == null) {
				sb.Append("{}");
				return;
			}

			if (Session != null && Parent != null) {
				var index = Parent.IndexOf(this);
				if (Parent._SetFlag[index]) {
					sb.Append("(set)");
				}
			}


			sb.AppendLine("{");


			i += 3;
			int t = 0;
			var vals = list;
			foreach (var v in vals) {
				if (t > 0) {
					sb.AppendLine(",");
				}
				sb.Append(' ', i);
				//                if (v is Json) {
				//                    (v as Json).WriteToDebugString(sb, i);
				//                }
				//                else {
				var prop = template.Properties[t];
				sb.Append('"');
				sb.Append(prop.PropertyName);
				sb.Append("\":");
				if (WasReplacedAt(t)) {
					//   if (prop is TContainer) {
					//       // The "d" is already anotated for Containers.
					//       // Let's just make sure that the dirty flag was the same
					//       var obj = (Json)(this as Json).Get((TValue)prop);
					//       if (!obj._Dirty) {
					//           throw new Exception("Missmach in dirty flags");
					//       }
					//   }
					//   else {
					sb.Append("(direct-set)");
					//   }
				}
				if (v is Json) {
					(v as Json).WriteToDebugString(sb, i);
				} else {
					WriteChildStatus(sb, t);
					sb.Append(((TValue)prop).ValueToJsonString(this));

				}//}
				t++;
			}
			i -= 3;
			sb.AppendLine();
			sb.Append(' ', i);
			sb.Append("}");
		}

		private void WriteChildStatus(StringBuilder sb, int index) {
			Template template;
			if (IsArray) {
				template = (Template as TObjArr).ElementType;
			} else {
				template = (Template as TObject).Properties[index];
			}
			if (template is TValue && Data != null) {
				if (((TValue)template).Bind != null) {
					string binding = ((TValue)template).Bind;
					if (binding != ((TValue)template).PropertyName) {
						sb.Append("(bound path=" + ((TValue)template).Bind + ")");
					} else {
						sb.Append("(bound)");
					}
				}
				
				var tv = (TValue)template;
				var dbgVal = tv.GetUnboundValueAsObject(this);
				if (tv.GetValueAsObject(this) != dbgVal) {
					if (dbgVal == null)
						dbgVal = "notsent";
					sb.Append("(indirect-set old=" + dbgVal + ")");
				}
			}
		}
	}
}