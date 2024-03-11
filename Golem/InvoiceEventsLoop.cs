using System.Globalization;
using System.Text.Json;

using Golem.Tools;

using GolemLib.Types;

using Microsoft.Extensions.Logging;


class InvoiceEventsLoop
{
    private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptionsBuilder()
                                    .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                                    .Build();

    private readonly HttpClient _httpClient;
    private readonly CancellationToken _token;
    private readonly ILogger _logger;
    private DateTime _since = DateTime.MinValue;
    private readonly string[] _monitorEventTypes =
    {
        "ISSUED",
        "RECEIVED",
        "ACCEPTED",
        "REJECTED",
        "FAILED",
        "SETTLED",
        "CANCELLED"
    };

    public InvoiceEventsLoop(HttpClient httpClient, CancellationToken token, ILogger logger)
    {
        _httpClient = httpClient;
        _token = token;
        _logger = logger;
    }

    public async Task Start(Action<string, GolemLib.Types.PaymentStatus> UpdatePaymentStatus, Action<string, List<Payment>> updatePaymentConfirmation)
    {
        _logger.LogInformation("Starting monitoring invoice events");

        DateTime newReconnect = DateTime.Now;
        try
        {
            const int timeout = 10;


            while (!_token.IsCancellationRequested)
            {
                var url = $"/payment-api/v1/invoiceEvents?timeout[timeout]={timeout}&afterTimestamp={_since.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}";
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

                requestMessage.Headers.Add("X-Requestor-Events", string.Join(',', _monitorEventTypes));
                requestMessage.Headers.Add("X-Provider-Events", string.Join(',', _monitorEventTypes));

                try
                {
                    var invoiceEventsResponse = await _httpClient.SendAsync(requestMessage, _token);
                    if (invoiceEventsResponse.IsSuccessStatusCode)
                    {
                        var result = await invoiceEventsResponse.Content.ReadAsStringAsync();
                        if (result != null)
                        {
                            _logger.LogDebug("InvoiceEvent: {0}", result);
                            var invoiceEvents = JsonSerializer.Deserialize<List<InvoiceEvent>>(result, _serializerOptions);
                            if (invoiceEvents != null && invoiceEvents.Count > 0)
                            {
                                _since = invoiceEvents.Max(x => x.EventDate);

                                invoiceEvents.ForEach(async i => await UpdatesForInvoice(i, UpdatePaymentStatus, updatePaymentConfirmation));
                            }
                        }
                    }
                    else
                    {
                        _logger.LogError("Got invoiceEvents {0}", invoiceEventsResponse);
                        await Task.Delay(TimeSpan.FromSeconds(1), _token);
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogDebug("Payment loop cancelled");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Payment request failure");
                    await Task.Delay(TimeSpan.FromSeconds(5), _token);
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

        var invoice = await GetInvoice(invoiceEvent.InvoiceId);
        if (invoice != null)
        {
            var paymentStatus = GetPaymentStatus(invoice.Status);
            UpdatePaymentStatus(invoice.AgreementId, paymentStatus);
            if (paymentStatus == PaymentStatus.Settled)
            {
                var payments = await GetPayments();
                var paymentsForRecentJob = payments
                    .Where(p => p.AgreementPayments.Exists(ap => ap.AgreementId == invoice.AgreementId))
                    .ToList();
                updatePaymentConfirmation(invoice.AgreementId, paymentsForRecentJob);
            }
        }
    }

    private GolemLib.Types.PaymentStatus GetPaymentStatus(InvoiceStatus status)
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

    private async Task<Invoice?> GetInvoice(string id)
    {
        Invoice? invoice = null;

        try
        {
            var invoiceResponse = await _httpClient.GetAsync($"/payment-api/v1/invoices/{id}", _token);

            if (invoiceResponse.IsSuccessStatusCode)
            {
                var result = await invoiceResponse.Content.ReadAsStringAsync();
                if (result != null)
                {
                    invoice = JsonSerializer.Deserialize<Invoice>(result, _serializerOptions);
                    _logger.LogInformation("Invoice[{0}]: {1}", id, invoice);
                }
            }
        }
        catch { }

        return invoice;
    }

    private async Task<List<Payment>> GetPayments()
    {
        List<Payment>? payments = null;
        try
        {
            var paymentsResponse = await _httpClient.GetAsync($"/payment-api/v1/payments", _token);

            if (paymentsResponse.IsSuccessStatusCode)
            {
                var result = await paymentsResponse.Content.ReadAsStringAsync();
                if (result != null)
                {
                    payments = JsonSerializer.Deserialize<List<Payment>>(result, _serializerOptions);
                    _logger.LogDebug("payments {0}", payments != null ? payments.SelectMany(x => x.AgreementPayments.Select(y => y.AgreementId + ": " + y.Amount)).ToList() : "(null)");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError("GetPayments error: {msg}", e.Message);
        }

        return payments ?? new List<Payment>();
    }


}
