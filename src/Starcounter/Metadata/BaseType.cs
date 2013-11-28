// ***********************************************************************
// <copyright file="BaseType.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Binding;
using Starcounter.Internal;
using System.Reflection;
using System;
using System.Diagnostics;

namespace Starcounter.Metadata {
    public abstract class BaseType : Entity {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_table_id = 1;
            internal static int columnHandle_name = 2;
        }
#pragma warning disable 0628, 0169
        #endregion

        /// <summary>
        /// Creates the database binding <see cref="TypeDef"/> representing
        /// the type in the database and holding its table- and column defintions.
        /// </summary>
        /// <remarks>
        /// Developer note: if you extend or change this class in any way, make
        /// sure to keep the <see cref="MaterializedColumn.__starcounterTypeSpecification"/>
        /// class in sync with what is returned by this method.
        /// </remarks>
        /// <returns>A <see cref="TypeDef"/> representing the current
        /// type.</returns>
        static internal TypeDef CreateTypeDef() {
            string typeName = "Starcounter.Metadata.BaseType";
            string baseTypeName = null;
            string tableName = "base_type";
            string baseTableName = null;
            var columnDefs = new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, false)
                };
            var propDefs = new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String)
                };

            var typeCodes = new DbTypeCode[columnDefs.Length];
            typeCodes[0] = DbTypeCode.Key;
            Debug.Assert(propDefs.Length + 1 == columnDefs.Length);
            for (int i = 0; i < propDefs.Length; i++) {
                propDefs[i].IsNullable = columnDefs[i + 1].IsNullable;
                propDefs[i].ColumnName = columnDefs[i + 1].Name;
                typeCodes[i + 1] = propDefs[i].Type;
            }

            var systemTableDef = new TableDef(tableName, baseTableName, columnDefs);
            var sysColumnTypeDef = new TypeDef(typeName, baseTypeName, propDefs,
                new TypeLoader(new AssemblyName("Starcounter"), typeName),
                systemTableDef, typeCodes);
            return sysColumnTypeDef;
        }


        /// <inheritdoc />
        public BaseType(Uninitialized u)
            : base(u) {
        }

        internal BaseType()
            : this(null) {
            DbState.Insert(BaseType.__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }

        /// <summary>
        /// Name of the type
        /// </summary>
        public string Name {
            get { return DbState.ReadString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_name); }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_name,
                    value);
            }
        }
    }

    public sealed class MaterializedType : BaseType {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_table_id = 1;
            internal static int columnHandle_name = 2;
            internal static int columnHandle_primitive_type = 3;
        }
#pragma warning disable 0628, 0169
        #endregion

        /// <summary>
        /// Creates the database binding <see cref="TypeDef"/> representing
        /// the type in the database and holding its table- and column defintions.
        /// </summary>
        /// <remarks>
        /// Developer note: if you extend or change this class in any way, make
        /// sure to keep the <see cref="MaterializedColumn.__starcounterTypeSpecification"/>
        /// class in sync with what is returned by this method.
        /// </remarks>
        /// <returns>A <see cref="TypeDef"/> representing the current
        /// type.</returns>
        static internal new TypeDef CreateTypeDef() {
            string typeName = "Starcounter.Metadata.MaterializedType";
            string baseTypeName = "Starcounter.Metadata.BaseType";
            string tableName = "materialized_type";
            string baseTableName = "base_type";
            var columnDefs = new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("primitive_type", sccoredb.STAR_TYPE_ULONG, false, false)
                };
            var propDefs = new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("PrimitiveType", DbTypeCode.UInt64)
                };

            var typeCodes = new DbTypeCode[columnDefs.Length];
            typeCodes[0] = DbTypeCode.Key;
            Debug.Assert(propDefs.Length + 1 == columnDefs.Length);
            for (int i = 0; i < propDefs.Length; i++) {
                propDefs[i].IsNullable = columnDefs[i + 1].IsNullable;
                propDefs[i].ColumnName = columnDefs[i + 1].Name;
                typeCodes[i + 1] = propDefs[i].Type;
            }

            var systemTableDef = new TableDef(tableName, baseTableName, columnDefs);
            var sysColumnTypeDef = new TypeDef(typeName, baseTypeName, propDefs,
                new TypeLoader(new AssemblyName("Starcounter"), typeName),
                systemTableDef, typeCodes);
            return sysColumnTypeDef;
        }


        /// <inheritdoc />
        public MaterializedType(Uninitialized u)
            : base(u) {
        }

        internal MaterializedType()
            : this(null) {
            DbState.Insert(Starcounter.Metadata.MaterializedType.__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }

        public UInt64 PrimitiveType {
            get { return DbState.ReadUInt64(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_primitive_type); }
            internal set {
                DbState.WriteUInt64(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_primitive_type,
                    value);
            }
        }
    }

    public abstract class RuntimeType : BaseType {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_table_id = 1;
            internal static int columnHandle_name = 2;
            internal static int columnHandle_vm_name = 3;
        }
#pragma warning disable 0628, 0169
        #endregion

        /// <summary>
        /// Creates the database binding <see cref="TypeDef"/> representing
        /// the type in the database and holding its table- and column defintions.
        /// </summary>
        /// <remarks>
        /// Developer note: if you extend or change this class in any way, make
        /// sure to keep the <see cref="MaterializedColumn.__starcounterTypeSpecification"/>
        /// class in sync with what is returned by this method.
        /// </remarks>
        /// <returns>A <see cref="TypeDef"/> representing the current
        /// type.</returns>
        static internal new TypeDef CreateTypeDef() {
            string typeName = "Starcounter.Metadata.RuntimeType";
            string baseTypeName = "Starcounter.Metadata.BaseType";
            string tableName = "runtime_type";
            string baseTableName = "base_type";
            var columnDefs = new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("vm_name", sccoredb.STAR_TYPE_STRING, true, false)
                };
            var propDefs = new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("VMName", DbTypeCode.String)
                };

            var typeCodes = new DbTypeCode[columnDefs.Length];
            typeCodes[0] = DbTypeCode.Key;
            Debug.Assert(propDefs.Length + 1 == columnDefs.Length);
            for (int i = 0; i < propDefs.Length; i++) {
                propDefs[i].IsNullable = columnDefs[i + 1].IsNullable;
                propDefs[i].ColumnName = columnDefs[i + 1].Name;
                typeCodes[i + 1] = propDefs[i].Type;
            }

            var systemTableDef = new TableDef(tableName, baseTableName, columnDefs);
            var sysColumnTypeDef = new TypeDef(typeName, baseTypeName, propDefs,
                new TypeLoader(new AssemblyName("Starcounter"), typeName),
                systemTableDef, typeCodes);
            return sysColumnTypeDef;
        }

        /// <inheritdoc />
        public RuntimeType(Uninitialized u)
            : base(u) {
        }

        internal RuntimeType()
            : this(null) {
            DbState.Insert(Starcounter.Metadata.MaterializedType.__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }

        public string VMName {
            get { return DbState.ReadString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_vm_name); }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_vm_name,
                    value);
            }
        }
    }

    public sealed class MappedType : RuntimeType {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_table_id = 1;
            internal static int columnHandle_name = 2;
            internal static int columnHandle_vm_name = 3;
            internal static int columnHandle_write_loss = 4;
            internal static int columnHandle_read_loss = 5;
        }
#pragma warning disable 0628, 0169
        #endregion

        /// <summary>
        /// Creates the database binding <see cref="TypeDef"/> representing
        /// the type in the database and holding its table- and column defintions.
        /// </summary>
        /// <remarks>
        /// Developer note: if you extend or change this class in any way, make
        /// sure to keep the <see cref="MaterializedColumn.__starcounterTypeSpecification"/>
        /// class in sync with what is returned by this method.
        /// </remarks>
        /// <returns>A <see cref="TypeDef"/> representing the current
        /// type.</returns>
        static internal new TypeDef CreateTypeDef() {
            string typeName = "Starcounter.Metadata.MappedType";
            string baseTypeName = "Starcounter.Metadata.RuntimeType";
            string tableName = "mapped_type";
            string baseTableName = "runtime_type";
            var columnDefs = new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("vm_name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("write_loss", sccoredb.STAR_TYPE_ULONG, false, false),
                    new ColumnDef("read_loss", sccoredb.STAR_TYPE_ULONG, false, false)
                };
            var propDefs = new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("VMName", DbTypeCode.String),
                    new PropertyDef("WriteLoss", DbTypeCode.Boolean),
                    new PropertyDef("ReadLoss",  DbTypeCode.Boolean)
                };

            var typeCodes = new DbTypeCode[columnDefs.Length];
            typeCodes[0] = DbTypeCode.Key;
            Debug.Assert(propDefs.Length + 1 == columnDefs.Length);
            for (int i = 0; i < propDefs.Length; i++) {
                propDefs[i].IsNullable = columnDefs[i + 1].IsNullable;
                propDefs[i].ColumnName = columnDefs[i + 1].Name;
                typeCodes[i + 1] = propDefs[i].Type;
            }

            var systemTableDef = new TableDef(tableName, baseTableName, columnDefs);
            var sysColumnTypeDef = new TypeDef(typeName, baseTypeName, propDefs,
                new TypeLoader(new AssemblyName("Starcounter"), typeName),
                systemTableDef, typeCodes);
            return sysColumnTypeDef;
        }

        /// <inheritdoc />
        public MappedType(Uninitialized u)
            : base(u) {
        }

        internal MappedType()
            : this(null) {
            DbState.Insert(Starcounter.Metadata.MaterializedType.__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }

        public Boolean WriteLoss {
            get { return DbState.ReadBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_write_loss); }
            internal set {
                DbState.WriteBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_write_loss,
                    value);
            }
        }

        public Boolean ReadLoss {
            get { return DbState.ReadBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_read_loss); }
            internal set {
                DbState.WriteBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_read_loss,
                    value);
            }
        }
    }
}
