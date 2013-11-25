
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Starcounter.Internal;
using Starcounter.Templates;

namespace Starcounter.Advanced.XSON {

	public abstract class StandardJsonSerializerBase : TypedJsonSerializer {
		public override int Serialize(Json obj, out byte[] buffer) {
			bool nameWritten;
			bool recreateBuffer;
			byte[] buf;
			byte[] childObjArr;
			int templateNo;
			int posInArray;
			int valueSize;
			int offset;
			List<Template> exposedProperties;
			Json childObj;
			Template tProperty;
			TObject tObj;

			// The following variables are offset for remembering last position when buffer needs to be increased:
			// templateNo: The position in the PropertyList that was about to be written.
			// offset: The last verified position in the buf that was succesfully written.
			// nameWritten: If set to true, the name of the template is succesfully written (but not the value).
			// childObjArr: If last value was an object or an object in an array, the array contains the serialized object.
			// posInArray: Set to the last succesful copied value for an objectarray.

			if (obj.IsArray) {
				throw new NotImplementedException("Serializer does not support arrays as root elements");
			}

			tObj = (TObject)obj.Template;
			buf = new byte[512];
			templateNo = 0;
			nameWritten = false;
			childObjArr = null;
			posInArray = -1;
			recreateBuffer = false;
			valueSize = -1;

			obj.ResumeTransaction(false);

			unsafe {
				buf[0] = (byte)'{';
				offset = 1;

restart:
				if (recreateBuffer)
					buf = IncreaseCapacity(buf, offset, valueSize);
				recreateBuffer = true;

				// Starting from the last written position
				fixed (byte* p = &buf[offset]) {
					byte* pfrag = p;
					exposedProperties = tObj.Properties.ExposedProperties;
					for (int i = templateNo; i < exposedProperties.Count; i++) {
						tProperty = exposedProperties[i];

						// Property name.
						if (!nameWritten) {
							valueSize = JsonHelper.WriteString((IntPtr)pfrag, buf.Length - offset, tProperty.TemplateName);
							if (valueSize == -1 || (buf.Length < (offset + valueSize + 1))) {
								nameWritten = false;
								goto restart;
							}
							nameWritten = true;
							offset += valueSize;
							pfrag += valueSize;

							*pfrag++ = (byte)':';
							offset++;
						}

						// Property value.
						if (tProperty is TObject) {
							if (childObjArr == null) {
								childObj = ((TObject)tProperty).Getter(obj);
								if (childObj != null) {
									valueSize = childObj.ToJsonUtf8(out childObjArr);
								} else {
									valueSize = 2;
									if (buf.Length < (offset + valueSize + 1))
										goto restart;

									pfrag[0] = (byte)'{';
									pfrag[1] = (byte)'}';
								}
							}

							if (valueSize != -1) {
								if (childObjArr != null) {
									if (buf.Length < (offset + valueSize + 1))
										goto restart;
									Buffer.BlockCopy(childObjArr, 0, buf, offset, valueSize);
									childObjArr = null;
								}
								pfrag += valueSize;
								offset += valueSize;
							} else
								goto restart;
						} else if (tProperty is TObjArr) {
							Json arr = ((TObjArr)tProperty).Getter(obj);
							if (buf.Length < (offset + arr.Count * 2 + 2))
								goto restart;

							// We know that we at least have room for all start-end characters in the buf.
							if (posInArray == -1) {
								*pfrag++ = (byte)'[';
								offset++;
								posInArray = 0;
							}
							for (int arrPos = posInArray; arrPos < arr.Count; arrPos++) {
								if (childObjArr == null) {
									valueSize = (arr._GetAt(arrPos) as Json).ToJsonUtf8(out childObjArr);
									if (valueSize == -1)
										goto restart;
									if (buf.Length < (offset + valueSize + 2))
										goto restart;
								}

								Buffer.BlockCopy(childObjArr, 0, buf, offset, valueSize);
								childObjArr = null;
								pfrag += valueSize;
								offset += valueSize;
								posInArray++;

								if ((arrPos + 1) < arr.Count) {
									*pfrag++ = (byte)',';
									offset++;
								}
							}
							*pfrag++ = (byte)']';
							offset++;
							posInArray = -1;
						} else {
							if (tProperty is TBool) {
								valueSize = JsonHelper.WriteBool((IntPtr)pfrag, buf.Length - offset, ((TBool)tProperty).Getter(obj));
							} else if (tProperty is TDecimal) {
								valueSize = JsonHelper.WriteDecimal((IntPtr)pfrag, buf.Length - offset, ((TDecimal)tProperty).Getter(obj));
							} else if (tProperty is TDouble) {
								valueSize = JsonHelper.WriteDouble((IntPtr)pfrag, buf.Length - offset, ((TDouble)tProperty).Getter(obj));
							} else if (tProperty is TLong) {
								valueSize = JsonHelper.WriteInt((IntPtr)pfrag, buf.Length - offset, ((TLong)tProperty).Getter(obj));
							} else if (tProperty is TString) {
								valueSize = JsonHelper.WriteString((IntPtr)pfrag, buf.Length - offset, ((TString)tProperty).Getter(obj));
							} else if (tProperty is TTrigger) {
								valueSize = JsonHelper.WriteNull((IntPtr)pfrag, buf.Length - offset);
							}

							if ((valueSize == -1) || (buf.Length < (offset + valueSize + 1)))
								goto restart;
							pfrag += valueSize;
							offset += valueSize;
						}

						if ((i + 1) < exposedProperties.Count) {
							*pfrag++ = (byte)',';
							offset++;
						}
						templateNo++;
						nameWritten = false;
					}

					//					var jsonObj = obj as Json;

					if (buf.Length < (offset + 1))
						goto restart; // Bummer! we dont have any place left for the last char :(
					*pfrag = (byte)'}';
					offset++;
				}
			}
			buffer = buf;
			return offset;
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

    public class StandardJsonSerializer : StandardJsonSerializerBase {
        public override int Populate(Json obj, IntPtr source, int sourceSize) {
            string propertyName;

            if (obj.IsArray) {
                throw new NotImplementedException("Cannot serialize JSON where the root object is an array");
            }

            var reader = new JsonReader(source, sourceSize);
            Json arr;
            Json childObj;
            TObject tObj = (TObject)obj.Template;
            Template tProperty;

            while (reader.GotoProperty()) {
                propertyName = reader.CurrentPropertyName;
                tProperty = tObj.Properties.GetExposedTemplateByName(propertyName);
                if (tProperty == null) {
                    JsonHelper.ThrowPropertyNotFoundException(propertyName);
                }

                reader.GotoValue();
                try {
                    if (tProperty is TBool) {
                        ((TBool)tProperty).Setter(obj, reader.ReadBool());
                    } else if (tProperty is TDecimal) {
                        ((TDecimal)tProperty).Setter(obj, reader.ReadDecimal());
                    } else if (tProperty is TDouble) {
                        ((TDouble)tProperty).Setter(obj, reader.ReadDouble());
                    } else if (tProperty is TLong) {
                        ((TLong)tProperty).Setter(obj, reader.ReadLong());
                    } else if (tProperty is TString) {
                        ((TString)tProperty).Setter(obj, reader.ReadString());
                    }
                    else if (tProperty is TObject) {
                        childObj = ((TObject)tProperty).Getter(obj);
                        reader.PopulateObject(childObj);
                    } else if (tProperty is TObjArr) {
                        arr = ((TObjArr)tProperty).Getter(obj);
                        while (reader.GotoNextObjectInArray()) {
                            childObj = arr.Add();
                            reader.PopulateObject(childObj);
                        }
                    }
                } catch (InvalidCastException ex) {
                    JsonHelper.ThrowWrongValueTypeException(ex, tProperty.TemplateName, tProperty.JsonType, reader.ReadString());
                }
            }
            return reader.Used;
        }
    }
}