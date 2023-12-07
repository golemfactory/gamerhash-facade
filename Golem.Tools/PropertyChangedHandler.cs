using System.ComponentModel;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Golem.Tools
{
    public class PropertyChangedHandler<T, V>
    {
        private Action<V?> Handler { get; set; }
        private string PropertyName { get; set; }
        private readonly ILogger _logger;

        public PropertyChangedHandler(string propertyName, Action<V?> handler, ILoggerFactory? loggerFactory = null)
        {
            Handler = handler;
            PropertyName = propertyName;

            loggerFactory = loggerFactory == null ? NullLoggerFactory.Instance : loggerFactory;
            _logger = loggerFactory.CreateLogger<PropertyChangedHandler<T, V>>();
        }

        public PropertyChangedEventHandler Subscribe()
        {
            return HandlerImpl;
        }

        private void HandlerImpl(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is null)
                return;

            var property = sender.GetType().GetProperty(PropertyName);
            if (property is null)
                return;

            var value = property.GetValue(sender);

            if (sender is not T || e.PropertyName != PropertyName)
                return;

            if (value is null)
                Handler(default);
            else if (value is V v)
                Handler(v);
            else
                Console.WriteLine("Cannot handle property changed for {0} - incorrect type: {1}", PropertyName, value);

            Console.WriteLine($"Property has changed: {e.PropertyName} to {value}");
        }
    }
}
