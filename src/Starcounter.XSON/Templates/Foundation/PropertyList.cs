// ***********************************************************************
// <copyright file="PropertyList.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;

namespace Starcounter.Templates {

    /// <summary>
    /// The collection of properties (Templates) in an Obj template. I.e. for the template PersonTemplate, the
    /// list might contain two elements such as TString "FirstName" and TString "LastName".
    /// </summary>
    public class PropertyList : IEnumerable<Template>, IList<Template>, IReadOnlyList<Template>
    {
        /// <summary>
        /// The owner of this list.
        /// </summary>
        private TObject parent;

        /// <summary>
        /// The full name dictionary. These names can contain characters that are not valid for C# properties,
        /// such as the $ character often found in Javascript identifiers.
        /// </summary>
        private readonly Dictionary<string, Template> nameLookup = new Dictionary<string, Template>();

        /// <summary>
        /// The property name dictionary contains property names that are legal to use in C#.
        /// </summary>
        private readonly Dictionary<string, Template> propertyNameLookup = new Dictionary<string, Template>();

        /// <summary>
        /// Dictionary of names of all exposed properties.
        /// </summary>
        private readonly Dictionary<string, Template> exposedPropertyLookup = new Dictionary<string, Template>();
        
        /// <summary>
        /// 
        /// </summary>
        private List<Template> list = new List<Template>();

        /// <summary>
        /// All properties in this list will be included when serializing a 
        /// typed json object to ordinary json.
        /// </summary>
        private List<Template> exposedProperties = new List<Template>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        internal PropertyList(TObject parent) {
            this.parent = parent;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearExposed() {
            exposedProperties.Clear();
            exposedPropertyLookup.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        internal void Expose(Template template) {
            exposedProperties.Add(template);
            exposedPropertyLookup.Add(template.TemplateName, template);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal Template GetExposedTemplateByName(string name) {
            Template template;
            exposedPropertyLookup.TryGetValue(name, out template);
            return template;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool IsExposed(Template template) {
            return exposedPropertyLookup.ContainsKey(template.TemplateName);
        }

        /// <summary>
        /// 
        /// </summary>
        public List<Template> ExposedProperties {
            get { return exposedProperties; }
        }

        /// <summary>
        /// Gets the <see cref="Template" /> with the specified id.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>Template.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Template this[int index] {
            get { return list[index]; }
            set {
                throw new NotImplementedException();

//                _List[index] = (Template)value; 
			}
        }

        /// <summary>
        /// Gets the <see cref="Template" /> with the specified id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>Template.</returns>
        public Template this[string id] {
            get {
                Template ret;
                if (!propertyNameLookup.TryGetValue(id, out ret))
                    return null;
                return ret;
            }
        }

        /// <summary>
        /// Gets the <see cref="Template" /> with the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Template.</returns>
        public Template GetTemplateByName(string name)
        {
            Template ret;
            nameLookup.TryGetValue(name, out ret);
            return ret;
        }

        /// <summary>
        /// Gets the <see cref="Template" /> with the specified propertyname.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public Template GetTemplateByPropertyName(string propertyName) {
            Template ret;
            propertyNameLookup.TryGetValue(propertyName, out ret);
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        internal void ChildNameIsSet(Template item) {
            nameLookup[item.TemplateName] = (Template)item;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        internal void ChildPropertyNameIsSet(Template item) {
            propertyNameLookup[item.PropertyName] = (Template)item;
        }

        /// <summary>
        /// Copies all items to the specified array.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The start index in the source.</param>
        public void CopyTo(Template[] array, int arrayIndex) {
            list.CopyTo((Template[])array, arrayIndex);
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
        int IList<Template>.IndexOf(Template item) {
            return list.IndexOf((Template)item);
        }

        /// <summary>
        /// Inserts the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        void IList<Template>.Insert(int index, Template item) {
            throw new NotImplementedException();
//            _List.Insert(index, (Template)item);
        }

        /// <summary>
        /// Removed the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        void IList<Template>.RemoveAt(int index) {
           throw new NotImplementedException();
//            _List.RemoveAt(index);
        }

        /// <summary>
        /// Gets the <see cref="Template" /> with the specified id.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>ITemplate.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        Template IList<Template>.this[int index] {
            get {
                return list[index];
            }
            set {
                throw new NotImplementedException();
//               _List[index] = (Template)value;
            }
        }

        /// <summary>
        /// Replaces an existing template with the specified template
        /// using the same name.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="System.Exception">This template is already used by an App. Cannot add more properties.</exception>
        public void Replace(Template item)
        {
            Template existing;
            Template newTemplate = (Template)item;
            PropertyList props;

            if (parent.Sealed)
                throw new Exception("This template is already used by an App. Cannot add more properties.");
            if (newTemplate.Parent != null)
                throw new Exception("Item already has a parent");

            existing = nameLookup[newTemplate.TemplateName];
            if (existing == null)
                throw new Exception("No item to replace found.");

            props = parent.Properties;
            props.nameLookup.Remove(newTemplate.TemplateName);
            props.ChildNameIsSet(newTemplate);

            if (newTemplate.PropertyName != null)
            {
                props.propertyNameLookup.Remove(existing.PropertyName);
                props.ChildPropertyNameIsSet(newTemplate);

				var index = props.exposedProperties.IndexOf(existing);
				if (index != -1) {
					props.exposedPropertyLookup.Remove(newTemplate.PropertyName);
					props.exposedPropertyLookup.Add(newTemplate.PropertyName, newTemplate);
					props.exposedProperties[index] = newTemplate;
				}
            }
            newTemplate._Parent = parent;
            newTemplate.TemplateIndex = existing.TemplateIndex;

            if (existing is TValue) {
                ((TValue)existing).CopyValueDelegates(newTemplate);
            }

            list[newTemplate.TemplateIndex] = newTemplate;
            parent.OnPropertyAdded(newTemplate);
        }

        /// <summary>
        /// Adds the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="System.Exception">This template is already used by an App. Cannot add more properties.</exception>
        public void Add(Template item) {
            Template t = (Template)item;
            if (parent.Sealed)
                throw new Exception("This template is already used by an App. Cannot add more properties.");
            if (t.Parent != null)
                throw new Exception("Item already has a parent");
            if (t.TemplateName != null) {
                var props = (PropertyList)(parent.Properties);
                props.ChildNameIsSet(t);
            }
            if (item.PropertyName != null) {
                var props = (PropertyList)(parent.Properties);
                props.ChildPropertyNameIsSet(t);
            }
            t._Parent = this.parent;
            t.TemplateIndex = this.Count; // Last one in list (added below)
            list.Add(t);
            Expose(t);
            parent.OnPropertyAdded(t);
        }

        /// <summary>
        /// Clears this instance.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        void ICollection<Template>.Clear() {
                       throw new NotImplementedException();
//            _List.Clear();
        }

        /// <summary>
        /// Checks if the specified item exists in the list.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if [contains] [the specified item]; otherwise, <c>false</c>.</returns>
        bool ICollection<Template>.Contains(Template item) {
            return list.Contains((Template)item);
        }

        /// <summary>
        /// Copies all items to the specified array.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The start index in the source.</param>
        void ICollection<Template>.CopyTo(Template[] array, int arrayIndex) {
            list.CopyTo((Template[])array, arrayIndex);
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <value>The count.</value>
        /// <returns>The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.</returns>
        int ICollection<Template>.Count {
            get { return list.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
        /// </summary>
        /// <value><c>true</c> if this instance is read only; otherwise, <c>false</c>.</value>
        /// <returns>true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.</returns>
        bool ICollection<Template>.IsReadOnly {
            get { return false; }
        }

        /// <summary>
        /// Removes the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        bool ICollection<Template>.Remove(Template item) {
                       throw new NotImplementedException();
//            return _List.Remove((Template)item);
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>IEnumerator{ITemplate}.</returns>
        IEnumerator<Template> IEnumerable<Template>.GetEnumerator() {
            return (IEnumerator<Template>)list.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return list.GetEnumerator();
        }

        /// <summary>
        /// Adds the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        void ICollection<Template>.Add(Template item) {
            throw new NotImplementedException();
        }
    }
}
