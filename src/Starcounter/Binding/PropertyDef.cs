// ***********************************************************************
// <copyright file="PropertyDef.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

namespace Starcounter.Binding
{

    /// <summary>
    /// Class PropertyDef
    /// </summary>
    public sealed class PropertyDef
    {

        /// <summary>
        /// The name
        /// </summary>
        public string Name;
        /// <summary>
        /// The type
        /// </summary>
        public DbTypeCode Type;
        /// <summary>
        /// The is nullable
        /// </summary>
        public bool IsNullable;

        /// <summary>
        /// The target type name
        /// </summary>
        public string TargetTypeName;

        /// <summary>
        /// Name of column if representing a database column. NULL otherwise.
        /// </summary>
        public string ColumnName;

        /// <summary>
        /// Index of column if representing a database column. -1 otherwise.
        /// </summary>
        public int ColumnIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyDef" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        public PropertyDef(string name, DbTypeCode type) : this(name, type, false, null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyDef" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="targetTypeName">Name of the target type.</param>
        public PropertyDef(string name, DbTypeCode type, string targetTypeName) : this(name, type, false, targetTypeName) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyDef" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="isNullable">if set to <c>true</c> [is nullable].</param>
        public PropertyDef(string name, DbTypeCode type, bool isNullable) : this(name, type, isNullable, null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyDef" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="isNullable">if set to <c>true</c> [is nullable].</param>
        /// <param name="targetTypeName">Name of the target type.</param>
        public PropertyDef(string name, DbTypeCode type, bool isNullable, string targetTypeName)
        {
            Name = name;
            Type = type;
            IsNullable = isNullable;
            TargetTypeName = targetTypeName;
            //ColumnName = null;
            ColumnIndex = -1;
        }
    }
}
