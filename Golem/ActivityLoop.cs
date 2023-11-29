
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Golem.Model;
using Golem.Yagna.Types;

using GolemLib.Types;

using Microsoft.Extensions.Logging;

class ActivityLoop
{
    private const string _dataPrefix = "data:";
    private static readonly TimeSpan s_reconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions s_serializerOptions = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly HttpClient _httpClient;
    private readonly CancellationToken _token;
    private readonly ILogger _logger;

    public ActivityLoop(HttpClient httpClient, CancellationToken token, ILogger logger)
    {
        _httpClient = httpClient;
        _token = token;
        _logger = logger;
    }

    public async Task Start(Action<Job?, ActivityState?> applyJob, Action<string, GolemUsage> updateUsage)
    {
        _logger.LogInformation("Starting monitoring activities");

        DateTime newReconnect = DateTime.Now;
        try
        {
            while (!_token.IsCancellationRequested)
            {
                _logger.LogInformation("Monitoring activities");
                var now = DateTime.Now;
                if (newReconnect > now)
                {
                    await Task.Delay(newReconnect - now);
                }
                newReconnect = now + s_reconnectDelay;
                if (_token.IsCancellationRequested)
                {
                    _token.ThrowIfCancellationRequested();
                }

                try
                {
                    var stream = await _httpClient.GetStreamAsync("/activity-api/v1/_monitor");
                    using StreamReader reader = new StreamReader(stream);

                    await foreach (string json in EnumerateMessages(reader).WithCancellation(_token))
                    {
                        _logger.LogInformation("got json {0}", json);
                        var activity_state = parseActivityState(json);
                        if (activity_state?.AgreementId == null)
                        {
                            _logger.LogDebug("No activity");
                            applyJob(null, null);
                            continue;
                        }
                        var agreement = await GetAgreement(activity_state.AgreementId);
                        if (agreement?.Demand?.RequestorId == null)
                        {
                            _logger.LogDebug($"No agreement for activity: {activity_state.Id} (agreement: {activity_state.AgreementId})");
                            applyJob(null, activity_state);
                            continue;
                        }

                        var job = new Job()
                        {
                            Id = activity_state.AgreementId,
                            RequestorId = agreement.Demand.RequestorId,
                        };

                        var price = GetPriceFromAgreement(agreement);
                        if (price != null)
                        {
                            job.Price = price;
                        }

                        applyJob(job, activity_state);

                        if (activity_state != null)
                        {
                            var jobId = activity_state.AgreementId;
                            var v = activity_state.Usage;
                            if (v != null)
                            {
                                var usage = new GolemUsage
                                {
                                    StartPrice = 1,
                                    GpuPerHour = v["golem.usage.gpu-sec"],
                                    EnvPerHour = v["golem.usage.duration_sec"],
                                    NumRequests = v["ai-runtime.requests"],
                                };
                                updateUsage(jobId, usage);
                            }
                        }
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
            _logger.LogInformation("Activity monitoring loop closed. Current job clenup");
            applyJob(null, null);
        }
    }

    private GolemPrice? GetPriceFromAgreement(YagnaAgreement agreement)
    {
        if (agreement.Offer.Properties.TryGetValue("golem.com.usage.vector", out var usageVector))
        {
            if (usageVector != null)
            {
                var list = usageVector is JsonElement element ? element.EnumerateArray().ToList() : null;
                if (list != null)
                {
                    var gpuSecIdx = list.FindIndex(x => x.Equals("golem.usage.gpu-sec"));
                    var durationSecIdx = list.FindIndex(x => x.Equals("golem.usage.duration_sec"));
                    var requestsIdx = list.FindIndex(x => x.Equals("ai-runtime.requests"));

                    if (gpuSecIdx >= 0 && durationSecIdx >= 0 && requestsIdx >= 0)
                    {
                        if (agreement.Offer.Properties.TryGetValue("golem.com.pricing.model.linear.coeffs", out var usageVectorValues))
                        {
                            var vals = usageVectorValues as List<decimal>;
                            if (vals != null)
                            {
                                var gpuSec = vals[gpuSecIdx];
                                var durationSec = vals[durationSecIdx];
                                var requests = vals[requestsIdx];

                                return new GolemPrice
                                {
                                    StartPrice = 1,
                                    GpuPerHour = gpuSec,
                                    EnvPerHour = durationSec,
                                    NumRequests = requests,
                                };
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    private ActivityState? parseActivityState(string message)
    {
        try
        {
            var trackingEvent = JsonSerializer.Deserialize<TrackingEvent>(message, s_serializerOptions);
            var _activities = trackingEvent?.Activities ?? new List<ActivityState>();
            if (!_activities.Any())
            {
                _logger.LogInformation("No activities");
                return null;
            }
            var _active_activities = _activities.FindAll(activity => activity.State != ActivityState.StateType.Terminated);
            if (!_active_activities.Any())
            {
                _logger.LogInformation("All activities terminated: {}", _activities);
                return null;
            }
            if (_active_activities.Count > 1)
            {
                _logger.LogWarning("Multiple non terminated activities: {}", _active_activities);
                //TODO what now?
            }

            //TODO take latest? the one with specific status?
            ActivityState _activity = _activities.First();
            if (_activity.AgreementId == null)
            {
                _logger.LogInformation("Activity without agreement id: {}", _activity);
                return null;
            }
            return _activity;
        }
        catch (JsonException e)
        {
            _logger.LogError(e, "Invalid monitoring event: {0}", message);
            return null;
        }
    }

    private ActivityState? parsePayments(string message)
    {
        try
        {
            var trackingEvent = JsonSerializer.Deserialize<TrackingEvent>(message, s_serializerOptions);
            var _activities = trackingEvent?.Activities ?? new List<ActivityState>();
            if (!_activities.Any())
            {
                _logger.LogInformation("No activities");
                return null;
            }
            var _active_activities = _activities.FindAll(activity => activity.State != ActivityState.StateType.Terminated);
            if (!_active_activities.Any())
            {
                _logger.LogInformation("All activities terminated: {}", _activities);
                return null;
            }
            if (_active_activities.Count > 1)
            {
                _logger.LogWarning("Multiple non terminated activities: {}", _active_activities);
                //TODO what now?
            }
            //TODO take latest? the one with specific status?
            ActivityState _activity = _activities.First();
            if (_activity.AgreementId == null)
            {
                _logger.LogInformation("Activity without agreement id: {}", _activity);
                return null;
            }
            return _activity;
        }
        catch (JsonException e)
        {
            _logger.LogError(e, "Invalid monitoring event: {0}", message);
            return null;
        }
    }

    private async IAsyncEnumerable<String> EnumerateMessages(StreamReader reader)
    {
        StringBuilder messageBuilder = new StringBuilder();
        while (true)
        {
            try
            {
                String? line;
                while (!String.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    if (line.StartsWith(_dataPrefix))
                    {
                        messageBuilder.Append(line.Substring(_dataPrefix.Length).TrimStart());
                        _logger.LogInformation("got line {0}", line);
                    }
                    else
                    {
                        _logger.LogError("Unable to deserialize message: {}", line);
                    }
                }
            }
            catch (Exception error)
            {
                _logger.LogError("Failed to read message: {}", error);
                break;
            }
            yield return messageBuilder.ToString();
            messageBuilder.Clear();
        }
        yield break;
    }

    public async Task<YagnaAgreement?> GetAgreement(string agreementID)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"/market-api/v1/agreements/{agreementID}");
            _logger.LogInformation("got agreement {0}", response);
            YagnaAgreement? agreement = JsonSerializer.Deserialize<YagnaAgreement>(response, s_serializerOptions) ?? null;
            return agreement;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed GetAgreementInfo: " + ex.Message);
            return null;
        }
    }
}
