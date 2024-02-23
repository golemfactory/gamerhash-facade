using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Avalonia.Data;
using Avalonia.Data.Converters;

using CommandLine;

namespace MockGUI.View
{
    public class HexBytesConverter : IValueConverter
    {
        public static readonly HexBytesConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null
                && value.GetType() == typeof(List<byte>)
                && targetType.IsAssignableTo(typeof(string)))
            {
                var list = (List<byte>)value;
                return System.Convert.ToHexString(list.ToArray());
            }

            // Converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }


    public class Base64Converter : IValueConverter
    {
        public static readonly Base64Converter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null
                && value.GetType() == typeof(string)
                && targetType.IsAssignableTo(typeof(string)))
            {
                var bytes = System.Convert.FromBase64String((string)value);
                return System.Convert.ToHexString(bytes);
            }

            // Converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }


        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

    }
}
