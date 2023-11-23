
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

using Golem.Model;
using Golem.Tools;
using Golem.Yagna.Types;

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

    public InvoiceEventsLoop(HttpClient httpClient, CancellationToken token, ILogger logger)
    {
        _httpClient = httpClient;
        _token = token;
        _logger = logger;
    }

    public async Task Start(Action<GolemLib.Types.PaymentStatus> UpdatePaymentStatus)
    {
        _logger.LogInformation("Starting monitoring invoice events");

        DateTime newReconnect = DateTime.Now;
        try
        {
            var since = DateTime.Now;

            while (!_token.IsCancellationRequested)
            {
                var timeout = 30;

                try
                {
                    var url = $"/payment-api/v1/invoiceEvents?timeout={timeout}&after_timestamp={since.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}";
                    var invoiceEventsResponse = await _httpClient.GetAsync(url);
                    if(invoiceEventsResponse.IsSuccessStatusCode)
                    {
                        var result = await invoiceEventsResponse.Content.ReadAsStringAsync();
                        if(result != null)
                        {
                            _logger.LogInformation("InvoiceEvent: {}", result);
                            var invoiceEvents = JsonSerializer.Deserialize<List<InvoiceEvent>>(result, _serializerOptions);
                            if(invoiceEvents != null && invoiceEvents.Count > 0)
                            {
                                since = invoiceEvents.OrderByDescending(x => x.EventDate).Select(x => x.EventDate).FirstOrDefault();
                            
                                foreach(var invoiceEvent in invoiceEvents)
                                {
                                    var invoice = await GetInvoice(invoiceEvent.InvoiceId);
                                    if(invoice != null)
                                    {
                                        var paymentStatus = GetPaymentStatus(invoice.Status);
                                        UpdatePaymentStatus(paymentStatus);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogError("got invoiceEvents {0}", invoiceEventsResponse);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Activity request failure");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Activity monitoring loop failure");
        }
        finally
        {
            UpdatePaymentStatus(GolemLib.Types.PaymentStatus.Rejected);
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
        var invoiceResponse = await _httpClient.GetAsync($"/payment-api/v1/invoices/{id}");

        if(invoiceResponse.IsSuccessStatusCode)
        {
            var result = await invoiceResponse.Content.ReadAsStringAsync();
            if(result != null)
            {
                var invoice = JsonSerializer.Deserialize<Invoice>(result, _serializerOptions);
                _logger.LogInformation("Invoice[{}]: {}", id, invoice);
                return invoice;
            }
        }
        return null;
    }
}





    