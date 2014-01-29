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
            internal static int columnHandle_BaseTable;
            internal static int columnHandle_Name;
            internal static int columnHandle_Type;
        }
#pragma warning disable 0628, 0169
        #endregion
    
        static internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.RuntimeMember", null,
                "RuntimeMember", null,
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("BaseTable", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("Name", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("Type", sccoredb.STAR_TYPE_REFERENCE, true, false)
                },
                new PropertyDef[] {
                    new PropertyDef("BaseTable", DbTypeCode.Object, "Starcounter.Metadata.BaseTable"),
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("Type", DbTypeCode.Object, "Starcounter.Metadata.BaseType")
                });
        }

        public RuntimeMember(Uninitialized u) : base(u) { }

        public BaseTable BaseTable {
            get {
                return (BaseTable)DbState.ReadObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_BaseTable);
            }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_BaseTable, value);
            }
        }

        public String Name {
            get {
                return DbState.ReadString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_Name);
            }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_Name, value);
            }
        }

        public BaseType Type {
            get {
                return (BaseType)DbState.ReadObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_Type);
            }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_Type, value);
            }
        }
    }

    public sealed class TableColumn : RuntimeMember {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_BaseTable;
            internal static int columnHandle_Name;
            internal static int columnHandle_Type;
            internal static int columnHandle_MaterializedColumn;
            internal static int columnHandle_Unique;
        }
#pragma warning disable 0628, 0169
        #endregion

        static internal new TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.TableColumn", "Starcounter.Metadata.RuntimeMember",
                "TableColumn", "RuntimeMember",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("BaseTable", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("Name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("Type", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("MaterializedColumn", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("Unique", sccoredb.STAR_TYPE_ULONG, false, false)
                },
                new PropertyDef[] {
                    new PropertyDef("BaseTable", DbTypeCode.Object, "Starcounter.Metadata.BaseTable"),
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
                    __starcounterTypeSpecification.columnHandle_MaterializedColumn);
            }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_MaterializedColumn, value);
            }
        }

        public Boolean Unique {
            get {
                return DbState.ReadBoolean(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_Unique);
            }
            internal set {
                DbState.WriteBoolean(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_Unique, value);
            }
        }
    }

    public sealed class CodeProperty : RuntimeMember {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_BaseTable;
            internal static int columnHandle_Name;
            internal static int columnHandle_Type;
            internal static int columnHandle_Polymorphic;
        }
#pragma warning disable 0628, 0169
        #endregion

        static internal new TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.CodeProperty", "Starcounter.Metadata.RuntimeMember",
                "CodeProperty", "RuntimeMember",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("BaseTable", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("Name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("Type", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("Polymorphic", sccoredb.STAR_TYPE_ULONG, false, false)
                },
                new PropertyDef[] {
                    new PropertyDef("BaseTable", DbTypeCode.Object, "Starcounter.Metadata.BaseTable"),
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
                    __starcounterTypeSpecification.columnHandle_Polymorphic);
            }
            internal set {
                DbState.WriteByte(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_Polymorphic, value);
            }
        }
    }
}
