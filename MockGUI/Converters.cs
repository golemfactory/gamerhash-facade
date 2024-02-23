using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Avalonia.Data;
using Avalonia.Data.Converters;

using CommandLine;

using Newtonsoft.Json;

namespace MockGUI.View
{
    public class BytesListConverter : IValueConverter
    {
        public static readonly BytesListConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null
                && value.GetType() == typeof(List<byte>)
                && parameter is string target
                && targetType.IsAssignableTo(typeof(string)))
            {
                var list = (List<byte>)value;
                switch (target)
                {
                    case "hex":
                        return "0x" + System.Convert.ToHexString(list.ToArray());
                    case "string":
                        return System.Text.Encoding.UTF8.GetString(list.ToArray());
                    case "json":
                        var jsonString = System.Text.Encoding.UTF8.GetString(list.ToArray());
                        return JsonConvert.SerializeObject(jsonString, Formatting.Indented);
                    default:
                        return "0x" + System.Convert.ToHexString(list.ToArray());
                }
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
                return "0x" + System.Convert.ToHexString(bytes);
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
