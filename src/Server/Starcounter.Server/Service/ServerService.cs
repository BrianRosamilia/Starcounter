
using Starcounter.Server.Windows;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace Starcounter.Server.Service {
    /// <summary>
    /// Expose a set of methods that is used to manage the Starcounter
    /// service when installed as a platform service on a given OS.
    /// </summary>
    public static class ServerService {
        /// <summary>
        /// Gets the name we use for the configured service.
        /// </summary>
        public const string Name = "StarcounterSystemService";

        /// <summary>
        /// Gets a value indicating if <paramref name="service"/> represents
        /// the configuration of a Starcounter server service.
        /// </summary>
        /// <param name="service">
        /// The service to check.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="service"/> is identified
        /// as a configured system service, <see langword="false"/> if not.
        /// </returns>
        public static bool IsServerService(ServiceController service) {
            return service.ServiceName.Equals(ServerService.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        public static ServiceController Find(string serviceName = ServerService.Name) {
            ServiceController result = null;
            foreach (var item in ServiceController.GetServices()) {
                if (IsServerService(item)) {
                    result = item;
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// Creates a Starcounter server service using the supplied parameters
        /// and internal knowledge.
        /// </summary>
        /// <param name="serviceManagerHandle">Handle to the service manager. Must
        /// be opened with the privilege to create new services in the service
        /// database.</param>
        /// <param name="displayName">The display name to use. Pass <see langword="null"/>
        /// to use the default display name.</param>
        /// <param name="serviceName">The name of the service to create.</param>
        /// <param name="description">A text describing the functionality of the
        /// service. Pass <see langword="null"/> to use the default description.
        /// </param>
        /// <param name="startupType">The startup type of the service.</param>
        /// <param name="commandLine">The command line of the service, referencing the
        /// Starcounter server executable and the arguments to pass to it.</param>
        /// <param name="user">The user under which the service should run. Pass 
        /// <see langword="null"/> to install under "LocalSystem".</param>
        /// <param name="password">The password to use by the system when logging on
        /// as the given <paramref name="user"/>.</param>
        /// <param name="tryDifferentNames">An optional parameters specifying that
        /// the methods should try with a prefix on the service name if creation
        /// fails because a service with the same name already exist.</param>
        /// <returns>The service name of the created service.</returns>
        public static string Create(
            IntPtr serviceManagerHandle,
            string displayName,
            string serviceName,
            string description,
            StartupType startupType,
            string commandLine,
            string user,
            string password,
            bool tryDifferentNames = false) {

            Win32Service.SERVICE_START serviceStart;
            string serviceNameCandidate;
            int serviceNumber;
            string serviceDisplayName;
            string serviceDisplayNameCandidate;
            IntPtr serviceHandle;
            int win32error;

            serviceNumber = 0;
            serviceNameCandidate = serviceName;
            serviceDisplayName = serviceDisplayNameCandidate = displayName ?? "Starcounter System Server";
            serviceStart = (Win32Service.SERVICE_START)startupType;

            string startName = user == null ? null : string.Format(".\\{0}", user);
            for (; ; ) {
                serviceHandle = Win32Service.CreateService(
                    serviceManagerHandle,
                    serviceNameCandidate,
                    serviceDisplayNameCandidate,
                    (uint)Win32Service.SERVICE_ACCESS.SERVICE_ALL_ACCESS,
                    (uint)Win32Service.SERVICE_TYPE.SERVICE_WIN32_OWN_PROCESS,
                    (uint)serviceStart,
                    (uint)Win32Service.SERVICE_ERROR.SERVICE_ERROR_NORMAL,
                    commandLine,
                    null,
                    null,
                    null,
                    startName,
                    password
                    );
                if (serviceHandle != IntPtr.Zero) break;

                win32error = Marshal.GetLastWin32Error();

                if (tryDifferentNames && (win32error == 1073 || win32error == 1078)) {
                    // Dont let non-crucial conflicting service names or service
                    // display names hinder us.

                    serviceNumber++;
                    if (win32error == 1073) {
                        serviceNameCandidate = string.Format("{0}{1}", serviceName, serviceNumber);
                    } else {
                        serviceDisplayNameCandidate = string.Format("{0} ({1})", serviceDisplayName, serviceNumber);
                    }

                } else {
                    throw new Win32Exception(win32error);
                }
            }

            try {
                // Configure the service for our needs and add it to the
                // remote configuration if instructed to, and make sure to
                // execute all further setup code guarded with a finally
                // clause that properly closes the service handle.

                ConfigureService(serviceHandle, description);

            } finally {
                Win32Service.CloseServiceHandle(serviceHandle);
            }

            return serviceNameCandidate;
        }

        /// <summary>
        /// Deletes a Starcounter server service (removing it from the registry)
        /// given it's service name.
        /// </summary>
        /// <param name="serviceName">Name of the service to delete.</param>
        public static void Delete(string serviceName = ServerService.Name) {
            if (string.IsNullOrEmpty(serviceName)) throw new ArgumentNullException("serviceName");

            Delete(new ServiceController(serviceName));
        }

        /// <summary>
        /// Starts the Starcounter server service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="millisecondsTimeout">Optional timeout to wait for it to become running.</param>
        public static void Start(string serviceName = ServerService.Name, int millisecondsTimeout = Timeout.Infinite) {
            var timeout = millisecondsTimeout == Timeout.Infinite ? TimeSpan.FromHours(24) : TimeSpan.FromMilliseconds(millisecondsTimeout);
            using (var controller = new ServiceController(serviceName)) {
                var status = controller.Status;
                if (status != ServiceControllerStatus.Running) {
                    if (status != ServiceControllerStatus.StartPending) {
                        controller.Start();
                    }

                    controller.Refresh();

                    // Waiting for service to start.
                    while (controller.Status == ServiceControllerStatus.StartPending) {
                        controller.Refresh();
                        Thread.Sleep(300);
                    }

                    // Checking if service is running properly.
                    controller.Refresh();
                    if (controller.Status != ServiceControllerStatus.Running) {
                        throw new Exception("The Starcounter service didn't start properly. Please check server logs for details.");
                    }
                }
            }
        }

        /// <summary>
        /// Stops the Starcounter server service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="millisecondsTimeout">Optional timeout to wait for it to stop.</param>
        public static void Stop(string serviceName = ServerService.Name, int millisecondsTimeout = Timeout.Infinite) {
            var timeout = millisecondsTimeout == Timeout.Infinite ? TimeSpan.FromHours(24) : TimeSpan.FromMilliseconds(millisecondsTimeout);
            using (var controller = new ServiceController(serviceName)) {
                try {
                    var status = controller.Status;
                    if (status != ServiceControllerStatus.Stopped) {
                        if (status != ServiceControllerStatus.StopPending) {
                            controller.Stop();
                        }
                        controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    }
                } catch (InvalidOperationException invalidOperation) {
                    var win32 = invalidOperation.InnerException as Win32Exception;
                    if (win32 == null || win32.NativeErrorCode != Win32Error.ERROR_SERVICE_DOES_NOT_EXIST) {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Deletes a Starcounter server service (removing it from the registry)
        /// given a reference to a <see cref="ServiceController"/> referencing it.
        /// </summary>
        /// <param name="service">A <see cref="ServiceController"/> referencing
        /// the service to delete.</param>
        internal static void Delete(ServiceController service) {
            if (service == null) throw new ArgumentNullException("service");

            using (var manager = LocalWindowsServiceManager.Open(Win32Service.SERVICE_ACCESS.SERVICE_CHANGE_CONFIG)) {
                ServerService.Delete(manager.Handle, service);
            }
        }

        /// <summary>
        /// Deletes a Starcounter server service (removing it from the registry)
        /// given it's service name.
        /// </summary>
        /// <param name="serviceManagerHandle">A handle to the service manager.
        /// The manager handle must be opened with the permission to change the
        /// service configuration (i.e. SERVICE_ACCESS.SERVICE_CHANGE_CONFIG).
        /// </param>
        /// <param name="serviceName">Name of the service to delete.</param>
        internal static void Delete(
            IntPtr serviceManagerHandle,
            string serviceName) {
            if (string.IsNullOrEmpty(serviceName)) throw new ArgumentNullException("serviceName");

            Delete(serviceManagerHandle, new ServiceController(serviceName));
        }

        /// <summary>
        /// Deletes a Starcounter server service (removing it from the registry)
        /// given a reference to a <see cref="ServiceController"/> referencing it.
        /// </summary>
        /// <param name="serviceManagerHandle">A handle to the service manager.
        /// The manager handle must be opened with the permission to change the
        /// service configuration (i.e. SERVICE_ACCESS.SERVICE_CHANGE_CONFIG).
        /// </param>
        /// <param name="service">A <see cref="ServiceController"/> referencing
        /// the service to delete.</param>
        internal static void Delete(IntPtr serviceManagerHandle, ServiceController service) {
            if (serviceManagerHandle == IntPtr.Zero) throw new ArgumentNullException("serviceManagerHandle");
            if (service == null) throw new ArgumentNullException("service");

            int win32Error = 0;
            Exception original = null;
            try {
                using (var opened = LocalWindowsServiceHandle.Open(serviceManagerHandle, service.ServiceName, Win32Service.SERVICE_ACCESS.DELETE)) {
                    var result = Win32Service.DeleteService(opened.Handle);
                    if (!result) {
                        win32Error = Marshal.GetLastWin32Error();
                    }
                }
            } catch (InvalidOperationException invalid) {
                var innerWin32 = invalid.InnerException as Win32Exception;
                if (innerWin32 == null) {
                    throw;
                }
                original = invalid;
                win32Error = innerWin32.NativeErrorCode;
            } catch (Win32Exception win32ex) {
                original = win32ex;
                win32Error = win32ex.NativeErrorCode;
            }

            if (win32Error != 0) {
                // Ignore a few: 1072 and 1060.
                if (win32Error != Win32Error.ERROR_SERVICE_DOES_NOT_EXIST && win32Error != Win32Error.ERROR_SERVICE_MARKED_FOR_DELETE) {
                    var x = original ?? new Win32Exception(win32Error);
                    throw x;
                }
            }                        
        }

        private static unsafe void ConfigureService(IntPtr serviceHandle, string description) {
            Win32Service.SERVICE_DESCRIPTION pDescription;
            bool result;
            const int actionCount = 3;
            // Add a description to the service
            description = description ?? string.Format(
                              "Manages Starcounter databases. If this service is disabled, the databases in the catalogue of Starcounter " +
                              "databases it maintains will not be started or monitored, and neither will they be displayed in client programs " +
                              "such as 'Starcounter Administrator' and the 'Starcounter for Visual Studio' extension."
                          );
            pDescription.lpDescription = Marshal.StringToHGlobalUni(description);
            try {
                result = Win32Service.ChangeServiceConfig2(
                             serviceHandle,
                             (int)Win32Service.SERVICE_CONFIGURATION_INFO_LEVEL.SERVICE_CONFIG_DESCRIPTION,
                             new IntPtr(&pDescription)
                         );
                if (result == false) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            } finally {
                Marshal.FreeHGlobal(pDescription.lpDescription);
            }
            // Configure recovery actions, the strategy for the service manager to use if
            // the service does not work as expected.
            Win32Service.SC_ACTION* pActions = stackalloc Win32Service.SC_ACTION[actionCount];

            // First failure restart is done immediately.
            pActions[0].Type = Win32Service.SC_ACTION_TYPE.RestartService;
            pActions[0].Delay = 60 * 1000;

            // Second failure restart is done after 1 minute.
            pActions[1].Type = Win32Service.SC_ACTION_TYPE.RestartService;
            pActions[1].Delay = 60 * 1000;

            // No third failure restart.
            pActions[2].Type = Win32Service.SC_ACTION_TYPE.None;
            pActions[2].Delay = 0;

            Win32Service.SERVICE_FAILURE_ACTIONS failureActions = new Win32Service.SERVICE_FAILURE_ACTIONS {
                dwResetPeriod = 120 * 60,
                cActions = actionCount,
                lpsaActions = (IntPtr)pActions
            };
            result = Win32Service.ChangeServiceConfig2(
                         serviceHandle,
                         (int)Win32Service.SERVICE_CONFIGURATION_INFO_LEVEL.SERVICE_CONFIG_FAILURE_ACTIONS,
                         new IntPtr(&failureActions)
                     );
            if (result == false) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }
}