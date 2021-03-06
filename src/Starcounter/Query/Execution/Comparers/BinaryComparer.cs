// ***********************************************************************
// <copyright file="BinaryComparer.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter;
using System;
using System.Collections.Generic;
using Starcounter.Binding;
using System.Diagnostics;

namespace Starcounter.Query.Execution
{
internal class BinaryComparer : ISingleComparer
{
    protected IBinaryExpression expression;
    protected SortOrder ordering;

    internal BinaryComparer(IBinaryExpression expr, SortOrder ord)
    {
        if (expr == null)
        {
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect expr.");
        }
        ordering = ord;
        expression = expr;
    }

    public DbTypeCode ComparerTypeCode
    {
        get
        {
            return DbTypeCode.Binary;
        }
    }

    public IValueExpression Expression
    {
        get
        {
            return expression;
        }
    }

    public SortOrder SortOrdering
    {
        get
        {
            return ordering;
        }
    }

    private Int32 InternalCompare(Nullable<Binary> value1, Nullable<Binary> value2)
    {
        switch (ordering)
        {
            case SortOrder.Ascending:
                // Null is regarded as less than any value.
                if (value1 == null && value2 == null)
                {
                    return 0;
                }
                if (value1 == null)
                {
                    return -1;
                }
                if (value2 == null)
                {
                    return 1;
                }
                return value1.Value.CompareTo(value2.Value);
            case SortOrder.Descending:
                // Null is regarded as less than any value.
                if (value1 == null && value2 == null)
                {
                    return 0;
                }
                if (value1 == null)
                {
                    return 1;
                }
                if (value2 == null)
                {
                    return -1;
                }
                return value2.Value.CompareTo(value1.Value);
            default:
                throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect ordering: " + ordering);
        }
    }

    public Int32 Compare(Row obj1, Row obj2)
    {
        Nullable<Binary> value1 = expression.EvaluateToBinary(obj1);
        Nullable<Binary> value2 = expression.EvaluateToBinary(obj2);
        return InternalCompare(value1, value2);
    }

    public Int32 Compare(ILiteral value, Row obj)
    {
        if (!(value is BinaryLiteral))
        {
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect type of value.");
        }
        Nullable<Binary> value1 = (value as BinaryLiteral).EvaluateToBinary(null);
        Nullable<Binary> value2 = expression.EvaluateToBinary(obj);
        return InternalCompare(value1, value2);
    }

    public ILiteral Evaluate(Row obj)
    {
        return new BinaryLiteral(expression.EvaluateToBinary(obj));
    }

    public IQueryComparer Clone(VariableArray varArray)
    {
        return new BinaryComparer(expression.CloneToBinary(varArray), ordering);
    }

    public ISingleComparer CloneToSingleComparer(VariableArray varArray)
    {
        return new BinaryComparer(expression.CloneToBinary(varArray), ordering);
    }

    public void BuildString(MyStringBuilder stringBuilder, Int32 tabs)
    {
        stringBuilder.AppendLine(tabs, "BinaryComparer(");
        expression.BuildString(stringBuilder, tabs + 1);
        stringBuilder.AppendLine(tabs + 1, ordering.ToString());
        stringBuilder.AppendLine(tabs, ")");
    }

    /// <summary>
    /// Generates compilable code representation of this data structure.
    /// </summary>
    public void GenerateCompilableCode(CodeGenStringGenerator stringGen)
    {
        expression.GenerateCompilableCode(stringGen);
    }

#if DEBUG
    private bool AssertEqualsVisited = false;
    public bool AssertEquals(ISingleComparer other) {
        BinaryComparer otherNode = other as BinaryComparer;
        Debug.Assert(otherNode != null);
        return this.AssertEquals(otherNode);
    }
    internal bool AssertEquals(BinaryComparer other) {
        Debug.Assert(other != null);
        if (other == null)
            return false;
        // Check if there are not cyclic references
        Debug.Assert(!this.AssertEqualsVisited);
        if (this.AssertEqualsVisited)
            return false;
        Debug.Assert(!other.AssertEqualsVisited);
        if (other.AssertEqualsVisited)
            return false;
        // Check basic types
        Debug.Assert(this.ordering == other.ordering);
        if (this.ordering != other.ordering)
            return false;
        // Check references. This should be checked if there is cyclic reference.
        AssertEqualsVisited = true;
        bool areEquals = this.expression.AssertEquals(other.expression);
        AssertEqualsVisited = false;
        return areEquals;
    }
#endif
}
}
