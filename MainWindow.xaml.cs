using System.Windows;
using System.Windows.Data;
using System.Globalization;
using System;

namespace SimpleRAG;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}