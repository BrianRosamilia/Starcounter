using Starcounter.Binding;
using Starcounter.Internal;
using System;

namespace Starcounter.Metadata {
    public abstract class RuntimeMember : Entity {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_runtime_view;
            internal static int columnHandle_name;
            internal static int columnHandle_type;
        }
#pragma warning disable 0628, 0169
        #endregion
    
        static internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.RuntimeMember", null,
                "runtime_member", null,
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("runtime_view", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("type", sccoredb.STAR_TYPE_REFERENCE, true, false)
                },
                new PropertyDef[] {
                    new PropertyDef("RuntimeView", DbTypeCode.Object, "Starcounter.Metadata.RuntimeView"),
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("Type", DbTypeCode.Object, "Starcounter.Metadata.BaseType")
                });
        }

        public RuntimeMember(Uninitialized u) : base(u) { }

        public RuntimeView RuntimeView {
            get {
                return (RuntimeView)DbState.ReadObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_runtime_view);
            }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_runtime_view, value);
            }
        }

        public String Name {
            get {
                return DbState.ReadString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_name);
            }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_name, value);
            }
        }

        public BaseType Type {
            get {
                return (BaseType)DbState.ReadObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_type);
            }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_type, value);
            }
        }
    }

    public sealed class TableColumn : RuntimeMember {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_runtime_view;
            internal static int columnHandle_name;
            internal static int columnHandle_type;
            internal static int columnHandle_materialized_column;
            internal static int columnHandle_unique;
        }
#pragma warning disable 0628, 0169
        #endregion

        static internal new TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.TableColumn", "Starcounter.Metadata.RuntimeMember",
                "table_column", "runtime_member",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("runtime_view", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("type", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("materialized_column", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("unique", sccoredb.STAR_TYPE_ULONG, false, false)
                },
                new PropertyDef[] {
                    new PropertyDef("RuntimeView", DbTypeCode.Object, "Starcounter.Metadata.RuntimeView"),
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("Type", DbTypeCode.Object, "Starcounter.Metadata.BaseType"),
                    new PropertyDef("MaterializedColumn", DbTypeCode.Object, "Starcounter.Metadata.MaterializedColumn"),
                    new PropertyDef("Unique", DbTypeCode.Boolean)
                });
        }

        public TableColumn(Uninitialized u) : base(u) { }

        internal TableColumn()
            : this(null) {
                DbState.Insert(__starcounterTypeSpecification.tableHandle, 
                    ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }

        public MaterializedColumn MaterializedColumn {
            get {
                return (MaterializedColumn)DbState.ReadObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_materialized_column);
            }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_materialized_column, value);
            }
        }

        public Boolean Unique {
            get {
                return DbState.ReadBoolean(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_unique);
            }
            internal set {
                DbState.WriteBoolean(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_unique, value);
            }
        }
    }

    public sealed class CodeProperty : RuntimeMember {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_runtime_view;
            internal static int columnHandle_name;
            internal static int columnHandle_type;
            internal static int columnHandle_polymorphic;
        }
#pragma warning disable 0628, 0169
        #endregion

        static internal new TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.CodeProperty", "Starcounter.Metadata.RuntimeMember",
                "code_property", "runtime_member",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("runtime_view", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("type", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("polymorphic", sccoredb.STAR_TYPE_ULONG, false, false)
                },
                new PropertyDef[] {
                    new PropertyDef("RuntimeView", DbTypeCode.Object, "Starcounter.Metadata.RuntimeView"),
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("Type", DbTypeCode.Object, "Starcounter.Metadata.BaseType"),
                    new PropertyDef("Polymorphic", DbTypeCode.Byte)
                });
        }

        public CodeProperty(Uninitialized u) : base(u) { }

        internal CodeProperty()
            : this(null) {
                DbState.Insert(__starcounterTypeSpecification.tableHandle, 
                    ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }

        public Byte Polymorphic {
            get {
                return DbState.ReadByte(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_polymorphic);
            }
            internal set {
                DbState.WriteByte(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_polymorphic, value);
            }
        }
    }
}
