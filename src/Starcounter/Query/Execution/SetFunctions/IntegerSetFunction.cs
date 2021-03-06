﻿// ***********************************************************************
// <copyright file="IntegerSetFunction.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter;
using Starcounter.Query;
using System;
using System.Collections.Generic;
using System.Collections;
using Starcounter.Binding;
using System.Diagnostics;

namespace Starcounter.Query.Execution
{
internal class IntegerSetFunction : SetFunction, ISetFunction
{
    INumericalExpression numExpr;
    IValueExpression valueExpr;
    Nullable<Int64> result;
    Int64 sum;
    Int64 count;

    internal IntegerSetFunction(SetFunctionType setFunc, INumericalExpression expr)
    {
        if (setFunc != SetFunctionType.MAX && setFunc != SetFunctionType.MIN && setFunc != SetFunctionType.SUM)
        {
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect setFunc: " + setFunc);
        }
        if (expr == null)
        {
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect expr.");
        }
        setFuncType = setFunc;
        numExpr = expr;
        valueExpr = null;
        result = null;
        sum = 0;
        count = 0;
    }

    internal IntegerSetFunction(IValueExpression expr)
    {
        if (expr == null)
        {
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect expr.");
        }
        setFuncType = SetFunctionType.COUNT;
        numExpr = null;
        valueExpr = expr;
        result = null;
        sum = 0;
        count = 0;
    }

    public DbTypeCode DbTypeCode
    {
        get
        {
            return DbTypeCode.Int64;
        }
    }

    internal Nullable<Int64> Result
    {
        get
        {
            switch (setFuncType)
            {
                case SetFunctionType.MAX:
                case SetFunctionType.MIN:
                    return result;

                case SetFunctionType.SUM:
                    if (count != 0)
                        return sum;
                    else
                        return null;

                case SetFunctionType.COUNT:
                    return count;

                default:
                    throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect setFuncType: " + setFuncType);
            }
        }
    }

    public ILiteral GetResult()
    {
        return new IntegerLiteral(Result);
    }

    public void UpdateResult(IObjectView obj)
    {
        Nullable<Int64> numValue = null;
        switch (setFuncType)
        {
            case SetFunctionType.MAX:
                numValue = numExpr.EvaluateToInteger(obj);
                if (numValue != null)
                {
                    if (result == null)
                    {
                        result = numValue;
                    }
                    else if (numValue.Value.CompareTo(result.Value) > 0)
                    {
                        result = numValue;
                    }
                }
                break;
            
            case SetFunctionType.MIN:
                numValue = numExpr.EvaluateToInteger(obj);
                if (numValue != null)
                {
                    if (result == null)
                    {
                        result = numValue;
                    }
                    else if (numValue.Value.CompareTo(result.Value) < 0)
                    {
                        result = numValue;
                    }
                }
                break;

            case SetFunctionType.SUM:
                numValue = numExpr.EvaluateToInteger(obj);
                if (numValue != null)
                {
                    sum = sum + numValue.Value;
                    count++;
                }
                break;

            case SetFunctionType.COUNT:
                if (!valueExpr.EvaluatesToNull(obj))
                {
                    count++;
                }
                break;

            default:
                throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect setFuncType: " + setFuncType);
        }
    }

    public void ResetResult()
    {
        result = null;
        sum = 0;
        count = 0;
    }

    public ISetFunction Clone(VariableArray varArray)
    {
        if (numExpr != null)
        {
            return new IntegerSetFunction(setFuncType, numExpr.CloneToNumerical(varArray));
        }
        if (valueExpr != null)
        {
            return new IntegerSetFunction(valueExpr.Clone(varArray));
        }
        throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect integer set function");
    }

    public void BuildString(MyStringBuilder stringBuilder, Int32 tabs)
    {
        stringBuilder.AppendLine(tabs, "IntegerSetFunction(");
        stringBuilder.AppendLine(tabs, setFuncType.ToString());
        if (numExpr != null)
        {
            numExpr.BuildString(stringBuilder, tabs + 1);
        }
        else if (valueExpr != null)
        {
            valueExpr.BuildString(stringBuilder, tabs + 1);
        }
        stringBuilder.AppendLine(tabs, ")");
    }

    /// <summary>
    /// Generates compilable code representation of this data structure.
    /// </summary>
    public void GenerateCompilableCode(CodeGenStringGenerator stringGen)
    {
        numExpr.GenerateCompilableCode(stringGen);
    }

#if DEBUG
    private bool AssertEqualsVisited = false;
    public bool AssertEquals(ISetFunction other) {
        IntegerSetFunction otherNode = other as IntegerSetFunction;
        Debug.Assert(otherNode != null);
        return this.AssertEquals(otherNode);
    }
    internal bool AssertEquals(IntegerSetFunction other) {
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
        Debug.Assert(this.result == other.result);
        if (this.result != other.result)
            return false;
        // Check references. This should be checked if there is cyclic reference.
        AssertEqualsVisited = true;
        bool areEquals = true;
        if (this.numExpr == null) {
            Debug.Assert(other.numExpr == null);
            areEquals = other.numExpr == null;
        } else
            areEquals = this.numExpr.AssertEquals(other.numExpr);
        AssertEqualsVisited = false;
        return areEquals;
    }
#endif
}
}
