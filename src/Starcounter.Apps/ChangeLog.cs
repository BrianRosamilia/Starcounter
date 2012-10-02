﻿using System;
using System.Collections.Generic;
using Starcounter.Templates;
using Starcounter.Templates.Interfaces;

namespace Starcounter
{
    internal class ChangeLog : IEnumerable<Change>
    {
        // TODO:
        // The session structure should be moved to App and 
        // the session should hold the changelog instance. We dont 
        // want several thread specific states (The log here and the current
        // session)
        [ThreadStatic]
        internal static ChangeLog Log;

        private List<Change> _changes;

        internal ChangeLog()
        {
            _changes = new List<Change>();
        }

        internal static void BeginRequest(ChangeLog log)
        {
            Log = log;
        }

        internal static void EndRequest()
        {
            Log.Clear();
            Log = null;
        }

        internal static void UpdateValue(App app, Property property)
        {
            if (!app.IsSerialized) return;
            if (!Log._changes.Exists((match) => { return match.IsChangeOf(app, property); }))
            {
                Log._changes.Add(Change.Update(app, property));
            }
        }

        internal static void UpdateValue(App app, IValueTemplate valueTemplate)
        {
            if (!app.IsSerialized) return;
            if (!Log._changes.Exists((match) => { return match.IsChangeOf(app, (Template)valueTemplate); }))
            {
                Log._changes.Add(Change.Update(app, valueTemplate));
            }
        }

        internal static void AddItemInList(App app, ListingProperty list, Int32 index)
        {
            if (!app.IsSerialized) return;
            Log._changes.Add(Change.Add(app, list, index));
        }

        internal static void RemoveItemInList(App app, ListingProperty list, Int32 index)
        {
            if (!app.IsSerialized) return;
            Log._changes.Add(Change.Remove(app, list, index));
        }

        internal void Clear()
        {
            _changes.Clear();
        }

        public IEnumerator<Change> GetEnumerator()
        {
            return _changes.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _changes.GetEnumerator();
        }

        internal Int32 Count { get { return _changes.Count; } }
    }

    internal struct Change
    {
        public const Int32 UNDEFINED = 0;
        public const Int32 REMOVE = 1;
        public const Int32 REPLACE = 2;
        public const Int32 ADD = 3;

        internal static Change Null = new Change(UNDEFINED, null, null, -1);

        internal readonly Int32 ChangeType;
        internal readonly App App;
        internal readonly Template Template;
        internal readonly Int32 Index;

        private Change(Int32 changeType, App app, Template template, Int32 index)
        {
            ChangeType = changeType;
            App = app;
            Template = template;
            Index = index;
        }

        internal Boolean IsChangeOf(App app, Template template)
        {
            return (App == app && Template == template);
        }

        internal static Change Add(App app, ListingProperty list, Int32 index)
        {
            return new Change(Change.ADD, app, list, index);
        }

        internal static Change Remove(App app, ListingProperty list, Int32 index)
        {
            return new Change(Change.REMOVE, app, list, index);
        }

        internal static Change Update(App app, Property property)
        {
            return new Change(Change.REPLACE, app, property, -1);
        }

        internal static Change Update(App app, IValueTemplate valueTemplate)
        {
            return new Change(Change.REPLACE, app, (Template)valueTemplate, -1);
        }

        //public override bool Equals(object obj)
        //{
        //    return Change.Equals(this, (Change)obj);
        //}

        //public bool Equals(Change change)
        //{
        //    return Change.Equals(this, change);
        //}

        //public static bool Equals(Change c1, Change c2)
        //{
        //    return Template.Equals(c1.Template, c2.Template);
        //}

        //public override int GetHashCode()
        //{
        //    return Template.GetHashCode();
        //}
    }
}
