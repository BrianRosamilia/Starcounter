﻿
using Starcounter.Binding;
using Starcounter.Internal;
using System;

namespace Starcounter
{


    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NotPersistentAttribute : Attribute {
    }
    
    public abstract class Entity : IObjectView
    {

        /// <summary>
        /// Specialized comparison operator for database objects. The
        /// framework-provided comparison operator for objects compares them by
        /// reference only, and for entity object's we want the comparison
        /// to be value based. This means that even if two objects are
        /// different in memory, the can still be considered equal if they only
        /// reference the same kernel database object.
        /// </summary>
        /// <param name="obj1">
        /// The first object in the comparison
        /// </param>
        /// <param name="obj2">
        /// The second object in the comparison
        /// </param>
        /// <returns>
        /// True if both references are null or if both references are not null
        /// and references the same kernel database object. False otherwise.
        /// </returns>
        public static bool operator == (Entity obj1, Entity obj2)
        {
            if (object.ReferenceEquals(obj1, null))
            {
                return object.ReferenceEquals(obj2, null);
            }
            return obj1.Equals(obj2);
        }

        /// <summary>
        /// Specialized comparison operator for database objects. The
        /// framework-provided comparison operator for objects compares them by
        /// reference only, and for entity object's we want the comparison
        /// to be value based. This means that even if two objects are
        /// different in memory, the can still be considered equal if they only
        /// reference the same kernel database object.
        /// </summary>
        /// <param name="obj1">
        /// The first object in the comparison
        /// </param>
        /// <param name="obj2">
        /// The second object in the comparison
        /// </param>
        /// <returns>
        /// False if both references are null or if both references are not null
        /// and references the same kernel database object. True otherwise.
        /// </returns>
        public static bool operator != (Entity obj1, Entity obj2)
        {
            if (object.ReferenceEquals(obj1, null))
            {
                return !object.ReferenceEquals(obj2, null);
            }
            return !obj1.Equals(obj2);
        }

        internal ObjectRef ThisRef;
        private TypeBinding typeBinding_;

        protected Entity()
            : base() {
            throw ErrorCode.ToException(Error.SCERRCODENOTENHANCED);
        }

        protected Entity(Uninitialized u) { }

        public Entity(ulong typeAddr, TypeBinding typeBinding, Uninitialized u)
        {
            DbState.Insert(this, typeAddr, typeBinding);
        }

        public void Delete()
        {
            int br;
            uint e;
            ObjectRef thisRef = ThisRef;
            // Issue the delete
            br = sccoredb.Mdb_ObjectIssueDelete(thisRef.ObjectID, thisRef.ETI);
            if (br == 0)
            {
                // If the error is because the delete already was issued then
                // we ignore it and just return. We are processing the delete
                // of this object so it will be deleted eventually.
                e = sccoredb.Mdb_GetLastError();
                if (e == Error.SCERRDELETEPENDING)
                {
                    return;
                }
                throw ErrorCode.ToException(e);
            }
            // Invoke all callbacks. If any of theese throws an exception then
            // we rollback the issued delete and pass on the thrown exception
            // to the caller.
            try
            {
                InvokeOnDelete();
            }
            catch (Exception ex)
            {
                // We can't generate an exception from an error in this
                // function since this will hide the original error.
                //
                // We can handle any error that can occur except for a fatal
                // error (and this will kill the process) and that the thread
                // has been detached (shouldn't occur). The most important
                // thing is that the transaction lock set when the delete was
                // issued is released and this will be the case as long as none
                // of the above errors occur.
                sccoredb.Mdb_ObjectDelete(thisRef.ObjectID, thisRef.ETI, 0);
                if (ex is System.Threading.ThreadAbortException) throw;
                throw ErrorCode.ToException(Error.SCERRERRORINHOOKCALLBACK, ex);
            }
            // Commit the delete.
            br = sccoredb.Mdb_ObjectDelete(thisRef.ObjectID, thisRef.ETI, 1);
            if (br != 0) return;
            throw ErrorCode.ToException(sccoredb.Mdb_GetLastError());
        }

        /// <summary>
        /// Overrides the .NET frameworks Object's Equals method, that will
        /// compare for object reference equality. For a database object, the
        /// equals function uses value equality logic rather than reference
        /// equality logic; that means, that even if two instances are
        /// different in memory, they can still be considered the same if they
        /// reference the same database kernel object.
        /// </summary>
        /// <param name="obj">
        /// The object to compare this instance to.
        /// </param>
        /// <returns>
        /// True if obj is not null, is an entity object and references the
        /// same kernel object as this; false otherwise.
        /// </returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as Entity);
        }

        public bool Equals(Entity obj)
        {
            if (obj != null)
            {
                if (obj.ThisRef.ObjectID == ThisRef.ObjectID)
                {
                    return (GetType() == obj.GetType());
                }
            }
            return false;
        }

        /// <summary>
        /// The GetHashCode implementation will return the hash-code of the
        /// unique object ID of a given object.
        /// </summary>
        public override int GetHashCode()
        {
            return ThisRef.ObjectID.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0}({1})", GetType().Name, ThisRef.ObjectID.ToString());
        }

        /// <summary>
        /// Called when a delete is issued.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For better performance; this method isn't called if not overridden
        /// in a base class.
        /// </para>
        /// <para>
        /// The transaction is locked on the thread during the call to this
        /// method (as long as called from Delete that is). Implementation is
        /// not allowed to change the current transaction or modify the state
        /// of the current transaction (like committing or rolling back the
        /// transaction). If the state of the transaction is changed by another
        /// thread the delete operation will be aborted.
        /// </para>
        /// </remarks>
        protected internal virtual void OnDelete()
        {
            // Note that this method isn't called unless overriden in a base
            // class and that override calls the base implementation. No use
            // putting any code here in other words.
        }

        internal void Attach(ObjectRef objectRef, TypeBinding typeBinding)
        {
            ThisRef.ETI = objectRef.ETI;
            ThisRef.ObjectID = objectRef.ObjectID;
            typeBinding_ = typeBinding;
        }

        internal void Attach(ulong addr, ulong oid, TypeBinding typeBinding)
        {
            ThisRef.ETI = addr;
            ThisRef.ObjectID = oid;
            typeBinding_ = typeBinding;
        }

        internal ushort TableId { get { return typeBinding_.TableId; } }

        private void InvokeOnDelete()
        {
            TypeBindingFlags typeBindingFlags = typeBinding_.Flags;
            if ((typeBindingFlags & TypeBindingFlags.Callback_OnDelete) != 0)
            {
                OnDelete();
            }
        }

        ITypeBinding IObjectView.TypeBinding { get { return typeBinding_; } }

        bool IObjectView.EqualsOrIsDerivedFrom(IObjectView obj)
        {
            throw new System.NotSupportedException();
        }

        Binary? IObjectView.GetBinary(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetBinary(this);
        }

        bool? IObjectView.GetBoolean(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetBoolean(this);
        }

        byte? IObjectView.GetByte(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetByte(this);
        }

        System.DateTime? IObjectView.GetDateTime(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetDateTime(this);
        }

        decimal? IObjectView.GetDecimal(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetDecimal(this);
        }

        double? IObjectView.GetDouble(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetDouble(this);
        }

        short? IObjectView.GetInt16(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetInt16(this);
        }

        int? IObjectView.GetInt32(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetInt32(this);
        }

        long? IObjectView.GetInt64(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetInt64(this);
        }

        IObjectView IObjectView.GetObject(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetObject(this);
        }

        sbyte? IObjectView.GetSByte(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetSByte(this);
        }

        float? IObjectView.GetSingle(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetSingle(this);
        }

        string IObjectView.GetString(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetString(this);
        }

        ushort? IObjectView.GetUInt16(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetUInt16(this);
        }

        uint? IObjectView.GetUInt32(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetUInt32(this);
        }

        ulong? IObjectView.GetUInt64(int index)
        {
            return typeBinding_.GetPropertyBinding(index).GetUInt64(this);
        }
    }
}