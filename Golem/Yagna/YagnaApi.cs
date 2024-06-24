using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Golem.Model;
using Golem.Tools;

using GolemLib.Types;

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
        private readonly EventsPublisher _events;

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

        public YagnaApi(ILoggerFactory loggerFactory, EventsPublisher events)
            : this(YagnaOptionsFactory.DefaultYagnaApiUrl, loggerFactory.CreateLogger<YagnaApi>(), events)
        { }

        public YagnaApi(ILogger logger, EventsPublisher events)
            : this(YagnaOptionsFactory.DefaultYagnaApiUrl, logger, events)
        { }

        public YagnaApi(string url, ILogger logger, EventsPublisher events)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(url)
            };
            _logger = logger;
            _events = events;
        }

        public void Authorize(string key)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        public async Task<T> RestGet<T>(string path, CancellationToken token = default) where T : class
        {
            return await RestGet<T>(path, new Dictionary<string, string>(), token);
        }

        /// <summary>
        /// Path should contain query parameters.
        /// </summary>
        public async Task<T> RestGet<T>(string path, Dictionary<string, string> headers, CancellationToken token = default) where T : class
        {
            return await RestCall<T>(HttpMethod.Get, path, headers, null, token);
        }

        public async Task<T> RestPost<T, C>(string path, Dictionary<string, string> headers, C content, CancellationToken token = default) where T : class where C : class
        {
            var content_ = JsonContent.Create<C>(content);
            return await RestPost<T>(path, headers, content_, token);
        }

        /// <summary>
        /// Path should contain query parameters.
        /// </summary>
        public async Task<T> RestPost<T>(string path, Dictionary<string, string> headers, JsonContent? content = null, CancellationToken token = default) where T : class
        {
            return await RestCall<T>(HttpMethod.Post, path, headers, content, token);
        }

        public async Task<T> RestDelete<T>(string path, CancellationToken token = default) where T : class
        {
            return await RestDelete<T>(path, new Dictionary<string, string>(), token);
        }

        public async Task<T> RestDelete<T>(string path, Dictionary<string, string> headers, CancellationToken token = default) where T : class
        {
            return await RestCall<T>(HttpMethod.Delete, path, headers, null, token);
        }

        private async Task<T> RestCall<T>(HttpMethod method, string path, Dictionary<string, string> headers, JsonContent? content = null, CancellationToken token = default) where T : class
        {
            using var requestMessage = new HttpRequestMessage(method, path);

            foreach (var (k, v) in headers)
                requestMessage.Headers.Add(k, v);

            if (content != null)
                requestMessage.Content = content;

            var response = _httpClient.SendAsync(requestMessage, token).Result;

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Unauthorized call to yagna daemon - is another instance of yagna running?");
                }
                throw new Exception($"Http call failed ({path}) code {response.StatusCode}: {response.ReasonPhrase}");
            }

            var txt = await response.Content.ReadAsStringAsync(token);
            return Deserialize<T>(txt);
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
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    _events.Raise(new ApplicationEventArgs("YagnaApi", $"Failed to get next stream event: {e.Message}", ApplicationEventArgs.SeverityLevel.Warning, e));
                    _logger.LogError("Failed to get next stream event: {0}", e);
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
                    _events.Raise(new ApplicationEventArgs("YagnaApi", $"Unable to deserialize message: {line}", ApplicationEventArgs.SeverityLevel.Warning, null));
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

            if (typeof(T) == typeof(EmptyResponse))
            {
                if (text == null || text == "null" || text == "")
                    return (T)(new EmptyResponse() as object);
                else
                    throw new Exception($"Deserializing: {typeof(T).Name}. Expected null, found: {text}");
            }

            return JsonSerializer.Deserialize<T>(text, options)
                ?? throw new Exception($"Failed to deserialize REST call reponse to type: {typeof(T).Name}");
        }

        public async Task<MeInfo> Me(CancellationToken token = default)
        {
            return await RestGet<MeInfo>("/me", token);
        }

        public async Task<YagnaAgreement> GetAgreement(string agreementId, CancellationToken token = default)
        {
            return await RestGet<YagnaAgreement>($"/market-api/v1/agreements/{agreementId}", token);
        }

        public async Task<List<YagnaAgreementInfo>> GetAgreements(DateTime? afterDate = null, CancellationToken token = default)
        {
            var args = afterDate != null
             ? new Dictionary<string, string> { { "afterDate", FormatTimestamp(afterDate.Value) } }
             : null;
            return await RestGet<List<YagnaAgreementInfo>>(BuildUrl("/market-api/v1/agreements", args), token);
        }

        public async Task<ActivityStatePair> GetState(string activityId, CancellationToken token = default)
        {
            return await RestGet<ActivityStatePair>($"/activity-api/v1/activity/{activityId}/state", token);
        }

        public async Task<string> GetActivityAgreement(string activityId, CancellationToken token = default)
        {
            return await RestGet<string>($"/activity-api/v1/activity/{activityId}/agreement", token);
        }

        public async IAsyncEnumerable<TrackingEvent> ActivityMonitorStream([EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var item in RestStream<TrackingEvent>($"/activity-api/v1/_monitor", token))
            {
                yield return item;
            }
        }

        public async Task<List<string>> GetActivities(string agreementId, CancellationToken token = default)
        {
            var path = "/activity-api/v1/activity";
            var args = new Dictionary<string, string> { { "agreementId", agreementId } };
            return await RestGet<List<string>>(BuildUrl(path, args), token);
        }

        public async Task<List<Invoice>> GetInvoices(DateTime? afterTimestamp = null, CancellationToken token = default)
        {
            var path = "/payment-api/v1/invoices";
            var args = afterTimestamp != null
             ? new Dictionary<string, string> { { "afterTimestamp", FormatTimestamp(afterTimestamp.Value) } }
             : null;

            return await RestGet<List<Invoice>>(BuildUrl(path, args), token);
        }

        public async Task<List<Payment>> GetPayments(DateTime? afterTimestamp = null, CancellationToken token = default)
        {
            var path = "/payment-api/v1/payments";
            var args = afterTimestamp != null
             ? new Dictionary<string, string> { { "afterTimestamp", FormatTimestamp(afterTimestamp.Value) } }
             : null;
            return await RestGet<List<Payment>>(BuildUrl(path, args), token);
        }

        public async Task<List<Payment>> GetInvoicePayments(string invoiceId, CancellationToken token = default)
        {
            var path = $"/payment-api/v1/invoices/{invoiceId}/payments";
            return await RestGet<List<Payment>>(path, token);
        }

        public async Task<Invoice> GetInvoice(string id, CancellationToken token = default)
        {
            var path = $"/payment-api/v1/invoices/{id}";
            return await RestGet<Invoice>(path, token);
        }

        public async Task<ActivityUsage> GetActivityUsage(string activityId, CancellationToken token = default)
        {
            var path = $"/activity-api/v1/activity/{activityId}/usage";
            return await RestGet<ActivityUsage>(path, token);
        }

        public async Task<YagnaAgreementEvent> GetTerminationReason(string agreementId, CancellationToken token = default)
        {
            var path = $"/market-api/v1/agreements/{agreementId}/terminate/reason";
            return await RestGet<YagnaAgreementEvent>(path, token);
        }

        public async Task<List<YagnaAgreementEvent>> GetAgreementEvents(DateTime? afterTimestamp = null, CancellationToken token = default)
        {
            var path = "/market-api/v1/agreementEvents";
            var args = afterTimestamp != null
             ? new Dictionary<string, string> { { "afterTimestamp", FormatTimestamp(afterTimestamp.Value) } }
             : null;
            return await RestGet<List<YagnaAgreementEvent>>(BuildUrl(path, args), token);
        }

        public async Task<List<InvoiceEvent>> GetInvoiceEvents(DateTime since, CancellationToken token = default)
        {
            const int timeout = 10;

            var args = new Dictionary<string, string> {
                {"timeout", $"{timeout}"},
                {"afterTimestamp", FormatTimestamp(since)}
            };

            var headers = new Dictionary<string, string> {
                {"X-Requestor-Events", string.Join(',', _monitorEventTypes)},
                {"X-Provider-Events", string.Join(',', _monitorEventTypes)}
            };

            return await RestGet<List<InvoiceEvent>>(BuildUrl("/payment-api/v1/invoiceEvents", args), headers, token);
        }

        public async Task DestroyActivity(string activityId, CancellationToken token = default)
        {
            var path = $"/activity-api/v1/activity/{activityId}";
            await RestDelete<EmptyResponse>(path, token);
        }

        public async Task TerminateAgreement(string agreementId, Reason reason, CancellationToken token = default)
        {
            var path = $"/market-api/v1/agreements/{agreementId}/terminate";
            await RestPost<EmptyResponse, Reason>(path, new Dictionary<string, string>(), reason, token);
        }


        public void CancelPendingRequests()
        {
            _httpClient.CancelPendingRequests();
        }

        private static string FormatTimestamp(DateTime dateTime)
        {
            return dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        private static string BuildUrl(string path, Dictionary<string, string>? args)
        {
            if (args != null && args.Count > 0)
            {
                path += "?";
                for (int i = 0; i < args.Count; ++i)
                {
                    var (k, v) = args.ElementAt(i);
                    path += $"{k}={v}" + (i == args.Count - 1 ? "" : "&");
                }
            }
            return path;
        }
    }
}