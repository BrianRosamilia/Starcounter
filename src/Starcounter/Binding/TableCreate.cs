﻿
namespace Starcounter.Binding
{

    internal class TableCreate
    {

        private readonly TableDef tableDef_;

        public TableCreate(TableDef tableDef)
        {
            tableDef_ = tableDef;
        }

        public TableDef Eval()
        {
            TableDef tableDef = tableDef_;
            TableDef inheritedTableDef = null;

            if (tableDef.BaseName != null)
            {
                Db.Transaction(() =>
                {
                    inheritedTableDef = Db.LookupTable(tableDef.BaseName);
                });

                if (inheritedTableDef == null)
                {
                    // TODO: Base table does not exist. Should not happen.
                    throw ErrorCode.ToException(Error.SCERRUNSPECIFIED);
                }
            }

            // TODO:
            // Check that the first columns of the table definition matches
            // that of the inherited table. Do this in Db.CreateTable?

            Db.CreateTable(tableDef, inheritedTableDef);

            TableDef newTableDef = null;

            Db.Transaction(() =>
            {
                newTableDef = Db.LookupTable(tableDef.Name);
            });

            return newTableDef;
        }
    }
}
