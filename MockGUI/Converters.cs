using System;
using System.Collections.Generic;
using System.Globalization;

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
            if (value.CanCast<List<byte>>()
                && targetType.IsAssignableTo(typeof(string)))
            {
                var list = value.Cast<List<byte>>();
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
}
