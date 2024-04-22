using Golem;
using Golem.Yagna;
using GolemLib.Types;
using Microsoft.Extensions.Logging;


class InvoiceEventsLoop
{
    private readonly YagnaApi _yagnaApi;
    private readonly CancellationToken _token;
    private readonly ILogger _logger;
    private readonly IJobs _jobs;
    private DateTime _since = DateTime.MinValue;
    private readonly EventsPublisher _events;
    

    public InvoiceEventsLoop(YagnaApi yagnaApi, IJobs jobs, CancellationToken token, EventsPublisher events, ILogger logger)
    {
        _yagnaApi = yagnaApi;
        _token = token;
        _logger = logger;
        _jobs = jobs;
        _events = events;
    }

    public async Task Start()
    {
        _logger.LogInformation("Starting monitoring invoice events");

        DateTime newReconnect = DateTime.Now;

        await Task.Yield();
        
        while (true)
        {
            _token.ThrowIfCancellationRequested();
            try
            {
                var invoiceEvents = await _yagnaApi.GetInvoiceEvents(_since, _token);
                if (invoiceEvents != null && invoiceEvents.Count > 0)
                {
                    _since = invoiceEvents.Max(x => x.EventDate);

                    foreach(var invoiceEvent in invoiceEvents.OrderBy(e => e.EventDate))
                    {
                        await UpdatesForInvoice(invoiceEvent);
                    }
                }
            }
            catch(OperationCanceledException e)
            {
                _events.Raise(new ApplicationEventArgs("InvoiceEventsLoop", $"OperationCanceledException", ApplicationEventArgs.SeverityLevel.Error, e));
                _logger.LogInformation("Invoice events loop cancelled");
                return;
            }
            catch(Exception e)
            {
                _events.Raise(new ApplicationEventArgs("InvoiceEventsLoop", $"Exception {e.Message}", ApplicationEventArgs.SeverityLevel.Warning, e));
                _logger.LogError("Error in invoice events loop: {e}", e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), _token);
            }
        }        
    }

    private async Task UpdatesForInvoice(InvoiceEvent invoiceEvent)
    {
        var invoice = await _yagnaApi.GetInvoice(invoiceEvent.InvoiceId, _token);

        foreach(var activityId in invoice.ActivityIds)
            await _jobs.UpdateJob(activityId, invoice, null);
    }
}
