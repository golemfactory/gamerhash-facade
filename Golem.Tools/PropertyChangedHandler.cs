using System.ComponentModel;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Golem.Tools
{
    public class PropertyChangedHandler<T, V> where T : INotifyPropertyChanged
    {
        private Action<V?> Handler { get; set; }
        private string PropertyName { get; set; }
        private readonly ILogger _logger;

        public V? Value { get; private set; }

        public PropertyChangedHandler(string propertyName, Action<V?> handler, ILoggerFactory? loggerFactory = null)
        {
            Handler = handler;
            PropertyName = propertyName;
            Value = default;

            loggerFactory = loggerFactory == null ? NullLoggerFactory.Instance : loggerFactory;
            _logger = loggerFactory.CreateLogger<PropertyChangedHandler<T, V>>();
        }

        public PropertyChangedHandler(string propertyName, ILoggerFactory? loggerFactory = null)
        {
            Handler = (v) => { };
            PropertyName = propertyName;
            Value = default;

            loggerFactory ??= NullLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<PropertyChangedHandler<T, V>>();
        }

        public PropertyChangedHandler<T, V> Observe(object? observed)
        {
            if (observed != null && observed is T obj)
            {
                var property = obj.GetType().GetProperty(PropertyName);
                Value = property != null ? (V?)property.GetValue(obj) : default;
                obj.PropertyChanged += Subscribe();
            }

            return this;
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
            {
                Value = default;
                Handler(default);
            }
            else if (value is V v)
            {
                Value = v;
                Handler(v);
            }
            else
                Console.WriteLine("Cannot handle property changed for {0} - incorrect type: {1}", PropertyName, value);

            Console.WriteLine($"Property has changed: {e.PropertyName} to {value}");
        }
    }
}
