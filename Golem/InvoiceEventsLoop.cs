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


    public InvoiceEventsLoop(YagnaApi yagnaApi, CancellationToken token, ILogger logger, IJobs jobs)
    {
        _yagnaApi = yagnaApi;
        _token = token;
        _logger = logger;
        _jobs = jobs;
    }

    public async Task Start()
    {
        _logger.LogInformation("Starting monitoring invoice events");

        DateTime newReconnect = DateTime.Now;

        await Task.Yield();

        while (!_token.IsCancellationRequested)
        {
            try
            {
                var invoiceEvents = await _yagnaApi.GetInvoiceEvents(_since, _token);
                if (invoiceEvents != null && invoiceEvents.Count > 0)
                {
                    _since = invoiceEvents.Max(x => x.EventDate);

                    foreach (var invoiceEvent in invoiceEvents.OrderBy(e => e.EventDate))
                    {
                        await UpdatesForInvoice(invoiceEvent);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Invoice events loop cancelled");
                return;
            }
            catch (Exception e)
            {
                _logger.LogError("Error in invoice events loop: {e}", e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), _token);
            }
        }
    }

    private async Task UpdatesForInvoice(InvoiceEvent invoiceEvent)
    {
        var invoice = await _yagnaApi.GetInvoice(invoiceEvent.InvoiceId, _token);

        _logger.LogDebug("Update Invoice info for Job: {}, status: {}", invoice.AgreementId, invoice.Status);
        foreach (var activityId in invoice.ActivityIds)
            await _jobs.UpdateJob(activityId, invoice, null);
    }
}
