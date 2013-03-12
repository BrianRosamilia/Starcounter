// ***********************************************************************
// <copyright file="NullObject.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter;
using Starcounter.Binding;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Starcounter.Query.Execution
{
/// <summary>
/// A NullObject is used in an outer join when there is no corresponding object of some extent.
/// </summary>
internal sealed class NullObject : IObjectView
{
    ITypeBinding typeBinding;

    internal NullObject(ITypeBinding typeBind)
    {
        if (typeBind == null)
        {
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect typeBind.");
        }
        typeBinding = typeBind;
    }

    public ITypeBinding TypeBinding
    {
        get
        {
            return typeBinding;
        }
    }

    override public String ToString()
    {
        return Starcounter.Db.NullString;
    }

    public Nullable<Binary> GetBinary(Int32 index)
    {
        return null;
    }

    public Nullable<Boolean> GetBoolean(Int32 index)
    {
        return null;
    }

    public Nullable<Byte> GetByte(Int32 index)
    {
        return null;
    }

    public Nullable<DateTime> GetDateTime(Int32 index)
    {
        return null;
    }

    public Nullable<Decimal> GetDecimal(Int32 index)
    {
        return null;
    }

    public Nullable<Double> GetDouble(Int32 index)
    {
        return null;
    }

    public Nullable<Int16> GetInt16(Int32 index)
    {
        return null;
    }

    public Nullable<Int32> GetInt32(Int32 index)
    {
        return null;
    }

    public Nullable<Int64> GetInt64(Int32 index)
    {
        return null;
    }

    public IObjectView GetObject(Int32 index)
    {
        return null;
    }

    public Nullable<SByte> GetSByte(Int32 index)
    {
        return null;
    }

    public Nullable<Single> GetSingle(Int32 index)
    {
        return null;
    }

    public String GetString(Int32 index)
    {
        return null;
    }

    public Nullable<UInt16> GetUInt16(Int32 index)
    {
        return null;
    }

    public Nullable<UInt32> GetUInt32(Int32 index)
    {
        return null;
    }

    public Nullable<UInt64> GetUInt64(Int32 index)
    {
        return null;
    }

    public IObjectView GetExtension(Int32 index)
    {
        throw new NotSupportedException();
    }

    Boolean IObjectView.EqualsOrIsDerivedFrom(IObjectView obj)
    {
        return Equals(obj);
    }

    #region Temporary extension methods from Entity
    public void Attach(ObjectRef objectRef, TypeBinding typeBinding) {
        throw ErrorCode.ToException(Error.SCERRNOTIMPLEMENTED, "This interface is temporary");
    }
    public void Attach(ulong addr, ulong oid, TypeBinding typeBinding) {
        throw ErrorCode.ToException(Error.SCERRNOTIMPLEMENTED, "This interface is temporary");
    }
    public ObjectRef ThisRef {
        get {
            throw ErrorCode.ToException(Error.SCERRNOTIMPLEMENTED, "This interface is temporary");
        }
        set {
            throw ErrorCode.ToException(Error.SCERRNOTIMPLEMENTED, "This interface is temporary");
        }
    }
    public void Delete() {
        throw ErrorCode.ToException(Error.SCERRNOTIMPLEMENTED, "This interface method is temporary");
    }
    #endregion

#if DEBUG
    public bool AssertEquals(IObjectView other) {
        NullObject otherNode = other as NullObject;
        Debug.Assert(otherNode != null);
        return this.AssertEquals(otherNode);
    }
    internal bool AssertEquals(NullObject other) {
        Debug.Assert(other != null);
        if (other == null)
            return false;
        // Check basic types
        Debug.Assert(this.typeBinding == other.typeBinding);
        if (this.typeBinding != other.typeBinding)
            return false;
        return true;
    }
#endif
}
}
