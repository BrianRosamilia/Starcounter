// ***********************************************************************
// <copyright file="Transaction.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Internal;
using System;
using Starcounter.Advanced;
using Starcounter.Binding;

namespace Starcounter
{
    /// <summary>
    /// </summary>
    public partial class Transaction
    {
        /// <summary>
        /// Commits current transaction.
        /// </summary>
        /// <param name="tran_locked_on_thread"></param>
        /// <param name="detach_and_free"></param>
        internal static void Commit(int tran_locked_on_thread, int detach_and_free) {
            uint r;
            ulong hiter;
            ulong viter;

            for (; ; ) {
                r = sccoredb.sccoredb_begin_commit(tran_locked_on_thread, out hiter, out viter);
                if (r == 0) {
                    // TODO: Handle triggers. Call abort commit on failure.
                    // r = sccoredb.sccoredb_abort_commit(tran_locked_on_thread);

                    r = sccoredb.sccoredb_complete_commit(
                            tran_locked_on_thread, detach_and_free
                            );
                    if (r == 0) break;
                }

#if true
                String additionalErrorInfo = null;
                unsafe {
                    char* unsafeAdditionalErrorInfo;
                    r = sccoredb.star_get_last_error(&unsafeAdditionalErrorInfo);
                    if (unsafeAdditionalErrorInfo != null) {
                        additionalErrorInfo = string.Concat(new string(unsafeAdditionalErrorInfo), ".");
                    }
                }
                throw ErrorCode.ToException(r, additionalErrorInfo);
#else
                throw ErrorCode.ToException(r);
#endif
            }
        }
    }

    /// <summary>
    /// Represents a long running transaction.
    /// </summary>
    public partial class Transaction : ITransaction, IDisposable {

        private const ulong _INVALID_VERIFY = 0xFF;

        /// <summary>
        /// Current long running transaction.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Note that in the case of a short running transaction (a transaction
        /// scope) this value will be null (motivation for not supporting
        /// fetching current transaction).
        /// </para>
        /// </remarks>
        [ThreadStatic]
        internal static Transaction _current;

        /// <summary>
        /// Sets given transaction as current.
        /// </summary>
        /// <param name="value">Transaction to set as current.</param>
        public static void SetCurrent(Transaction value) {

            // Checking if current transaction is the same.
            if (value == _current)
                return;

            ulong handle;
            ulong verify;

            if (value != null) {
                handle = value._handle;
                verify = value._verify;
            }
            else {
                handle = 0;
                verify = _INVALID_VERIFY;
            }

            uint r = sccoredb.sccoredb_set_current_transaction(0, handle, verify);
            if (r == 0) {
                _current = value;
                return;
            }

            throw ToException(value, r);
        }

        /// <summary>
        /// Returns current transaction if any.
        /// </summary>
        /// <returns></returns>
        public static Transaction GetCurrent() {
            return _current;
        }

        /// <summary>
        /// Creates a new transaction and sets it as current.
        /// </summary>
        public static Transaction NewCurrent() {
            return NewCurrent(false);
        }

        /// <summary>
        /// Creates a new transaction and sets it as current.
        /// </summary>
        /// <param name="readOnly">Transaction read-only flag.</param>
        /// <param name="detectConflicts">Transaction conflicts detection flag (merging writes are used if False).</param>
        /// <returns>New transaction object.</returns>
        public static Transaction NewCurrent(bool readOnly, bool detectConflicts = true) {
            try {
                ulong handle;
                ulong verify;
                uint flags = detectConflicts ? 0 : sccoredb.MDB_TRANSCREATE_MERGING_WRITES;

                if (readOnly)
                    flags |= sccoredb.MDB_TRANSCREATE_READ_ONLY;

                uint r = sccoredb.sccoredb_create_transaction_and_set_current(
                    flags,
                    0,
                    out handle,
                    out verify
                    );
                if (r == 0) {
                    try {
                        _current = new Transaction(handle, verify, readOnly);
                        return _current;
                    }
                    catch (Exception) {
                        if (
                            sccoredb.sccoredb_set_current_transaction(0, 0, 0) != 0 ||
                            sccoredb.sccoredb_free_transaction(handle, verify) != 0
                            ) {
                            ExceptionManager.HandleInternalFatalError(sccoredb.star_get_last_error());
                        }
                        throw;
                    }
                }
                throw ErrorCode.ToException(r);
            }
            catch {
                if (_current != null) Transaction.SetCurrent(_current);
                throw;
            }
        }

        /// <summary>
        /// Adds <paramref name="obj"/> to the write list of the
        /// currently attached transaction.
        /// </summary>
        /// </exception>
        /// <param name="obj">The object to be added to the transaction
        /// write list. If the object is not an instance of a database
        /// class, an exception will be raised.</param>
        public static void Touch(object obj) {
            var proxy = obj as IObjectProxy;
            if (proxy == null) {
                if (obj == null) {
                    throw new ArgumentNullException("obj");
                } else {
                    throw ErrorCode.ToException(
                        Error.SCERRCODENOTENHANCED,
                        string.Format("The type {0} is not a database class.", obj.GetType()),
                        (msg, inner) => { return new InvalidCastException(msg, inner); });
                }
            }

            Touch(proxy);
        }

        /// <summary>
        /// Adds <paramref name="proxy"/> to the write list of the
        /// currently attached transaction.
        /// </summary>
        /// </exception>
        /// <param name="proxy">The proxy referencing the kernel
        /// object to be added to the transaction write list.</param>
        public static void Touch(IObjectProxy proxy) {
            var dr = sccoredb.SCObjectFakeWrite(proxy.Identity, proxy.ThisHandle);
            if (dr != 0) throw ErrorCode.ToException(dr);
        }

        private static Exception ToException(Transaction transaction, uint r) {
            // If the error indicates that the object isn't owned we check if
            // verification is set to 0. If so the object has been disposed.

            if (
                r == Error.SCERRTRANSACTIONNOTOWNED &&
                transaction != null &&
                transaction._verify == _INVALID_VERIFY
                ) {
                return new ObjectDisposedException(null);
            }

            return ErrorCode.ToException(r);
        }

        private readonly ulong _handle;
        private ulong _verify;

        private readonly TransactionScrap _scrap;

        /// <summary>
        /// Gets a value indicating if the current transaction is a
        /// read-only transaction, i.e. a transaction where commits
        /// are not allowed.
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Default constructor to create read-write transaction.
        /// </summary>
        public Transaction() : this(false) { }

        /// <summary>
        /// New transaction constructor.
        /// </summary>
        /// <param name="readOnly">Transaction read-only flag.</param>
        /// <param name="detectConflicts">Transaction conflicts detection flag (merging writes are used if False).</param>
        public Transaction(bool readOnly, bool detectConflicts = true) {
            ulong handle;
            ulong verify;
            uint flags = detectConflicts ? 0 : sccoredb.MDB_TRANSCREATE_MERGING_WRITES;

            this.IsReadOnly = readOnly;
            if (readOnly)
                flags |= sccoredb.MDB_TRANSCREATE_READ_ONLY;

            uint r = sccoredb.sccoredb_create_transaction(
                flags,
                out handle,
                out verify
                );
            if (r == 0) {
                try {
                    _scrap = new TransactionScrap(handle, verify);
                    _handle = handle;
                    _verify = verify;
                    return;
                }
                catch (Exception) {
                    _verify = _INVALID_VERIFY;
                    r = sccoredb.sccoredb_free_transaction(handle, verify);
                    if (r != 0) ExceptionManager.HandleInternalFatalError(r);
                    throw;
                }
            }
            _verify = _INVALID_VERIFY;
            throw ErrorCode.ToException(r);
        }

        private Transaction(ulong handle, ulong verify, bool readOnly) {
            _handle = handle;
            _verify = verify;
            IsReadOnly = readOnly;
            _scrap = new TransactionScrap(handle, verify);
        }

        /// <summary>
        /// </summary>
        ~Transaction() {
            if (_verify == _INVALID_VERIFY) return;

            // Add transaction scrap to scrap heap, a job will be scheduled on
            // the owning thread to release the transaction (the finalizer
            // thread is not the owner of the transaction so it's not allowed
            // to).

            _verify = _INVALID_VERIFY;
            ScrapHeap.ThrowAway(_scrap);
        }

        /// <summary>
        /// <c>System.IDisposable</c> interface implementation.
        /// </summary>
        public void Dispose() {
            uint r;

            // If another thread attempts to dispose the context at the same
            // time as the current thread attempts to dispose it then it will
            // fail since it doesn't own the object (assuming that this does).
            // Therefore it's thread safe regardless of if the verification
            // used by the other thread is 0 or the original value.
            //
            // If disposed context is the current context then the current
            // context is always set to null before the context is disposed.

            if (_current == this) SetCurrent(null);
            r = sccoredb.sccoredb_free_transaction(_handle, _verify);
            if (r == 0) {
                _verify = _INVALID_VERIFY;
                GC.SuppressFinalize(this);
                return;
            }
            if (r == Error.SCERRTRANSACTIONNOTOWNED && _verify == _INVALID_VERIFY) {
                return;
            }
            throw ErrorCode.ToException(r);
        }

        /// <summary>
        /// Executes some code within this transaction scope.
        /// </summary>
        /// <param name="action">Delegate that is called on transaction.</param>
        public void Add(Action action) {
			Transaction old = _current;
			try {
				SetCurrent(this);
				action.Invoke();
			} finally {
				SetCurrent(old);
			}
        }

        /// <summary>
        /// Begins the transaction scope (sets transaction to current).
        /// </summary>
        /// <param name="action"></param>
        public void BeginScope() {
            SetCurrent(this);
        }

        /// <summary>
        /// Ends the transaction scope (resets current transaction).
        /// </summary>
        /// <param name="action"></param>
        public void EndScope() {
            SetCurrent(null);
        }

        /// <summary>
        /// Commits changes made on transaction.
        /// </summary>
        public void Commit() {
            Transaction current = _current;
            if (current == this) {
                Transaction.Commit(0, 0);
            }
            else {
                Transaction.SetCurrent(this);
                try {
                    Transaction.Commit(0, 0);
                }
                finally {
                    // We can't guarantee that old transaction is restored here
                    // since in theory the transaction or current context can
                    // have been manipulated by user code preventing this from
                    // happening. But if this is so there will be an exception
                    // at least.
                    //
                    // NOTE:
                    // It should be possible to make restore fail-safe if
                    // transaction is still bound while executing the commit
                    // within the context of the other transaction.

                    Transaction.SetCurrent(current);
                }
            }
        }

        /// <summary>
        /// Rollbacks uncommitted changes on transaction.
        /// </summary>
        public void Rollback() {
            Transaction current = _current;
            if (current == this) {
                uint r = sccoredb.sccoredb_rollback();
                if (r == 0) return;
                throw ToException(this, r);
            }
            else {
                Transaction.SetCurrent(this);
                try {
                    uint r = sccoredb.sccoredb_rollback();
                    if (r == 0) return;
                    throw ToException(this, r);
                }
                finally {
                    Transaction.SetCurrent(current);
                }
            }
        }

        /// <summary>
        /// Checks if there are any changes on transaction since last commit.
        /// </summary>
        public Boolean IsDirty {
            get {
                Int32 isDirty;
                uint r;

                unsafe {
                    r = sccoredb.Mdb_TransactionIsReadWrite(_handle, _verify, &isDirty);
                }

                if (r == 0)
                    return (isDirty != 0);

                throw ToException(this, r);
            }
        }
    }

    internal sealed class TransactionScrap {

        internal TransactionScrap Next;

        private readonly ulong _handle;
        private readonly ulong _verify;

        internal TransactionScrap(ulong handle, ulong verify) {
            //Next = null;
            _handle = handle;
            _verify = verify;
        }

        internal Byte OwnerCpu { get { return (byte)_verify; } }

        internal void Cleanup() {
            uint r = sccoredb.sccoredb_free_transaction(_handle, _verify);
            if (r == 0) return;
            throw ErrorCode.ToException(r);
        }
    }
    
    /// <summary>
    /// Used to clean up garbage collected objects that could not be freed by
    /// garbage collector directly because the associated memory is associated
    /// with a specific scheduler.
    /// </summary>
    public sealed class ScrapHeap {

        private static ScrapHeap[] _instances;
        private static unsafe void* _hsched;

        /// <summary>
        /// </summary>
        public unsafe static void Setup(void* hsched) {
            var schedulerCount = sccorelib.GetCpuCount((IntPtr)hsched);
            var instances = new ScrapHeap[schedulerCount];
            for (var i = 0; i < instances.Length; i++) {
                instances[i] = new ScrapHeap();
            }
            _instances = instances;

            _hsched = hsched;
        }

        internal static void ThrowAway(TransactionScrap scrap) {
            byte cpuNumber;
            ScrapHeap instance;
            TransactionScrap next;
            uint r;
            bool scheduleRecycle;

            cpuNumber = scrap.OwnerCpu;
            instance = _instances[cpuNumber];

            // Add the scrap to the scrap heap.
            //
            // If the scrap was the first bit of scrap on the heap we post a
            // request to cleanup the scrap on the heap.
            //
            // NOTE:
            // To be able to handle the the scheduler input queue is full we
            // _scheduleOnNextAdd to determin if to schedule a task instead of
            // if the scrap heap was empty. If the operation should fail we
            // reset this value and it will try again later. Currently we only
            // retry when more scrap is added so we could have a big leak on a
            // burst activity. Motivation for not doing a better solution is
            // that the problem with finit input queue should be solved in the
            // future so no point making a big effort on the workaround.
            
            lock (instance._syncRoot) {
                next = instance._firstScrap;
                scrap.Next = next;
                instance._firstScrap = scrap;
                //scheduleRecycle = (next == null);
                scheduleRecycle = instance._scheduleOnNextAdd;
                instance._scheduleOnNextAdd = false;
            }
            
            if (!scheduleRecycle) return;

            unsafe {
                r = sccorelib.cm2_schedule(
                    _hsched,
                    cpuNumber,
                    sccorelib_ext.TYPE_RECYCLE_SCRAP,
                    0,
                    0,
                    0,
                    0
                    );
            }

            if (r == 0) return;

            if (r == Error.SCERRINPUTQUEUEFULL) {
                lock (instance._syncRoot) {
                    instance._scheduleOnNextAdd = true;
                }
            }
            
            throw ErrorCode.ToException(r);
        }

        /// <summary>
        /// </summary>
        public static void RecycleScrap() {
            ThreadHelper.SetYieldBlock();
            try {
                var schedulerNumber = sccorelib.GetCpuNumber();
                var instance = _instances[schedulerNumber];
                instance.CleanupAll();
            }
            finally {
                ThreadHelper.ReleaseYieldBlock();
            }
        }

        private readonly object _syncRoot;
        private volatile TransactionScrap _firstScrap;

        /// <summary>
        /// This is here to provide a workaround to avoid unhandled exception
        /// should the scheduler input queue be full. See ThrowAway for
        /// details.
        /// </summary>
        private volatile bool _scheduleOnNextAdd;

        private ScrapHeap() {
            _syncRoot = new object();
            _scheduleOnNextAdd = true;
        }

        private void CleanupAll() {
            TransactionScrap scrap;

            lock (_syncRoot) {
                scrap = _firstScrap;
                _firstScrap = null;
                _scheduleOnNextAdd = true;
            }

            while (scrap != null) {
                scrap.Cleanup();
                scrap = scrap.Next;
            }
        }
    }
}
