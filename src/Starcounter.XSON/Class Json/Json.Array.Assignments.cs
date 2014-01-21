using Starcounter.Advanced;
using Starcounter.Templates;
using System;
using System.Collections;

namespace Starcounter {

    partial class Json {

        /// <summary>
        /// Initializes this Arr and sets the template and parent if not already done.
        /// If the notEnumeratedResult is not null the list is filled from the sqlresult.
        /// </summary>
        /// <paramCheckpointChangeLogparent"></param>
        /// <param name="template"></param>
        /// <remarks>
        /// This method can be called several times, the initialization only occurs once.
        /// </remarks>
        internal void Array_InitializeAfterImplicitConversion(Json parent, TObjArr template) {
            Json newApp;

            if (Template == null) {
                Template = template;
                Parent = parent;
            }

            if (_PendingEnumeration) {
                var notEnumeratedResult = (IEnumerable)_data;
                foreach (var entity in notEnumeratedResult) {
                    if (entity is Json) {
						Add(entity);
					} else {
                        var tobj = template.ElementType;
                        if (tobj == null) {
                            template.CreateElementTypeFromDataObject(entity);
                            tobj = template.ElementType;
                        }
                        newApp = (Json)tobj.CreateInstance(this);
						Add(newApp);
						newApp.Data = entity;
                    }
                }
                _PendingEnumeration = false;
            }
            parent._CallHasChanged(template);
        }
    }
}
