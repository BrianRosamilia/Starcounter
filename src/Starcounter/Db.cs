// ***********************************************************************
// <copyright file="Db.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Advanced;
using Starcounter.Binding;
using Starcounter.Internal;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Starcounter
{

    public static partial class Db
    {
        /// <summary>
        /// </summary>
        public static DbEnvironment Environment { get; private set; }

        /// <summary>
        /// Gets the set of <see cref="Application"/>s currently running
        /// in the <see cref="Db"/>.
        /// </summary>
        public static Application[] Applications {
            get {
                return Application.GetAllApplications();
            }
        }

        /// <summary>
        /// Lookups the table.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>TableDef.</returns>
        public static TableDef LookupTable(string name)
        {
            unsafe
            {
                sccoredb.SCCOREDB_TABLE_INFO tableInfo;
                var r = sccoredb.sccoredb_get_table_info_by_name(name, out tableInfo);
                if (r == 0)
                {
                    return TableDef.ConstructTableDef(tableInfo);
                }
                if (r == Error.SCERRTABLENOTFOUND) return null;
                throw ErrorCode.ToException(sccoredb.star_get_last_error());
            }
        }

        /// <summary>
        /// Creates the table.
        /// </summary>
        /// <param name="tableDef">The table def.</param>
        public static void CreateTable(TableDef tableDef)
        {
            CreateTable(tableDef, null);
        }

        /// <summary>
        /// Creates the table.
        /// </summary>
        /// <param name="tableDef">The table def.</param>
        /// <param name="inheritedTableDef">The inherited table def.</param>
        public static void CreateTable(TableDef tableDef, TableDef inheritedTableDef)
        {
            unsafe
            {
                int implicitColumnCount;
                ushort inheritedTableId = UInt16.MaxValue;
                if (inheritedTableDef == null)
                {
                    implicitColumnCount = 1; // Implicit key column.
                }
                else
                {
                    // TODO:
                    // We're assume that the base table definition is complete
                    // (has definition address) and that the current table
                    // definition and the inherited table definition matches.

                    implicitColumnCount = inheritedTableDef.ColumnDefs.Length;
                    inheritedTableId = inheritedTableDef.TableId;
                }
                ColumnDef[] columns = tableDef.ColumnDefs;
                sccoredb.SC_COLUMN_DEFINITION[] column_definitions = new sccoredb.SC_COLUMN_DEFINITION[columns.Length - implicitColumnCount + 1];
                char* name = null;
                try
                {
                    for (int cc = column_definitions.Length - 1, ci = implicitColumnCount, di = 0; di < cc; ci++, di++)
                    {
                        column_definitions[di].name = (char *)Marshal.StringToCoTaskMemUni(columns[ci].Name);
                        column_definitions[di].type = columns[ci].Type;
                        column_definitions[di].is_nullable = columns[ci].IsNullable ? (byte)1 : (byte)0;
                    }
                    name = (char*)Marshal.StringToCoTaskMemUni(tableDef.Name);
                    fixed (sccoredb.SC_COLUMN_DEFINITION* fixed_column_definitions = column_definitions)
                    {
                        uint e = sccoredb.star_create_table(0, name, inheritedTableId, fixed_column_definitions, 0);
                        if (e != 0) throw ErrorCode.ToException(e);
                    }
                }
                finally
                {
                    if (name != null) Marshal.FreeCoTaskMem((IntPtr)name);
                    for (int i = 0; i < column_definitions.Length; i++)
                    {
                        if (column_definitions[i].name != null)
                            Marshal.FreeCoTaskMem((IntPtr)column_definitions[i].name);
                    }
                }
            }
        }

        /// <summary>
        /// Renames the table.
        /// </summary>
        /// <param name="tableId">The table id.</param>
        /// <param name="newName">The new name.</param>
        public static void RenameTable(ushort tableId, string newName)
        {
            uint e = sccoredb.star_rename_table(0, tableId, newName);
            if (e == 0) return;
            throw ErrorCode.ToException(e);
        }

        /// <summary>
        /// Drops the table.
        /// </summary>
        /// <param name="name">The name.</param>
        public static void DropTable(string name)
        {
            uint e = sccoredb.star_drop_table(0, name);
            if (e == 0) return;
            throw ErrorCode.ToException(e);
        }

        /// <summary>
        /// Executes the given <paramref name="action"/> within a new transaction.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="forceSnapshot">
        /// If set, instructs Starcounter to raise an error if the transaction can't
        /// be executed within a single snapshot (taken at the time of the transaction
        /// start). The default is false, allowing the isolation to drop to "read
        /// committed" in case the transaction for some reason should block or take a
        /// long time.
        /// </param>
        /// <param name="maxRetries">Number of times to retry the execution of the
        /// transaction if committing it fails because of a conflict with another
        /// transaction. Specify <c>int.MaxValue</c> to instruct Starcounter
        /// to try until the transaction succeeds. Specify 0 to disable retrying.
        /// </param>
        public static void Transaction(Action action, bool forceSnapshot = false, int maxRetries = 100)
        {
            int retries;
            uint r;
            ulong handle;
            ulong verify;

            if (maxRetries < 0) {
                throw new ArgumentOutOfRangeException("maxRetries", string.Format("Valid range: 0-{0}", int.MaxValue));
            }

            if (forceSnapshot) {
                throw ErrorCode.ToException(Error.SCERRNOTIMPLEMENTED, "Forcing snapshot isolation is not yet implemented.");
            }

            retries = 0;

            for (; ; )
            {
                r = sccoredb.sccoredb_create_transaction_and_set_current(0, 1, out handle, out verify);
                if (r == 0)
                {
                    var currentTransaction = Starcounter.Transaction._current;
                    Starcounter.Transaction._current = null;

                    try {
                        action();
                        Starcounter.Transaction.Commit(1, 1);
                        return;
                    }
                    catch (Exception ex) {
                        if (
                            sccoredb.sccoredb_set_current_transaction(1, 0, 0) == 0 &&
                            sccoredb.sccoredb_free_transaction(handle, verify) == 0
                            ) {
                            if (ex is ITransactionConflictException) {
                                if (++retries <= maxRetries) continue;
                                throw ErrorCode.ToException(Error.SCERRUNHANDLEDTRANSACTCONFLICT, ex);
                            }
                            throw;
                        }
                        HandleFatalErrorInTransactionScope();
                    }
                    finally {
                        if (currentTransaction != null) {
                            // There should be no current transaction and so
                            // there should be no reason setting the current
                            // transaction to the replaced one (other then the
                            // current transcation being bound to another
                            // thread).

                            Starcounter.Transaction.SetCurrent(currentTransaction);
                        }
                    }
                }

                if (r == Error.SCERRTRANSACTIONLOCKEDONTHREAD) {
                    // We already have a transaction locked on thread so we're
                    // already in a transaction scope (possibly an implicit one if
                    // for example in the context of a trigger): Just invoke the
                    // callback and exit.

                    try {
                        action();
                    }
                    catch {
                        // Operation will fail only if transaction is already
                        // aborted (in which case we need not abort it).

                        sccoredb.sccoredb_external_abort();
                        throw;
                    }
                    return;
                }

                throw ErrorCode.ToException(r);
            }
        }

        /// <summary>
        /// </summary>
        public static void SetEnvironment(DbEnvironment e) // TODO: Internal
        {
            Environment = e;
        }

        /// <summary>
        /// Gets a value indicating of the given object is a
        /// Starcounter database instance.
        /// </summary>
        /// <param name="obj">The object to evaluate.</param>
        /// <returns>True if considered a database object; false
        /// otherwise.</returns>
        public static bool IsPersistent(this object obj) {
            // This is probably not the final solution; we should compare
            // this one (and maybe other alternatives) to the one that is
            // probably fastest (but most complex), as suggested here:
            // http://www.starcounter.com/forum/showthread.php?2493-Making-a-polymorphic-Object.IsPersistent()
            return obj is IObjectProxy;
        }

        /// <summary>
        /// Deletes an object from the database. The runtime will check
        /// the object to see if it's a valid database object. If it is,
        /// the delete will be carried out. If not, an exception will be
        /// raised.
        /// </summary>
        /// <param name="target">
        /// The database object to delete.</param>
        public static void Delete(this object target) {
            var proxy = target as IObjectProxy;
            if (proxy == null) {
                // Proper error message
                // TODO:
                throw ErrorCode.ToException(Error.SCERRUNSPECIFIED, "Not a database object.");
            }
            Db.Delete(proxy);
        }

        /// <summary>
        /// Deletes a database object from the database.
        /// </summary>
        /// <param name="proxy">The database object to delete,
        /// reprsented as a <see cref="IObjectProxy"/>.</param>
        public static void Delete(this IObjectProxy proxy) {
            ulong oid;
            ulong address;

            oid = proxy.Identity;
            address = proxy.ThisHandle;

            var r = sccoredb.sccoredb_begin_delete(oid, address);
            if (r != 0) {
                // If the error is because the delete already was issued then
                // we ignore it and just return. We are processing the delete
                // of this object so it will be deleted eventually.

                if (r == Error.SCERRDELETEPENDING) return;
                throw ErrorCode.ToException(r);
            }

            // Invoke all callbacks. If any of theese throws an exception then
            // we rollback the issued delete and pass on the thrown exception
            // to the caller.

            try {
                InvokeOnDelete(proxy);
            } catch (Exception ex) {
                // We can't generate an exception from an error in this
                // function since this will hide the original error.
                //
                // We can handle any error that can occur except for a fatal
                // error (and this will kill the process) and that the thread
                // has been detached (shouldn't occur). The most important
                // thing is that the transaction lock set when the delete was
                // issued is released and this will be the case as long as none
                // of the above errors occur.

                sccoredb.sccoredb_abort_delete(oid, address);
                if (ex is System.Threading.ThreadAbortException) throw;
                throw ErrorCode.ToException(Error.SCERRERRORINHOOKCALLBACK, ex);
            }

            r = sccoredb.sccoredb_complete_delete(oid, address);
            if (r == 0) return;
            throw ErrorCode.ToException(r);
        }

        static void InvokeOnDelete(IObjectProxy proxy) {
            // These flags really don't do their work right now, since we
            // are in fact doing a cast just to retreive them. The whole
            // purpose of them is to not do an extra cast if not neccessary.
            // From the proxy, we must all for a non-generated way of
            // getting the binding.
            // TODO:

            var entityInterface = proxy as IEntity;
            if (entityInterface != null) {
                entityInterface.OnDelete();
            }

            //var typeBindingFlags = (proxy.TypeBinding as TypeBinding).Flags;
            //if ((typeBindingFlags & TypeBindingFlags.Callback_OnDelete) != 0) {
            //    ((IEntity)proxy).OnDelete();
            //}
        }

        private static void HandleFatalErrorInTransactionScope()
        {
            uint e = sccoredb.star_get_last_error();
            ExceptionManager.HandleInternalFatalError(e);
        }
    }
}
