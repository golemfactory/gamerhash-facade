using Golem.Yagna;
using GolemLib.Types;
using Microsoft.Extensions.Logging;


class InvoiceEventsLoop
{
    private readonly YagnaApi _yagnaApi;
    private readonly CancellationToken _token;
    private readonly ILogger _logger;
    private DateTime _since = DateTime.MinValue;
    

    public InvoiceEventsLoop(YagnaApi yagnaApi, CancellationToken token, ILogger logger)
    {
        _yagnaApi = yagnaApi;
        _token = token;
        _logger = logger;
    }

    public async Task Start(Action<string, GolemLib.Types.PaymentStatus> UpdatePaymentStatus, Action<string, List<Payment>> updatePaymentConfirmation)
    {
        _logger.LogInformation("Starting monitoring invoice events");

        DateTime newReconnect = DateTime.Now;
        try
        {
            while (!_token.IsCancellationRequested)
            {
                var invoiceEvents = await _yagnaApi.GetInvoiceEvents(_since, _token);
                if (invoiceEvents != null && invoiceEvents.Count > 0)
                {
                    _since = invoiceEvents.Max(x => x.EventDate);

                    invoiceEvents.ForEach(async i => await UpdatesForInvoice(i, UpdatePaymentStatus, updatePaymentConfirmation));
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Payment monitoring loop failure");
        }
        finally
        {
            // UpdatePaymentStatus(GolemLib.Types.PaymentStatus.Rejected);
        }
    }

    private async Task UpdatesForInvoice(InvoiceEvent invoiceEvent, Action<string, PaymentStatus> UpdatePaymentStatus, Action<string, List<Payment>> updatePaymentConfirmation)
    {

        var invoice = await _yagnaApi.GetInvoice(invoiceEvent.InvoiceId, _token);
        if (invoice != null)
        {
            var paymentStatus = GetPaymentStatus(invoice.Status);
            if (paymentStatus == PaymentStatus.Settled)
            {
                var payments = await _yagnaApi.GetPayments(_token);
                var paymentsForRecentJob = payments
                    .Where(p => p.AgreementPayments.Exists(ap => ap.AgreementId == invoice.AgreementId))
                    .ToList();
                updatePaymentConfirmation(invoice.AgreementId, paymentsForRecentJob);
            }
            UpdatePaymentStatus(invoice.AgreementId, paymentStatus);
        }
    }

    public static GolemLib.Types.PaymentStatus GetPaymentStatus(InvoiceStatus status)
    {
        return status switch
        {
            InvoiceStatus.ISSUED => GolemLib.Types.PaymentStatus.InvoiceSent,
            InvoiceStatus.RECEIVED => GolemLib.Types.PaymentStatus.InvoiceSent,
            InvoiceStatus.ACCEPTED => GolemLib.Types.PaymentStatus.Accepted,
            InvoiceStatus.REJECTED => GolemLib.Types.PaymentStatus.Rejected,
            InvoiceStatus.FAILED => GolemLib.Types.PaymentStatus.Rejected,
            InvoiceStatus.SETTLED => GolemLib.Types.PaymentStatus.Settled,
            InvoiceStatus.CANCELLED => GolemLib.Types.PaymentStatus.Rejected,
            _ => throw new Exception($"Unknown InvoiceStatus: {status}"),
        };
    }
}
