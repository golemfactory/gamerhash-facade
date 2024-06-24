using GolemLib.Types;

using Microsoft.Extensions.Logging;

namespace Golem
{
    public class EventsPublisher
    {
        public EventHandler<ApplicationEventArgs> ApplicationEvent;

        public void Raise(ApplicationEventArgs e)
        {
            ApplicationEvent?.Invoke(this, e);
        }
        public void RaiseAndLog(ApplicationEventArgs e, ILogger logger)
        {
            var severity = e.Severity == ApplicationEventArgs.SeverityLevel.Error ? LogLevel.Error : LogLevel.Warning;

            logger.Log(severity, $"{e.Message}");
            Raise(e);
        }
    }
}