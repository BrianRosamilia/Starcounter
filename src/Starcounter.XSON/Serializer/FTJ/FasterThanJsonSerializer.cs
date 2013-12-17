using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Starcounter.Internal;
using Starcounter.Templates;

namespace Starcounter.Advanced.XSON {
	public class FasterThanJsonSerializer : TypedJsonSerializer {
		private const int MAX_INT_SIZE = 11;

		// Only used to get rid of used before initialization warning
		private static TupleWriterBase64 nullWriter = new TupleWriterBase64();

		public override int Serialize(Json obj, out byte[] buffer) {
			bool recreateBuffer;
			byte[] buf;
			byte[] childObjArr;
			int templateNo;
			int posInArray;
			int valueSize;
			List<Template> exposedProperties;
			Json childObj;
			Template tProperty;
			TObject tObj;
			TupleWriterBase64 writer;
			TupleWriterBase64 arrWriter = nullWriter;
			TupleWriterBase64 itemWriter = nullWriter;

			// The following variables are offset for remembering last position when buffer needs to be increased:
			// templateNo: The position in the PropertyList that was about to be written.
			// offset: The last verified position in the buf that was succesfully written.
			// childObjArr: If last value was an object or an object in an array, the array contains the serialized object.
			// posInArray: Set to the last succesful copied value for an objectarray.

			if (obj.IsArray) {
				throw new NotImplementedException("Serializer does not support arrays as root elements");
			}

			tObj = (TObject)obj.Template;
			buf = new byte[512];
			templateNo = 0;
			childObjArr = null;
			posInArray = -1;
			recreateBuffer = false;
			valueSize = -1;
			writer = new TupleWriterBase64();

			exposedProperties = tObj.Properties.ExposedProperties;
			int valueCount = exposedProperties.Count;
			int offset = 0;

			obj.ResumeTransaction(true);

			unsafe {
restart:
				if (recreateBuffer) {
					offset = writer.Length;
					buf = IncreaseCapacity(buf, offset, valueSize);
				}
				
				// Starting from the last written position
				fixed (byte* p = &buf[0]) {
					writer = new TupleWriterBase64(p, (uint)valueCount);

					if (recreateBuffer) {
						int initLen = writer.Length;
						writer.HaveWritten(offset - initLen);
					}
					recreateBuffer = true;

					for (int i = templateNo; i < exposedProperties.Count; i++) {
						tProperty = exposedProperties[i];

						if (tProperty is TObject) {
							if (childObjArr == null) {
								childObj = ((TObject)tProperty).Getter(obj);
								valueSize = ((TContainer)childObj.Template).ToFasterThanJson(childObj, out childObjArr);
							}
							if (valueSize != -1) {
								if (childObjArr != null) {
									if (valueSize > (buf.Length - writer.Length))
										goto restart;

									Marshal.Copy(childObjArr, 0, (IntPtr)writer.AtEnd, valueSize);
									writer.HaveWritten(valueSize);
									childObjArr = null;
								}
							} else
								goto restart;
						} else if (tProperty is TObjArr) {
							Json arr = ((TObjArr)tProperty).Getter(obj);
							if (posInArray == -1) {
								if (MAX_INT_SIZE > (buf.Length - writer.Length))
									goto restart;

								arrWriter = new TupleWriterBase64(writer.AtEnd, 2);
								arrWriter.WriteULong((ulong)arr.Count);

								itemWriter = new TupleWriterBase64(arrWriter.AtEnd, (uint)arr.Count);

								posInArray = 0;
							}

							for (int arrPos = posInArray; arrPos < arr.Count; arrPos++) {
								if (childObjArr == null) {
									var arrItem = (Json)arr._GetAt(arrPos);
									valueSize = ((TContainer)arrItem.Template).ToFasterThanJson(arrItem, out childObjArr);
									if (valueSize == -1)
										goto restart;

									if (valueSize > (buf.Length - itemWriter.Length))
										goto restart;
								}

								Marshal.Copy(childObjArr, 0, (IntPtr)itemWriter.AtEnd, valueSize);
								itemWriter.HaveWritten(valueSize);
								childObjArr = null;
								posInArray++;
							}
							arrWriter.HaveWritten(itemWriter.SealTuple());
							writer.HaveWritten(arrWriter.SealTuple());
							posInArray = -1;
						} else {
							string valueAsStr;
							
							if (tProperty is TBool) {
								if (buf.Length < (writer.Length + 1))
									goto restart;

								bool b = ((TBool)tProperty).Getter(obj);
								if (b) writer.WriteULong(1);
								else writer.WriteULong(0);
							} else if (tProperty is TDecimal) {
								valueAsStr = ((TDecimal)tProperty).Getter(obj).ToString("0.0###########################", CultureInfo.InvariantCulture);
								valueSize = valueAsStr.Length;
								if (valueSize > (buf.Length - writer.Length))
									goto restart;
								writer.WriteString(valueAsStr);
							} else if (tProperty is TDouble) {
								valueAsStr = ((TDouble)tProperty).Getter(obj).ToString("0.0###########################", CultureInfo.InvariantCulture);
								valueSize = valueAsStr.Length;
								if (valueSize > (buf.Length - writer.Length))
									goto restart;
								writer.WriteString(valueAsStr);
							} else if (tProperty is TLong) {
								valueSize = MAX_INT_SIZE;
								if (valueSize > (buf.Length - writer.Length))
									goto restart;
								writer.WriteLong(((TLong)tProperty).Getter(obj));
							} else if (tProperty is TString) {
								valueAsStr = ((TString)tProperty).Getter(obj);
								if (valueAsStr == null)
									throw new NotImplementedException("null values are not yet supported");
								valueSize = valueAsStr.Length;
								if (valueSize > (buf.Length - writer.Length))
									goto restart;
								writer.WriteString(valueAsStr);
							} else if (tProperty is TTrigger) {
								throw new NotImplementedException("null values are not yet supported");
//								valueSize = JsonHelper.WriteNull((IntPtr)pfrag, buf.Length - offset);
							}

						}
						templateNo++;
					}
					offset = (int)writer.SealTuple();
				}
			}

			buffer = buf;
			return offset;
		}

		public override int Populate(Json obj, IntPtr src, int srcSize) {
			if (obj.IsArray) {
				throw new NotImplementedException("Cannot serialize JSON where the root object is an array");
			}

			unsafe {
				List<Template> exposedProperties = ((TObject)obj.Template).Properties.ExposedProperties;
				int valueCount = exposedProperties.Count;
				var reader = new TupleReaderBase64((byte*)src, (uint)valueCount);

				Json arr;
				Json childObj;
				TObject tObj = (TObject)obj.Template;
				Template tProperty;
				String valueAsStr;

				for (int i = 0; i < exposedProperties.Count; i++){
					tProperty = exposedProperties[i];

					try {
						if (tProperty is TBool) {
							ulong v = reader.ReadULong();
							if (v == 1) ((TBool)tProperty).Setter(obj, true);
							else ((TBool)tProperty).Setter(obj, false);
						} else if (tProperty is TDecimal) {
							valueAsStr = reader.ReadString();
							((TDecimal)tProperty).Setter(obj, Decimal.Parse(valueAsStr, CultureInfo.InvariantCulture));
						} else if (tProperty is TDouble) {
							valueAsStr = reader.ReadString();
							((TDouble)tProperty).Setter(obj, Double.Parse(valueAsStr, CultureInfo.InvariantCulture));
						} else if (tProperty is TLong) {
							((TLong)tProperty).Setter(obj, reader.ReadLong());
						} else if (tProperty is TString) {
							((TString)tProperty).Setter(obj, reader.ReadString());
						} else if (tProperty is TObject) {
							childObj = ((TObject)tProperty).Getter(obj);
							((TContainer)childObj.Template).PopulateFromFasterThanJson(childObj, (IntPtr)reader.AtEnd, 0);
							reader.Skip();
						} else if (tProperty is TObjArr) {
							arr = ((TObjArr)tProperty).Getter(obj);

							var arrReader = new TupleReaderBase64(reader.AtEnd, 2);
							int arrItemCount = (int)arrReader.ReadULong();

							var itemReader = new TupleReaderBase64(arrReader.AtEnd, (uint)arrItemCount);
							

							for (int aic = 0; aic < arrItemCount; aic++) {
								childObj = arr.Add();
								((TContainer)childObj.Template).PopulateFromFasterThanJson(childObj, (IntPtr)itemReader.AtEnd, 0);
								itemReader.Skip();
							}
							arrReader.Skip();
							reader.Skip();
						}
					} catch (InvalidCastException ex) {
						JsonHelper.ThrowWrongValueTypeException(ex, tProperty.TemplateName, tProperty.JsonType, reader.ReadString());
					}
				}
				return (int)reader.ReadByteCount;
			}
		}

		protected static byte[] IncreaseCapacity(byte[] current, int offset, int needed) {
			byte[] tmpBuffer;
			long bufferSize = current.Length;

			bufferSize *= 2;
			if (needed != -1) {
				while (bufferSize < (offset + needed))
					bufferSize *= 2;
			}
			//            System.Diagnostics.Debug.WriteLine("Increasing buffer, new size: " + bufferSize);
			tmpBuffer = new byte[bufferSize];
			Buffer.BlockCopy(current, 0, tmpBuffer, 0, offset);
			return tmpBuffer;
		}
	}
}
