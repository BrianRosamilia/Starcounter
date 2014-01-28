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
using System.Windows.Shapes;
using System.Windows.Resources;
using System.IO;
using System.ComponentModel;
using System.Windows.Media.Animation;

namespace Starcounter.InstallerWPF.Slides {
    /// <summary>
    /// Interaction logic for AddComponentsFinishedPage.xaml
    /// </summary>
    public partial class Movie : Grid, ISlide, INotifyPropertyChanged {

        private Uri MovieUri { get; set; }

        public bool AutoClose {
            get {
                return !this.MediaCanBePlayed;
            }
        }

        private bool _MediaCanBePlayed = true;
        public bool MediaCanBePlayed {
            get {
                return this._MediaCanBePlayed;
            }
            protected set {
                if (this._MediaCanBePlayed == value) return;
                this._MediaCanBePlayed = value;
                this.OnPropertyChanged("MediaCanBePlayed");
            }
        }

        public Movie() {
            InitializeComponent();

            this.Loaded += new RoutedEventHandler(Movie_Loaded);
            this.mediaElement.MediaEnded += new RoutedEventHandler(mediaElement_MediaEnded);
            this.mediaElement.MediaFailed += new EventHandler<ExceptionRoutedEventArgs>(mediaElement_MediaFailed);
            this.Unloaded += new RoutedEventHandler(Movie_Unloaded);

            this.PropertyChanged += Movie_PropertyChanged;
        }

        void Movie_PropertyChanged(object sender, PropertyChangedEventArgs e) {

            if (e.PropertyName == "MediaCanBePlayed") {
                if (this.MediaCanBePlayed) {
                    StopAnimation();
                }
                else {
                    StartAnimation();
                }
            }

        }

        void mediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e) {
            this.mediaElement.Visibility = System.Windows.Visibility.Collapsed;
            this.MediaCanBePlayed = false;
            //this.nomovieimage.Visibility = System.Windows.Visibility.Visible;
        }

        void Movie_Unloaded(object sender, RoutedEventArgs e) {
            if (this.MovieUri != null && File.Exists(this.MovieUri.LocalPath)) {
                File.Delete(this.MovieUri.LocalPath);
            }
        }

        void Movie_Loaded(object sender, RoutedEventArgs e) {
            if (this.MediaCanBePlayed) {
                this.MovieUri = this.prepareVideo();
                if (this.MovieUri == null) {
                    this.MediaCanBePlayed = false;
                }
                else {
                    this.mediaElement.Source = this.MovieUri;
                    this.mediaElement.Play();
                }
            }
            else {
                // TODO: Show picture/text
            }
        }

        void mediaElement_MediaEnded(object sender, RoutedEventArgs e) {
            NavigationCommands.NextPage.Execute(null, this);
            CommandManager.InvalidateRequerySuggested();
        }

        private Uri prepareVideo() {

            try {
                StreamResourceInfo sri = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/../resources/FullStory.wmv"));

                Stream resFilestream = sri.Stream;

                if (resFilestream.Length == 0) return null;

                string tempFileName = System.IO.Path.GetTempFileName();

                // Prepare file name 
                tempFileName = System.IO.Path.ChangeExtension(tempFileName, "wmv");

                // Remove any escaped characters like "%20" (FileStream dosent work so good with escaped pathes)
                tempFileName = Uri.UnescapeDataString(tempFileName);

                // Create the Uri to the tempFile
                Uri movieUri = new Uri(tempFileName);

                if (resFilestream != null) {
                    using (BinaryReader br = new BinaryReader(resFilestream)) {
                        using (FileStream fs = new FileStream(movieUri.LocalPath, FileMode.Create)) {
                            using (BinaryWriter bw = new BinaryWriter(fs)) {
                                byte[] ba = new byte[resFilestream.Length];
                                resFilestream.Read(ba, 0, ba.Length);
                                bw.Write(ba);
                                bw.Close();
                            }
                            fs.Close();
                        }
                        br.Close();
                    }
                    resFilestream.Close();
                }
                return movieUri;
            }
            catch (Exception) {
                return null;
            }

        }


        private void StartAnimation() {
            Storyboard Element_Storyboard = (Storyboard)PART_Canvas.FindResource("canvasAnimation");
            Element_Storyboard.Begin(PART_Canvas, true);
        }

        private void StopAnimation() {
            Storyboard Element_Storyboard = (Storyboard)PART_Canvas.FindResource("canvasAnimation");
            Element_Storyboard.Stop(PART_Canvas);
        }

        #region ISlide Members

        public string HeaderText {
            get { return "Movie"; }
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string fieldName) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(fieldName));
            }
        }

        #endregion

    }
}
