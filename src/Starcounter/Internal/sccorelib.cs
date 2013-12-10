// ***********************************************************************
// <copyright file="sccorelib.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Starcounter.Internal
{

    /// <summary>
    /// Class sccorelib
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public static class sccorelib
    {

        /// <summary>
        /// Delegate THREAD_ENTER
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="p">The p.</param>
        /// <param name="init">The init.</param>
        public unsafe delegate void THREAD_ENTER(void* hsched, byte cpun, void* p, int init);

        /// <summary>
        /// Delegate THREAD_LEAVE
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="p">The p.</param>
        /// <param name="yr">The yr.</param>
        public unsafe delegate void THREAD_LEAVE(void* hsched, byte cpun, void* p, uint yr);

        /// <summary>
        /// The C M5_ STAR t_ FLA g_ FIRS t_ THREAD
        /// </summary>
        public const uint CM5_START_FLAG_FIRST_THREAD = 1;

        /// <summary>
        /// Delegate THREAD_START
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="p">The p.</param>
        /// <param name="sf">The sf.</param>
        public unsafe delegate void THREAD_START(void* hsched, byte cpun, void* p, uint sf);

        /// <summary>
        /// Delegate THREAD_RESET
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="p">The p.</param>
        public unsafe delegate void THREAD_RESET(void* hsched, byte cpun, void* p);

        /// <summary>
        /// Delegate THREAD_YIELD
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="p">The p.</param>
        /// <param name="yr">The yr.</param>
        /// <returns>System.Int32.</returns>
        public unsafe delegate int THREAD_YIELD(void* hsched, byte cpun, void* p, uint yr);

        /// <summary>
        /// Delegate VPROC_BGTASK
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="p">The p.</param>
        public unsafe delegate void VPROC_BGTASK(void* hsched, byte cpun, void* p);

        /// <summary>
        /// Delegate VPROC_CTICK
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="psec">The psec.</param>
        public unsafe delegate void VPROC_CTICK(void* hsched, byte cpun, uint psec);

        /// <summary>
        /// Delegate VPROC_IDLE
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="p">The p.</param>
        /// <returns>System.Int32.</returns>
        public unsafe delegate int VPROC_IDLE(void* hsched, byte cpun, void* p);

        /// <summary>
        /// Delegate VPROC_WAIT
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="p">The p.</param>
        public unsafe delegate void VPROC_WAIT(void* hsched, byte cpun, void* p);

        /// <summary>
        /// The C M5_ STAL l_ REASO n_ UNRESPOSIV e_ THREAD
        /// </summary>
        public const uint CM5_STALL_REASON_UNRESPOSIVE_THREAD = 1;

        /// <summary>
        /// The C M5_ STAL l_ REASO n_ THREAD s_ BLOCKED
        /// </summary>
        public const uint CM5_STALL_REASON_THREADS_BLOCKED = 2;

        /// <summary>
        /// The C M5_ STAL l_ REASO n_ UNYIELDIN g_ THREAD
        /// </summary>
        public const uint CM5_STALL_REASON_UNYIELDING_THREAD = 3;

        /// <summary>
        /// Delegate ALERT_STALL
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="p">The p.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="sr">The sr.</param>
        /// <param name="sc">The sc.</param>
        public unsafe delegate void ALERT_STALL(void* hsched, void* p, byte cpun, uint sr, uint sc);

        /// <summary>
        /// The C M5_ LOWME m_ REASO n_ PHYSICA l_ MEMORY
        /// </summary>
        public const uint CM5_LOWMEM_REASON_PHYSICAL_MEMORY = 1;

        /// <summary>
        /// The C M5_ LOWME m_ REASO n_ ADDRES s_ SPACE
        /// </summary>
        public const uint CM5_LOWMEM_REASON_ADDRESS_SPACE = 2;

        /// <summary>
        /// Delegate ALERT_LOWMEM
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="p">The p.</param>
        /// <param name="lr">The lr.</param>
        public unsafe delegate void ALERT_LOWMEM(void* hsched, void* p, uint lr);

        /// <summary>
        /// The C M5_ YIEL d_ REASO n_ TIME s_ UP
        /// </summary>
        public const uint CM5_YIELD_REASON_TIMES_UP = 1;

        /// <summary>
        /// The C M5_ YIEL d_ REASO n_ USE r_ INITIATED
        /// </summary>
        public const uint CM5_YIELD_REASON_USER_INITIATED = 2;

        /// <summary>
        /// The C M5_ YIEL d_ REASO n_ INTERRUPTED
        /// </summary>
        public const uint CM5_YIELD_REASON_INTERRUPTED = 3;

        /// <summary>
        /// The C M5_ YIEL d_ REASO n_ DETACHED
        /// </summary>
        public const uint CM5_YIELD_REASON_DETACHED = 4;

        /// <summary>
        /// The C M5_ YIEL d_ REASO n_ BLOCKED
        /// </summary>
        public const uint CM5_YIELD_REASON_BLOCKED = 5;

        /// <summary>
        /// The C M5_ YIEL d_ REASO n_ SUSPENDED
        /// </summary>
        public const uint CM5_YIELD_REASON_SUSPENDED = 6;

        /// <summary>
        /// The C M5_ YIEL d_ REASO n_ RELEASED
        /// </summary>
        public const uint CM5_YIELD_REASON_RELEASED = 7;


        /// <summary>
        /// Struct CM2_SETUP
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public unsafe struct CM2_SETUP
        {
            /// <summary>
            /// The name
            /// </summary>
            public char* name;
            /// <summary>
            /// The db_data_dir_path
            /// </summary>
            public char* db_data_dir_path;
            /// <summary>
            /// The server_name
            /// </summary>
            public char* server_name;
            /// <summary>
            /// The mem
            /// </summary>
            public void* mem;
            /// <summary>
            /// The mem_size
            /// </summary>
            public uint mem_size;
            /// <summary>
            /// The num_shm_chunks
            /// </summary>
            public uint num_shm_chunks;
            /// <summary>
            /// Number of workers in gateway
            /// </summary>
            public byte gateway_num_workers;
            /// <summary>
            /// The cpuc
            /// </summary>
            public byte cpuc;
            /// <summary>
            /// The hmenv
            /// </summary>
            public ulong hmenv;
            /// <summary>
            /// The is_system
            /// </summary>
            public int is_system;
            /// <summary>
            /// The th_enter
            /// </summary>
            public void* th_enter;
            /// <summary>
            /// The th_leave
            /// </summary>
            public void* th_leave;
            /// <summary>
            /// The th_start
            /// </summary>
            public void* th_start;
            /// <summary>
            /// The th_reset
            /// </summary>
            public void* th_reset;
            /// <summary>
            /// The th_yield
            /// </summary>
            public void* th_yield;
            /// <summary>
            /// The vp_bgtask
            /// </summary>
            public void* vp_bgtask;
            /// <summary>
            /// The vp_ctick
            /// </summary>
            public void* vp_ctick;
            /// <summary>
            /// The vp_idle
            /// </summary>
            public void* vp_idle;
            /// <summary>
            /// The vp_wait
            /// </summary>
            public void* vp_wait;
            /// <summary>
            /// The al_stall
            /// </summary>
            public void* al_stall;
            /// <summary>
            /// The al_lowmem
            /// </summary>
            public void* al_lowmem;
            /// <summary>
            /// The pex_ctxt
            /// </summary>
            public void* pex_ctxt;
        }


        /// <summary>
        /// Cm2_setups the specified psetup.
        /// </summary>
        /// <param name="psetup">The psetup.</param>
        /// <param name="phsched">The phsched.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe uint cm2_setup(CM2_SETUP* psetup, void** phsched);


        /// <summary>
        /// Cm2_starts the specified hsched.
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe uint cm2_start(void* hsched);

        /// <summary>
        /// Cm2_stops the specified hsched.
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="wait">The wait.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe uint cm2_stop(void* hsched, int wait);

        /// <summary>
        /// Cm3_get_cpuns the specified ignore.
        /// </summary>
        /// <param name="ignore">The ignore.</param>
        /// <param name="pcpun">The pcpun.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe uint cm3_get_cpun(void* ignore, byte* pcpun);

        /// <summary>
        /// Cm2_schedules the specified hsched.
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="cpun">The cpun.</param>
        /// <param name="type">The type.</param>
        /// <param name="prio">The prio.</param>
        /// <param name="output1">The output1.</param>
        /// <param name="output2">The output2.</param>
        /// <param name="output3">The output3.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe uint cm2_schedule(
	        void *hsched,
	        byte cpun,
            ushort type,
	        ushort prio,
	        uint output1,
            ulong output2,
	        ulong output3
        	);

        /// <summary>
        /// The C M2_ TYP e_ RELEASE
        /// </summary>
        public const ushort CM2_TYPE_RELEASE = 0x0000;

        /// <summary>
        /// The C M2_ TYP e_ REQUEST
        /// </summary>
        public const ushort CM2_TYPE_REQUEST = 0x0001;

        /// <summary>
        /// The C M2_ TYP e_ CALLBACK
        /// </summary>
        public const ushort CM2_TYPE_CALLBACK = 0x0010;

        /// <summary>
        /// Struct CM2_TASK_DATA
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CM2_TASK_DATA
        {
            /// <summary>
            /// The type
            /// </summary>
            public ushort Type;
            /// <summary>
            /// The prio
            /// </summary>
            public ushort Prio;
            /// <summary>
            /// The output1
            /// </summary>
            public uint Output1;
            /// <summary>
            /// The output2
            /// </summary>
            public ulong Output2;
            /// <summary>
            /// The output3
            /// </summary>
            public ulong Output3;
        };

        /// <summary>
        /// Cm2_standbies the specified hsched.
        /// </summary>
        /// <param name="hsched">The hsched.</param>
        /// <param name="ptask_data">The ptask_data.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe uint cm2_standby(void* hsched, CM2_TASK_DATA* ptask_data);

        /// <summary>
        /// Cm2_get_cpucs the specified h.
        /// </summary>
        /// <param name="h">The h.</param>
        /// <param name="pcpuc">The pcpuc.</param>
        /// <returns>UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        private unsafe extern static UInt32 cm2_get_cpuc(IntPtr h, Byte* pcpuc);

        /// <summary>
        /// Cm2_get_cpuns the specified h.
        /// </summary>
        /// <param name="h">The h.</param>
        /// <param name="pcpun">The pcpun.</param>
        /// <returns>UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        private unsafe extern static UInt32 cm2_get_cpun(IntPtr h, Byte* pcpun);

        /// <summary>
        /// Cm3_eautodets the specified h_opt.
        /// </summary>
        /// <param name="h_opt">The h_opt.</param>
        /// <returns>UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public extern static UInt32 cm3_eautodet(IntPtr h_opt);

#if true
        /// <summary>
        /// Cm3_set_yblks the specified h_opt.
        /// </summary>
        /// <param name="h_opt">The h_opt.</param>
        /// <returns>UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public extern static UInt32 cm3_set_yblk(IntPtr h_opt);

        /// <summary>
        /// Cm3_rel_yblks the specified h_opt.
        /// </summary>
        /// <param name="h_opt">The h_opt.</param>
        /// <returns>UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public extern static UInt32 cm3_rel_yblk(IntPtr h_opt);
#endif

        /// <summary>
        /// Expose the kernel method <c>cm3_sleep</c> to managed code.
        /// </summary>
        /// <param name="h_opt">Optional handle.</param>
        /// <param name="time">The time to sleep.</param>
        /// <returns>The result of the call.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public extern static UInt32 cm3_sleep(IntPtr h_opt, UInt32 time);

        /// <summary>
        /// Cm3_get_stashes the specified ignore.
        /// </summary>
        /// <param name="ignore">The ignore.</param>
        /// <param name="ppstash">The ppstash.</param>
        /// <returns>UInt32.</returns>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        public extern static unsafe UInt32 cm3_get_stash(void* ignore, UInt32** ppstash);

        public const uint MH4_MENV_MAX_SLABS = (uint)((0x4000000000UL - 1) / 8192);

        /// <summary>
        /// Mh4_menv_creates the specified mem128.
        /// </summary>
        /// <param name="mem128">The mem128.</param>
        /// <param name="slabs">The slabs.</param>
        /// <returns>System.UInt64.</returns>
        [DllImport("sunflower.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe ulong mh4_menv_create(void* mem128, uint slabs);

        /// <summary>
        /// </summary>
        [DllImport("sunflower.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void mh4_menv_alert_lowmem(ulong hmenv);

        /// <summary>
        /// Mh4_menv_trim_caches the specified hmenv.
        /// </summary>
        /// <param name="hmenv">The hmenv.</param>
        /// <param name="periodic">The periodic.</param>
        [DllImport("sunflower.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void mh4_menv_trim_cache(ulong hmenv, int periodic);

        /// <summary>
        /// </summary>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        internal extern static uint cm_send_to_client(uint client_index, uint chunk_index);

        /// <summary>
        /// </summary>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        internal extern static unsafe uint cm3_mevt_new(void* h_opt, int set, void** ph);

        /// <summary>
        /// </summary>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        internal extern static unsafe uint cm3_mevt_rel(void* h);

        /// <summary>
        /// </summary>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        internal extern static unsafe uint cm3_mevt_set(void* h);

        /// <summary>
        /// </summary>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall)]
        internal extern static unsafe uint cm3_mevt_wait(void* h, uint time, uint flags);

        internal const uint CM3_WAIT_FLAG_BLOCK_SCHED = 0x00000001;

        /// <summary>
        /// </summary>
        [DllImport("coalmine.dll")]
        public extern static int fix_wait_for_gateway_available(uint timeout_milliseconds);

        /// <summary>
        /// Gets the cpu count.
        /// </summary>
        /// <param name="handle">The handle.</param>
        /// <returns>Byte.</returns>
        public unsafe static Byte GetCpuCount(IntPtr handle)
        {
            byte cpuc;
            uint e = sccorelib.cm2_get_cpuc(handle, &cpuc);
            if (e == 0) return cpuc;
            throw ErrorCode.ToException(e);
        }

        /// <summary>
        /// Gets the cpu number.
        /// </summary>
        /// <returns>System.Byte.</returns>
        public unsafe static byte GetCpuNumber()
        {
            byte cpun;
            uint e = sccorelib.cm3_get_cpun(null, &cpun);
            if (e == 0) return cpun;
            throw ErrorCode.ToException(e);
        }

        /// <summary>
        /// Gets the state share.
        /// </summary>
        /// <returns>System.UInt32.</returns>
        public unsafe static uint* GetStateShare()
        {
            uint* pstash;
            uint e = sccorelib.cm3_get_stash(null, &pstash);
            if (e == 0) return pstash;
            throw ErrorCode.ToException(e);
        }
    };

    /// <summary>
    /// Class sccorelib_ext
    /// </summary>
    public static class sccorelib_ext
    {
        /// <summary>
        /// </summary>
        public const ushort TYPE_RECYCLE_SCRAP = 0x0100;

        /// <summary>
        /// </summary>
        public const ushort TYPE_RUN_TASK = 0x0101;
        
        /// <summary>
        /// </summary>
        public const ushort TYPE_PROCESS_PACKAGE = 0x0102;
    }
}
