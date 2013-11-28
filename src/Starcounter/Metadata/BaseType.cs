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
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.BaseType", null,
                "base_type", null,
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String)
                });
        }


        /// <inheritdoc />
        public BaseType(Uninitialized u)
            : base(u) {
        }

        internal BaseType()
            : this(null) {
            DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
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
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.MaterializedType", "Starcounter.Metadata.BaseType",
                "materialized_type", "base_type",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("primitive_type", sccoredb.STAR_TYPE_ULONG, false, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("PrimitiveType", DbTypeCode.UInt64)
                });
        }


        /// <inheritdoc />
        public MaterializedType(Uninitialized u)
            : base(u) {
        }

        internal MaterializedType()
            : this(null) {
            DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
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
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.RuntimeType", "Starcounter.Metadata.BaseType",
                "runtime_type", "base_type",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("vm_name", sccoredb.STAR_TYPE_STRING, true, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("VMName", DbTypeCode.String)
                });
        }

        /// <inheritdoc />
        public RuntimeType(Uninitialized u)
            : base(u) {
        }

        internal RuntimeType()
            : this(null) {
            DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
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
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.MappedType", "Starcounter.Metadata.RuntimeType",
                "mapped_type", "runtime_type",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("vm_name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("write_loss", sccoredb.STAR_TYPE_ULONG, false, false),
                    new ColumnDef("read_loss", sccoredb.STAR_TYPE_ULONG, false, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("VMName", DbTypeCode.String),
                    new PropertyDef("WriteLoss", DbTypeCode.Boolean),
                    new PropertyDef("ReadLoss",  DbTypeCode.Boolean)
                });
        }

        /// <inheritdoc />
        public MappedType(Uninitialized u)
            : base(u) {
        }

        internal MappedType()
            : this(null) {
            DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
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

    public sealed class ReferenceType : RuntimeType {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_table_id = 1;
            internal static int columnHandle_name = 2;
            internal static int columnHandle_vm_name = 3;
            internal static int columnHandle_table_ref = 4;
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
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.ReferenceType", "Starcounter.Metadata.RuntimeType",
                "reference_type", "runtime_type",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("vm_name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("table_ref", sccoredb.STAR_TYPE_REFERENCE, true, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("VMName", DbTypeCode.String),
                    new PropertyDef("TableRef", DbTypeCode.Object, "Starcounter.Metadata.MaterializedTable")
                });
        }

        /// <inheritdoc />
        public ReferenceType(Uninitialized u)
            : base(u) {
        }

        internal ReferenceType()
            : this(null) {
            DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }

        public MaterializedTable TableRef {
            get { return (MaterializedTable)DbState.ReadObject(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_table_ref); }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_table_ref,
                    value);
            }
        }
    }
}
