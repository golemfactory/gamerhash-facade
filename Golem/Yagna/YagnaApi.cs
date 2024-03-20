using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Golem.Model;
using Golem.Tools;

using GolemLib.Types;

using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace Golem.Yagna
{
    public class YagnaApi
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptionsBuilder()
                                    .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                                    .Build();

        private readonly ILogger _logger;
        
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

        public YagnaApi(ILoggerFactory loggerFactory)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(YagnaOptionsFactory.DefaultYagnaApiUrl)
            };
            _logger = loggerFactory.CreateLogger<YagnaApi>();
        }

        public void Authorize(string key)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }


        public async Task<T> RestCall<T>(string path, CancellationToken token = default) where T : class
        {
            var response = _httpClient.GetAsync(path, token).Result;
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exception("Unauthorized call to yagna daemon - is another instance of yagna running?");
            }
            var txt = await response.Content.ReadAsStringAsync(token);
            return Deserialize<T>(txt);
        }

        public async Task<T> RestCall<T>(string path, Dictionary<string, string>? args = default, CancellationToken token = default) where T : class
        {
            if(args != null && args.Count > 0)
            {
                path += "?";
                for (int i=0; i<args.Count; ++i)
                {
                    var (k, v) = args.ElementAt(i);
                    path += $"{k}={v}" + (i==args.Count-1?"":"&");
                }
            }
            return await RestCall<T>(path, token);
        }

        public async IAsyncEnumerable<T> RestStream<T>(string path, [EnumeratorCancellation] CancellationToken token = default) where T : class
        {
            var stream = await _httpClient.GetStreamAsync(path, token);
            using StreamReader reader = new StreamReader(stream);

            while (true)
            {
                T result;
                try
                {
                    result = await Next<T>(reader, "data:", token);
                }
                catch (OperationCanceledException e)
                {
                    throw e;
                }
                catch (Exception error)
                {
                    _logger.LogError("Failed to get next stream event: {0}", error);
                    break;
                }
                yield return result;
            }
            yield break;
        }

        private async Task<T> Next<T>(StreamReader reader, string dataPrefix = "data:", CancellationToken token = default) where T : class
        {
            StringBuilder messageBuilder = new StringBuilder();

            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(token)))
            {
                if (line.StartsWith(dataPrefix))
                {
                    messageBuilder.Append(line.Substring(dataPrefix.Length).TrimStart());
                    _logger.LogDebug("got line {0}", line);
                }
                else
                {
                    _logger.LogError("Unable to deserialize message: {0}", line);
                }
            }

            return Deserialize<T>(messageBuilder.ToString());
        }
        
        internal static T Deserialize<T>(string text) where T : class
        {
            var options = new JsonSerializerOptionsBuilder()
                            .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                            .Build();

            return JsonSerializer.Deserialize<T>(text, options)
                ?? throw new Exception($"Failed to deserialize REST call reponse to type: {typeof(T).Name}");
        }

        public async Task<MeInfo> Me(CancellationToken token = default)
        {
            return await RestCall<MeInfo>("/me", token);
        }

        public async Task<YagnaAgreement> GetAgreement(string agreementId, CancellationToken token = default)
        {
            return await RestCall<YagnaAgreement>($"/market-api/v1/agreements/{agreementId}", token);
        }

        public async Task<List<YagnaAgreement>> GetAgreements(CancellationToken token = default)
        {
            return await RestCall<List<YagnaAgreement>>($"/market-api/v1/agreements", token);
        }

        public async Task<ActivityStatePair> GetState(string activityId, CancellationToken token = default)
        {
            return await RestCall<ActivityStatePair>($"/activity-api/v1/activity/{activityId}/state", token);
        }

        public async Task<string> GetActivityAgreement(string activityId, CancellationToken token = default)
        {
            return await RestCall<string>($"/activity-api/v1/activity/{activityId}/agreement", token);
        }

        public async IAsyncEnumerable<TrackingEvent> ActivityMonitorStream([EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var item in RestStream<TrackingEvent>($"/activity-api/v1/_monitor", token))
            {
                yield return item;
            }
        }

        public async Task<List<string>> GetActivities(DateTime? afterTimestamp = null, CancellationToken token = default)
        {
            var path = "/activity-api/v1/activity";
            var args = afterTimestamp != null
             ? new Dictionary<string, string> { {"afterTimestamp", afterTimestamp.Value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")} }
             : null;
            return await RestCall<List<string>>(path, args, token);
        }

        public async Task<List<Invoice>> GetInvoices(DateTime? afterTimestamp = null, CancellationToken token = default)
        {
            var path = "/payment-api/v1/invoices";
            var args = afterTimestamp != null
             ? new Dictionary<string, string> { {"afterTimestamp", afterTimestamp.Value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")} }
             : null;
                
            return await RestCall<List<Invoice>>(path, args, token);
        }

        public async Task<List<Payment>> GetPayments(DateTime? afterTimestamp = null, CancellationToken token = default)
        {
            var path = "/payment-api/v1/payments";
            var args = afterTimestamp != null
             ? new Dictionary<string, string> { {"afterTimestamp", afterTimestamp.Value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")} }
             : null;
            return await RestCall<List<Payment>>(path, args, token);
        }

        public async Task<Invoice?> GetInvoice(string id, CancellationToken token = default)
        {
            var path = $"/payment-api/v1/invoices/{id}";
            return await RestCall<Invoice>(path, token);
        }

        public async Task<List<InvoiceEvent>> GetInvoiceEvents(DateTime since, CancellationToken token = default)
        {
            const int timeout = 10;

            var url = $"/payment-api/v1/invoiceEvents?timeout[timeout]={timeout}&afterTimestamp={since.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}";
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            requestMessage.Headers.Add("X-Requestor-Events", string.Join(',', _monitorEventTypes));
            requestMessage.Headers.Add("X-Provider-Events", string.Join(',', _monitorEventTypes));

            try
            {
                var invoiceEventsResponse = await _httpClient.SendAsync(requestMessage, token);
                if (invoiceEventsResponse.IsSuccessStatusCode)
                {
                    var result = await invoiceEventsResponse.Content.ReadAsStringAsync();
                    if (result != null)
                    {
                        _logger.LogDebug("InvoiceEvent: {0}", result);
                        var invoiceEvents = JsonSerializer.Deserialize<List<InvoiceEvent>>(result, _serializerOptions);
                        return invoiceEvents ?? new List<InvoiceEvent>();
                    }
                }
                else
                {
                    _logger.LogError("Got invoiceEvents {0}", invoiceEventsResponse);
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Payment loop cancelled");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Payment request failure");
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }

            return new List<InvoiceEvent>();
        }

        public void CancelPendingRequests()
        {
            _httpClient.CancelPendingRequests();
        }
    }
}