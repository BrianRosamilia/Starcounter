﻿
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Starcounter.Advanced {
    
    /// <summary>
    /// </summary>
    public class DbEnvironment {

        /// <summary>
        /// </summary>
        public DbEnvironment(string databaseName, bool hasDatabase) { // TODO: Internal

            if (string.IsNullOrEmpty(databaseName)) throw new ArgumentException("databaseName");

            DatabaseName = databaseName;
            HasDatabase = hasDatabase;

            unsafe
            {
                Byte cpuc = 0;
                cm3_get_cpuc(null, &cpuc);
                SchedulerCount = cpuc;
            }
        }

        /// <summary>
        /// Name of the database.
        /// </summary>
        public string DatabaseName { get; private set; }

        /// <summary>
        /// Gets a value indicating whether there is a database attached to the current applet
        /// </summary>
        public bool HasDatabase { get; private set; }

        /// <summary>
        /// Gets the number of schedulers.
        /// </summary>
        [DllImport("coalmine.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        unsafe extern static UInt32 cm3_get_cpuc(void* h_opt, Byte* pcpuc);

        /// <summary>
        /// Gets the number of schedulers.
        /// </summary>
        public Byte SchedulerCount { get; private set; }
    }
}
