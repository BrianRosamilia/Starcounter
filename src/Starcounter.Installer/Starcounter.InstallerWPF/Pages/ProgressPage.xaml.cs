using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Threading;
using System.Windows.Threading;
using System.IO;
using Starcounter.InstallerEngine;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Starcounter.InstallerWPF.Slides;
using Starcounter.Controls;
using System.Windows.Resources;
using Starcounter.InstallerWPF.Components;

namespace Starcounter.InstallerWPF.Pages {
    /// <summary>
    /// Interaction logic for Page4.xaml
    /// </summary>
    public partial class ProgressPage : BasePage {

        #region Win32 import

        private const uint SC_CLOSE = 0xF060;
        private const int MF_DISABLED = 0x00000002;
        private const int MF_ENABLED = 0x00000000;

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("User32.Dll")]
        public static extern IntPtr DrawMenuBar(IntPtr hwnd);

        #endregion

        #region Commands

        #region NextPage

        private void CanExecute_NextPage_Command(object sender, CanExecuteRoutedEventArgs e) {
            bool bCanExexute = this.CurrentIndex < (this.Slides.Count - 1) || this.IsInstalling == false;

            if (bCanExexute) {
                e.Handled = true;
                e.CanExecute = bCanExexute;
                //this.UpdateCloseNextButton();
            }


        }

        private void Executed_NextPage_Command(object sender, ExecutedRoutedEventArgs e) {
            if (this.CurrentIndex < (this.Slides.Count - 1)) {
                e.Handled = true;

                this.CurrentIndex++;
            }
            else {
                //if (this.CanClose)
                //{
                //    ApplicationCommands.Close.Execute(null, this);
                //}
                //else
                //{
                NavigationCommands.NextPage.Execute(null, Application.Current.MainWindow);
                //}
            }
        }

        #endregion

        #region PreviousPage

        private void CanExecute_PreviousPage_Command(object sender, CanExecuteRoutedEventArgs e) {
            if (this.CurrentIndex > 0) {
                e.Handled = true;
                e.CanExecute = true;
            }
        }

        private void Executed_PreviousPage_Command(object sender, ExecutedRoutedEventArgs e) {
            if (this.CurrentIndex > 0) {
                e.Handled = true;
                this.CurrentIndex--;
            }
        }

        #endregion

        #endregion

        #region Properties

        private bool _CanGoNext = false;
        public override bool CanGoNext {
            get {
                return base.CanGoNext && _CanGoNext;
            }
        }

        private bool _CanGoBack = false;
        public override bool CanGoBack {
            get {
                return base.CanGoBack && _CanGoBack;
            }
        }

        public override bool CanClose {
            get {
                if (this._IsInstalling == true) return false;

                return base.CanClose;

                //                return !this._IsInstalling;
            }

        }

        private bool _IsInstalling = false;
        public bool IsInstalling {
            get {
                return this._IsInstalling;
            }
            set {
                if (this._IsInstalling == value) return;
                this._IsInstalling = value;

                this.OnPropertyChanged("IsInstalling");
                this.OnPropertyChanged("CanClose");

                this.HasProgress = value;

                this.CloseSystemMenuButtonIsEnabled = !this.IsInstalling;

            }
        }

        private bool _IsExecuted = false;
        public bool IsExecuted {
            get {
                return this._IsExecuted;
            }
            set {
                if (this._IsExecuted == value) return;
                this._IsExecuted = value;
                this.OnPropertyChanged("IsInstalled");
            }
        }

        private Dispatcher _dispatcher;

        private bool _CloseSystemMenuButtonIsEnabled = true;
        private bool CloseSystemMenuButtonIsEnabled {
            get {
                return this._CloseSystemMenuButtonIsEnabled;
            }
            set {
                if (this._CloseSystemMenuButtonIsEnabled == value) return;
                this._CloseSystemMenuButtonIsEnabled = value;

                var hwnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                IntPtr menu = GetSystemMenu(hwnd, false);

                if (value) {
                    EnableMenuItem(menu, SC_CLOSE, MF_ENABLED);
                }
                else {
                    EnableMenuItem(menu, SC_CLOSE, MF_DISABLED);
                }

                // Redraw
                DrawMenuBar(hwnd);


                this.OnPropertyChanged("CloseSystemMenuButtonIsEnabled");
            }
        }

        private int _CurrentIndex = -1;
        public int CurrentIndex {
            get {
                return this._CurrentIndex;
            }
            set {
                if (this._CurrentIndex == value) return;

                this._CurrentIndex = value;

                if (this.Slides.Count > 0) {
                    this.cp_SlideShow.Content = this.Slides[this._CurrentIndex];
                }

                //this.tb_header.Text = ((ISlide)this.cp_SlideShow.Content).HeaderText;


            }
        }


        #endregion



        IList<FrameworkElement> Slides = new List<FrameworkElement>();

        public ProgressPage() {
            this._dispatcher = Dispatcher.FromThread(Thread.CurrentThread);

            Slides.Add(new Movie());
            //Slides.Add(new Slide1());

            //Slides.Add(new Slide1());
            //Slides.Add(new Slide2());
            //Slides.Add(new Slide3());
            //Slides.Add(new Slide5());


            this.Selected += new EventHandler(Page_Selected);
            this.Loaded += new RoutedEventHandler(ProgressPage_Loaded);
            InitializeComponent();
        }



        void ProgressPage_Loaded(object sender, RoutedEventArgs e) {

            if (this.CurrentIndex == -1) {
                this.CurrentIndex = 0;
            }

        }

        void Page_Selected(object sender, EventArgs e) {
            if (this.IsExecuted == false) {
                this.IsExecuted = true;
                this.Start();
            }
        }

        //private void UpdateCloseNextButton()
        //{
        //    if (this.CurrentIndex < (this.Slides.Count - 1))
        //    {
        //        this.btn_Next.Content = "_Next";
        //    }
        //    else if (this.CanClose == true || this.CurrentIndex >= (this.Slides.Count - 1))
        //    {
        //        this.btn_Next.Content = "_Close";
        //    }
        //}

        private void Start() {
            // Referring to the user configuration.
            Configuration config = (Configuration)this.DataContext;

            this.IsInstalling = true;

            ThreadPool.QueueUserWorkItem(this.StartInstallationThread, config);
        }

        /// <summary>
        /// Starts the installation thread.
        /// </summary>
        /// <param name="state">The state.</param>
        private void StartInstallationThread(object state) {



            // Starting the installation process.
            try {

                Configuration config = state as Configuration;

                PersonalServer personalServerComponent = config.Components[PersonalServer.Identifier] as PersonalServer;
                VisualStudio2012Integration vs2012IntegrationComponent = config.Components[VisualStudio2012Integration.Identifier] as VisualStudio2012Integration;
                VisualStudio2013Integration vs2013IntegrationComponent = config.Components[VisualStudio2013Integration.Identifier] as VisualStudio2013Integration;

                Starcounter.Tracking.Client.Instance.SendInstallerExecuting(Starcounter.Tracking.Client.InstallationMode.FullInstallation,
                    personalServerComponent != null && personalServerComponent.IsExecuteCommandEnabled && personalServerComponent.ExecuteCommand,
                    vs2012IntegrationComponent != null && vs2012IntegrationComponent.IsExecuteCommandEnabled && vs2012IntegrationComponent.ExecuteCommand,
                    vs2013IntegrationComponent != null && vs2013IntegrationComponent.IsExecuteCommandEnabled && vs2013IntegrationComponent.ExecuteCommand);

                config.ExecuteSettings(
                          delegate(object sender, Utilities.InstallerProgressEventArgs args) {
                              this._dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                                      new Action(delegate {
                                  this.Progress = args.Progress;
                                  this.ProgressText = args.Text;
                              }
                                                  ));
                          },
                            delegate(object sender, Utilities.MessageBoxEventArgs args) {
                                this._dispatcher.Invoke(new Action(() => {
                                    args.MessageBoxResult = WpfMessageBox.Show(args.MessageBoxText, args.Caption, args.Button, args.Icon, args.DefaultResult);
                                }));

                            }

                          );


                // Installation succeeded.
                this._dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    new Action(delegate {

                    Starcounter.Tracking.Client.Instance.SendInstallerFinish(Tracking.Client.InstallationMode.FullInstallation, true);

                    this.OnSuccess();
                }
                ));
                //((Configuration)state).RunInstallerEngine(ConstantsBank.ScGlobalSettingsIniName, null);
            }
            catch (Exception installException) {
                // Error occurred during installation.
                this._dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    new Action(delegate {

                    Starcounter.Tracking.Client.Instance.SendInstallerFinish(Tracking.Client.InstallationMode.FullInstallation, false);

                    this.OnError(installException);
                }
                ));
            }


        }


        /// <summary>
        /// Called when [success].
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="internalDatabases">The internal databases.</param>
        private void OnSuccess() {
            this.IsInstalling = false;
            this._CanGoNext = true;

            if (this.Slides.Count > 0) {
                ISlide currentClide = this.Slides[this.CurrentIndex] as ISlide;

                if (currentClide.AutoClose) {

                    // Go to next "page"
                    NavigationCommands.NextPage.Execute(null, Application.Current.MainWindow);
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Called when [error].
        /// </summary>
        /// <param name="e">The e.</param>
        private void OnError(Exception e) {

            try {
#if SIMULATE_INSTALLATION
#else
                UninstallEngine.DeleteInstallationDir(false);
#endif
                this.IsInstalling = false;
                this._CanGoNext = true;
                this._CanGoBack = true;
                NavigationCommands.GoToPage.Execute(e, this);
            }
            catch (Exception ee) {
                this.IsInstalling = false;
                this._CanGoNext = true;
                this._CanGoBack = true;
                NavigationCommands.GoToPage.Execute(ee, this);
            }

            CommandManager.InvalidateRequerySuggested();
        }



    }
}
