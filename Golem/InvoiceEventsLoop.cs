using Golem.Yagna;

using GolemLib.Types;

using Microsoft.Extensions.Logging;


class InvoiceEventsLoop
{
    private readonly YagnaApi _yagnaApi;
    private readonly CancellationToken _token;
    private readonly ILogger _logger;
    private readonly IJobs _jobs;


    public InvoiceEventsLoop(YagnaApi yagnaApi, CancellationToken token, ILogger logger, IJobs jobs)
    {
        _yagnaApi = yagnaApi;
        _token = token;
        _logger = logger;
        _jobs = jobs;
    }

    public Task Start()
    {
        return Task.WhenAll(PaymentsLoop(), InvoiceLoop());
    }

    public async Task InvoiceLoop()
    {
        _logger.LogInformation("Starting monitoring invoice events");

        DateTime since = DateTime.Now;
        while (!_token.IsCancellationRequested)
        {
            try
            {
                var invoiceEvents = await _yagnaApi.GetInvoiceEvents(since, _token);
                if (invoiceEvents != null && invoiceEvents.Count > 0)
                {
                    foreach (var invoiceEvent in invoiceEvents.OrderBy(e => e.EventDate))
                    {
                        await UpdatesForInvoice(invoiceEvent);
                        since = invoiceEvent.EventDate;
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

    public async Task PaymentsLoop()
    {
        _logger.LogInformation("Starting monitoring payments");

        DateTime since = DateTime.Now;
        while (!_token.IsCancellationRequested)
        {
            try
            {
                var payments = await _yagnaApi.GetPayments(since, _token);
                foreach (var payment in payments.OrderBy(pay => pay.Timestamp))
                {
                    await _jobs.UpdatePartialPayment(payment);
                    since = payment.Timestamp;
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Payments loop cancelled");
                return;
            }
            catch (Exception e)
            {
                _logger.LogError("Error in payments loop: {e}", e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), _token);
            }
        }
    }

    private async Task UpdatesForInvoice(InvoiceEvent invoiceEvent)
    {
        var invoice = await _yagnaApi.GetInvoice(invoiceEvent.InvoiceId, _token);

        _logger.LogDebug("Update Invoice info for Job: {}, status: {}", invoice.AgreementId, invoice.Status);
        await _jobs.UpdateJob(invoice.AgreementId, invoice, null);
    }
}
