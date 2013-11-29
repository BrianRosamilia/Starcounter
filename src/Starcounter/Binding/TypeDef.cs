// ***********************************************************************
// <copyright file="TypeDef.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System.Diagnostics;
using System.Reflection;
namespace Starcounter.Binding
{

    /// <summary>
    /// Class TypeDef
    /// </summary>
    public class TypeDef
    {

        /// <summary>
        /// The name
        /// </summary>
        public string Name;

        /// <summary>
        /// The base name
        /// </summary>
        public string BaseName;

        /// <summary>
        /// The property defs
        /// </summary>
        public PropertyDef[] PropertyDefs;

        /// <summary>
        /// The type loader
        /// </summary>
        public TypeLoader TypeLoader;

        /// <summary>
        /// The table def
        /// </summary>
        public TableDef TableDef;

        /// <summary>
        /// Array parallel to TableDef.ColumnDefs with type codes of columns as mapped by
        /// properties.
        /// </summary>
        public DbTypeCode[] ColumnRuntimeTypes;

        private string LowerName_;
        /// <summary>
        /// Gets the full name of the class in lowercase.
        /// </summary>
        public string LowerName {
            get {
                if (LowerName_ == null)
                    LowerName_ = Name.ToLower();
                return LowerName_;
            }
        }

        private string ShortName_;
        /// <summary>
        /// Gets the class name without namespaces in lowercase.
        /// </summary>
        public string ShortName {
            get {
                if (ShortName_ == null) {
                    int pos = Name.LastIndexOf('.');
                    ShortName_ = pos == -1 ? Name.ToLower() : Name.Substring(pos + 1).ToLower();
                }
                return ShortName_;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeDef" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="baseName">Name of the base.</param>
        /// <param name="propertyDefs">The property defs.</param>
        /// <param name="typeLoader">The type loader.</param>
        /// <param name="tableDef">The table def.</param>
        public TypeDef(string name, string baseName, PropertyDef[] propertyDefs, TypeLoader typeLoader, TableDef tableDef, DbTypeCode[] columnRuntimeTypes)
        {
            Name = name;
            BaseName = baseName;
            PropertyDefs = propertyDefs;
            TypeLoader = typeLoader;
            TableDef = tableDef;
            ColumnRuntimeTypes = columnRuntimeTypes;
        }

        /// <summary>
        /// This is a help method, which creates TypeDef and TableDef for given names and columns/properties. Some property information are filled 
        /// from columns, thus it is assumed that columns and properties have the same order.
        /// </summary>
        /// <param name="typeName">The name of the type to create TypeDef</param>
        /// <param name="baseTypeName">The name of the base type or null</param>
        /// <param name="tableName">The name of the table to which type is mapped and for which TableDef to create.</param>
        /// <param name="baseTableName">The name of the base table</param>
        /// <param name="columnDefs">Complete ColumnDef for the TableDef</param>
        /// <param name="propDefs">PropertyDef with names and types of the properties with the same
        /// order as ColumnDef. Column names and nullable properties are extracted from columnDefs</param>
        /// <returns></returns>
        internal static TypeDef CreateTypeTableDef(string typeName, string baseTypeName, string tableName, string baseTableName,
            ColumnDef[] columnDefs, PropertyDef[] propDefs) {
            var typeCodes = new DbTypeCode[columnDefs.Length];
            typeCodes[0] = DbTypeCode.Key;  // Column 0 is always the key column, __id
            Debug.Assert(propDefs.Length + 1 == columnDefs.Length);
            for (int i = 0; i < propDefs.Length; i++) {
                // Complete PropertyDef from ColumnDef
                propDefs[i].IsNullable = columnDefs[i + 1].IsNullable;
                propDefs[i].ColumnName = columnDefs[i + 1].Name;
                // Populate TypeCodes
                typeCodes[i + 1] = propDefs[i].Type;
            }
            // Finally create TableDef and TypeDef
            var systemTableDef = new TableDef(tableName, baseTableName, columnDefs);
            var sysColumnTypeDef = new TypeDef(typeName, baseTypeName, propDefs,
                new TypeLoader(new AssemblyName("Starcounter"), typeName),
                systemTableDef, typeCodes);
            return sysColumnTypeDef;
        }

        internal IndexInfo2 GetIndexInfo(string name) {
            var indexInfo = TableDef.GetIndexInfo(name);
            if (indexInfo != null) {
                return new IndexInfo2(indexInfo, this);
            }
            return null;
        }
    }
}
