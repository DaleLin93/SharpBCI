﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace SharpBCI.Windows
{

    internal class MultiplyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var result = 1.0;
            foreach (var t in values)
                if (t is double d)
                    result *= d;
            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>
    /// Interaction logic for ParadigmComboBox.xaml
    /// </summary>
    public partial class ParadigmComboBox 
    {

        public ParadigmComboBox() => InitializeComponent();

    }

}
