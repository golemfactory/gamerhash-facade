using GolemLib.Types;

namespace Golem
{
    public class EventsPublisher
    {
        public EventHandler<ApplicationEventArgs> ApplicationEvent;

        public void Raise(ApplicationEventArgs e)
        {
            ApplicationEvent?.Invoke(this, e);
        }
    }
}