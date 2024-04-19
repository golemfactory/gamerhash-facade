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
        return Task.WhenAll(Task.Run(PaymentsLoop), Task.Run(InvoiceLoop));
    }

    public async Task InvoiceLoop()
    {
        _logger.LogInformation("Starting monitoring invoice events");

        DateTime since = DateTime.Now;
        while (true)
        {
            try
            {
                _token.ThrowIfCancellationRequested();
                _logger.LogDebug("Checking for new invoice events since: {}", since);

                var invoiceEvents = await _yagnaApi.GetInvoiceEvents(since, _token);
                if (invoiceEvents != null && invoiceEvents.Count > 0)
                {
                    foreach (var invoiceEvent in invoiceEvents.OrderBy(e => e.EventDate))
                    {
                        _logger.LogDebug("Invoice event for: {}", invoiceEvent.InvoiceId);

                        await UpdatesForInvoice(invoiceEvent);
                        since = invoiceEvent.EventDate;
                    }
                }
            }
            catch (OperationCanceledException)
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
        while (true)
        {
            try
            {

                _token.ThrowIfCancellationRequested();
                _logger.LogDebug("Checking for new payments since: {}", since);

                var payments = await _yagnaApi.GetPayments(since, _token);
                foreach (var payment in payments.OrderBy(pay => pay.Timestamp))
                {
                    _logger.LogDebug("New Payment, id: {}", payment.PaymentId);

                    await _jobs.UpdatePartialPayment(payment);
                    since = payment.Timestamp;
                }
            }
            catch (OperationCanceledException)
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
