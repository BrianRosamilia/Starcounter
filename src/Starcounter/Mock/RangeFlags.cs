// ***********************************************************************
// <copyright file="RangeFlags.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Internal;
using System;

namespace Starcounter
{

    /// <summary>
    /// Enum RangeFlags
    /// </summary>
    [Flags]
    public enum RangeFlags : uint
    {
        /// <summary>
        /// The include lesser key
        /// </summary>
        IncludeLesserKey = sccoredb.SC_ITERATOR_RANGE_INCLUDE_LSKEY,

        /// <summary>
        /// The include greater key
        /// </summary>
        IncludeGreaterKey = sccoredb.SC_ITERATOR_RANGE_INCLUDE_GRKEY,
    }
}
