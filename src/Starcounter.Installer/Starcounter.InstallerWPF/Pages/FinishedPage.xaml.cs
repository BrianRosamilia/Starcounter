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
using Starcounter.InstallerWPF.Components;

namespace Starcounter.InstallerWPF.Pages
{
    /// <summary>
    /// Interaction logic for Page5.xaml
    /// </summary>
    public partial class FinishedPage : BasePage, IFinishedPage
    {

        public override bool CanGoBack {
            get {
                return false;
            }
        }

        public FinishedPage()
        {
            InitializeComponent();
        }
    }

    public class IsCheckedStartDemoConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {

            if (DependencyProperty.UnsetValue == values[0] ||
                     DependencyProperty.UnsetValue == values[1] ||
                     DependencyProperty.UnsetValue == values[2])
            {
                return Visibility.Collapsed;

            }
            bool IsEnabled = (bool)values[0];
            bool ExecuteCommand = (bool)values[1];
            bool StartWhenInstalled = (bool)values[2];


            if (IsEnabled == false) return false;

            return StartWhenInstalled;



            //foreach (bool val in values)
            //{
            //    if (val == false) return false;
            //}
            //return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {

            object[] values = new object[3];

            values[0] = null;
            values[1] = null;
            values[2] = (bool)value;

            return values;

        }
    }

    public class IsStartDemoConverterToVisibility : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {

            if (DependencyProperty.UnsetValue == values[0] ||
                DependencyProperty.UnsetValue == values[1] ||
                DependencyProperty.UnsetValue == values[2] ||
                DependencyProperty.UnsetValue == values[3])
            {
                return Visibility.Collapsed;
            }

            bool isAvailable_visualStudio2010Integration = (bool)values[0];
            bool isAvailable_visualStudio2012Integration = (bool)values[1];
            bool isAvailable_personalServer = (bool)values[2];
            bool isAvailable_systemServer = (bool)values[3];

            // SystemServer Or PersonalServer is available
            if (isAvailable_personalServer || isAvailable_systemServer)
            {
                if ("VS".Equals(parameter))
                {
                    if (isAvailable_visualStudio2010Integration ||
                        isAvailable_visualStudio2012Integration)
                    {
                        return Visibility.Visible;
                    }
                }
                else if ("Runtime".Equals(parameter))
                {
                    if (!isAvailable_visualStudio2010Integration &&
                        !isAvailable_visualStudio2012Integration)
                    {
                        return Visibility.Visible;
                    }
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
            else if (parameter == null)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;

        }

        //public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        //{

        //    object[] values = new object[3];

        //    values[0] = null;
        //    values[1] = null;
        //    values[2] = null;

        //    return values;

        //}

        #region IMultiValueConverter Members


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }



}
