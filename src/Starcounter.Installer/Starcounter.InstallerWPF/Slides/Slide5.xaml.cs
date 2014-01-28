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

namespace Starcounter.InstallerWPF.Slides
{
    /// <summary>
    /// Interaction logic for AddComponentsFinishedPage.xaml
    /// </summary>
    public partial class Slide5 : Grid, ISlide
    {
        public bool AutoClose { get { return false; } }

        public Slide5()
        {
            InitializeComponent();
        }

        #region ISlide Members

        public string HeaderText
        {
            get { return "Starcounter"; }
        }

        #endregion
    }
}
