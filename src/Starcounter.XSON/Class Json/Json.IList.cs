// ***********************************************************************
// <copyright file="PropertyList.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;

using Starcounter;
using System.Collections;
using Starcounter.Templates;

namespace Starcounter {

    partial class Json : IList {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(Json item) {
            return list.IndexOf(item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, Json item) {
            this.Insert(index, (object)item);
        }

        public bool IsFixedSize {
            get {
                return false;
            }
        }

        public object SyncRoot {
            get {
                return null;
            }
        }

        public bool IsSynchronized {
            get {
                return false;
            }
        }

        internal object _GetAt(int index) {
            return list[index];
        }

        internal void _SetAt(int index, object value) {
            list[index] = value;
        }

        /// <summary>
        /// If true, this object has been flushed from the change log (usually an
        /// indication that the object has been sent to its client.
        /// </summary>
        public bool HasBeenSent {
            get {
				if (Parent != null) {
					return ((IndexInParent != -1) && (!Parent.WasReplacedAt(IndexInParent)));
				}
				else {
                    var s = Session;
                    if (s == null) {
                        return false;
                    }
                    return !s.BrandNew;
                }
            }
        }

        private bool IsChanged(Template template) {
            if (IsArray) {
                throw new Exception("You can only call IsChanged on Json objects, not on Json Arrays");
            }
#if DEBUG
            this.Template.VerifyProperty(template);
#endif
            return this.WasReplacedAt(template.TemplateIndex);
        }

        /// <summary>
        /// Use this property to access the values internally
        /// </summary>
        protected IList list {
            get {
                if (_list == null) {
                    return null;
                }
                if (IsArray) {
                    return _list;
                }
                else {
                    int childIndex;
                    var template = (TObject)Template;
                    while (_list.Count < template.Properties.Count) {
                        // We allow adding new properties to dynamic templates
                        // even after instances have been created.
                        // For this reason, we need to allow the expansion of the 
                        // values.
                        _SetFlag.Add(false);
                        childIndex = _list.Count;
                        _list.Add(null);
                        ((TValue)template.Properties[childIndex]).SetDefaultValue(this);
                    }
                    return _list;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vc"></param>
        internal void InitializeCache() {

            if (IsArray) {
                _list = new List<Json>();
                _SetFlag = new List<bool>();
            }
            else {
                var template = (TObject)Template;
                var prop = template.Properties;
                var vc = prop.Count;
                _list = new List<object>(vc);
                _SetFlag = new List<bool>(vc);
                _Dirty = false;
                for (int t = 0; t < vc; t++) {
					_list.Add(null);
					_SetFlag.Add(false);
					((TValue)prop[t]).SetDefaultValue(this);
                }
            }
        }

        public bool WasReplacedAt(int index) {
            return _SetFlag[index];
        }

        public void CheckpointAt(int index) {
            _SetFlag[index] = false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="template"></param>
        internal void MarkAsReplaced(Templates.Template template) {
            this.MarkAsReplaced(template.TemplateIndex);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        internal void MarkAsReplaced(int index) {
            _SetFlag[index] = true;
            var v = list[index];
           // if (v is Json) {
           //     (v as Json).Dirtyfy();
           // }
           // else {
                this.Dirtyfy();
           // }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        object IList.this[int index] {
            get {
                return list[index];
            }
            set {
                list[index] = value;
            }
        }

        /// <summary>
        /// Copies all items to the specified array.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The start index in the source.</param>
        public void CopyTo(Array array, int arrayIndex) {
            list.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <value>The count.</value>
        /// <returns>The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.</returns>
        public int Count {
            get { return list.Count; }
        }

        /// <summary>
        /// Gets the index in the list of the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.Int32.</returns>
        public int IndexOf(object item) {
            return list.IndexOf((object)item);
        }

        /// <summary>
        /// Inserts the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Insert(int index, object item) {
            Json j = VerifyJsonForInserting(item);

            Json otherItem;
            for (Int32 i = index + 1; i < list.Count; i++) {
                otherItem = (Json)list[i];
                otherItem._cacheIndexInArr = i;
            }

            list.Insert(index, j);
            _SetFlag.Insert(index, false);
            MarkAsReplaced(index);
            (this as Json)._CallHasAddedElement(index,j);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private Json VerifyJsonForInserting(object item) {
            if (!IsArray) {
                throw new Exception("You are not allowed to insert/add elements to an Object. Use an array instead");
            }
            if (item == null) {
                throw new Exception("Type object arrays cannot contain null elements");
            }
            if (!(item is Json) ) {
                throw new Exception("You are only allowed to insert/add elements of type Json to a type Json array");
            }
            if ((item as Json).IsArray) {
                throw new Exception("Nested arrays are not currently supported in typed Json");
            }
            return (Json)item;
        }

        /// <summary>
        /// Removed the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void RemoveAt(int index) {
            list.RemoveAt(index);
            _SetFlag.RemoveAt(index);

            if (IsArray) {
                Json otherItem;
                var tarr = (TObjArr)this.Template;
                (this as Json)._CallHasRemovedElement(index);
                for (Int32 i = index; i < list.Count; i++) {
                    otherItem = (Json)_list[i];
                    otherItem._cacheIndexInArr = i;
                }
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int Add(object item) {
            return _Add(item);
        }

        /// <summary>
        /// Adds the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="System.Exception">This template is already used by an App. Cannot add more properties.</exception>
        internal int _Add(object item) {

            var j = VerifyJsonForInserting(item);

            var typedListTemplate = ((TObjArr)Template).ElementType;
            if (typedListTemplate != null) {
                if (j.Template != typedListTemplate) {
                    throw new Exception(
                        String.Format("Cannot add item with template {0} as the array is expecting another template of type {1}",
                                j.Template.DebugString,
                                typedListTemplate.DebugString));
                }
            }
            _SetFlag.Add(true);
//            MarkAsReplaced( this.IndexOf(item));

            var index = list.Add(j);
            Dirtyfy();
            j._cacheIndexInArr = index;
            j.Parent = this;

            
            (this as Json)._CallHasAddedElement(list.Count - 1, j);
            return index;
        }

        /// <summary>
        /// Clears this instance.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Clear() {
            list.Clear();
            (Parent as Json).MarkAsReplaced(Template);
            //this._BrandNew = true;
            _SetFlag.Clear();


            if (IsArray) {
                (this as Json).InternalClear();
                Json parent = (Json)this.Parent;
                parent._CallHasChanged((TContainer)this.Template);
            }
        }

        /// <summary>
        /// Checks if the specified item exists in the list.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if [contains] [the specified item]; otherwise, <c>false</c>.</returns>
        public bool Contains(object item) {
            return list.Contains((object)item);
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
        /// </summary>
        /// <value><c>true</c> if this instance is read only; otherwise, <c>false</c>.</value>
        /// <returns>true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.</returns>
        public bool IsReadOnly {
            get { return false; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public void Remove(object item) {
            var i = IndexOf(item);
            if (i == -1)
                return;
            this.RemoveAt(i);
            return;
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
        public IEnumerator GetEnumerator() {
            return list.GetEnumerator();
        }


        public List<Json> GetJsonArray() {
            return (List<Json>)list;
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(Json item) {
            Boolean b;
            Int32 index;

#if QUICKTUPLE
            index = list.IndexOf(item);
            b = (index != -1);
            if (b)
                RemoveAt(index);
            return b;
#else
         throw new NotImplementedException();
#endif
        }

        /// <summary>
        /// Return the position of this Json object or array within its parent
        /// object or array. For arrays, this means the index of the element and
        /// for objects it means the index of the property.
        /// </summary>
        public int IndexInParent {
            get {
                if (_cacheIndexInArr != -1) {
                    return _cacheIndexInArr;
                }
                else {
                    return Template.TemplateIndex;
                }
            }
        }
    }

}
