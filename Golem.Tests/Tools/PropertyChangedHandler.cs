using System.ComponentModel;

namespace Golem.IntegrationTests.Tools
{

    public class PropertyChangedHandler<T>
    {
        private Action<T> Handler { get; set; }
        private string PropertyName { get; set; }

        public PropertyChangedHandler(string propertyName, Action<T> handler)
        {
            Handler = handler;
            PropertyName = propertyName;
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

            if (value is null)
                return;

            Handler((T)value);

            if (sender is not Golem golem || e.PropertyName != PropertyName)
                return;

            Console.WriteLine($"Property has changed: {e.PropertyName} to {golem.Status}");
        }
    }
}
