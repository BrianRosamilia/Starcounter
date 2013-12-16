// ***********************************************************************
// <copyright file="DatabaseRuntimeConfiguration.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Internal;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Starcounter.Advanced.Configuration {
    /// <summary>
    /// Configures the runtime properties of a Starcounter database.
    /// </summary>
    [XmlType("DatabaseRuntime", Namespace = ConfigurationElement.Namespace)]
    [Serializable]
    public class DatabaseRuntimeConfiguration : ConfigurationElement {
        /// <summary>
        /// Directory in which temporary files are stored.
        /// </summary>
        [DefaultValue(null)]
        public string TempDirectory {
            get {
                return _tempDirectory;
            }
            set {
                _tempDirectory = value;
                OnPropertyChanged("TempDirectory");
            }
        }
        private string _tempDirectory;

        /// <summary>
        /// Full path of the directory containing image files.
        /// </summary>
        /// <remarks>Required. No default value.</remarks>
        [DefaultValue(null)]
        public string ImageDirectory {
            get {
                return _imageDirectory;
            }
            set {
                _imageDirectory = value;
                OnPropertyChanged("ImageDirectory");
            }
        }
        private string _imageDirectory;

        /// <summary>
        /// Full path of the directory containing transaction log files.
        /// </summary>
        /// <remarks>Required. No default value.</remarks>
        [DefaultValue(null)]
        public string TransactionLogDirectory {
            get {
                return _transactionLogDirectory;
            }
            set {
                _transactionLogDirectory = value;
                OnPropertyChanged("TransactionLogDirectory");
            }
        }
        private string _transactionLogDirectory;

        /// <summary>
        /// Full path of the directory containing dump files.
        /// </summary>
        /// <remarks>
        /// By default, dump files are stored in <see cref="ImageDirectory"/>.
        /// </remarks>
        [DefaultValue(null)]
        public string DumpDirectory {
            get;
            set;
        }

        /// <summary>
        /// SQL Prolog process listening port number.
        /// </summary>
        public UInt16 SQLProcessPort {
            get {
                return _SQLProcessPort;
            }
            set {
                _SQLProcessPort = value;
                OnPropertyChanged(StarcounterConstants.BootstrapOptionNames.SQLProcessPort);
            }
        }
        private UInt16 _SQLProcessPort;

        /// <summary>
        /// Gets the default user HTTP port.
        /// </summary>
        /// <value>The default user HTTP port.</value>
        public UInt16 DefaultUserHttpPort
        {
            get
            {
                return _DefaultUserHttpPort;
            }
            set
            {
                _DefaultUserHttpPort = value;
                OnPropertyChanged(StarcounterConstants.BootstrapOptionNames.DefaultUserHttpPort);
            }
        }
        private UInt16 _DefaultUserHttpPort = StarcounterConstants.NetworkPorts.DefaultPersonalServerUserHttpPort;

        /// <summary>
        /// Gets the default session timeout.
        /// </summary>
        /// <value>The default session timeout.</value>
        public UInt32 DefaultSessionTimeoutMinutes
        {
            get
            {
                return _DefaultSessionTimeoutMinutes;
            }
            set
            {
                _DefaultSessionTimeoutMinutes = value;
                OnPropertyChanged(StarcounterConstants.BootstrapOptionNames.DefaultSessionTimeoutMinutes);
            }
        }
        private UInt32 _DefaultSessionTimeoutMinutes = StarcounterConstants.NetworkPorts.DefaultSessionTimeoutMinutes;

        /// <summary>
        /// Support of aggregations in SQL queries, for which current implementation is very slow.
        /// </summary>
        [DefaultValue(false)]
        public Boolean SqlAggregationSupport {
            get {
                return _sqlAggregationSupport;
            }
            set {
                _sqlAggregationSupport = value;
                OnPropertyChanged("SqlAggregationSupport");
            }
        }
        private Boolean _sqlAggregationSupport;

        /// <summary>
        /// Number of shared memory chunks, dividable by 256.
        /// </summary>
        public int ChunksNumber {
            get {
                return _chunksNumber;
            }
            set {
                _chunksNumber = value;
                OnPropertyChanged(StarcounterConstants.BootstrapOptionNames.ChunksNumber);
            }
        }
        private int _chunksNumber;

        /// <summary>
        /// Number of schedulers the database host should utilize.
        /// </summary>
        /// <remarks>
        /// The current default, applied if this property is set to NULL, is
        /// the number of processors on the hosting machine, as returned by
        /// <see cref="Environment.ProcessorCount"/>.
        /// </remarks>
        [DefaultValue(null)]
        public int? SchedulerCount {
            get;
            set;
        }
    }

    /// <summary>
    /// Levels of transaction consistency.
    /// </summary>
    public enum TransactionConsistencyLevel {
        /// <summary>
        /// Fuzzy reads, merging writes.
        /// </summary>
        MergingWrites = 0,

        /// <summary>
        /// Fuzzy reads, ordered writes.
        /// </summary>
        OrderedWrites = 1
    }
}