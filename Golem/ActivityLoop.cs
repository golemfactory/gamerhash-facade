
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

    public async Task Start(
        Action<Job?> setCurrentJob,
        Jobs jobs
    )
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
                        var activityStates = parseActivityStates(json);
                        activityStates = filterEmptyOrIncomplete(activityStates);
                        if (activityStates.Count == 0)
                        {
                            jobs.SetAllJobsFinished();
                            setCurrentJob(null);
                            continue;
                        }
                        List<Job> currentJob = activityStates
                            .Select(async state => await updateJob(state, jobs))
                                .Select(t => t.Result)
                                .ToList()
                                .FindAll(j => j != null)
                        // foreach (ActivityState activityState in activityStates)
                        // {
                        //     await updateJobs(activityState, jobs
                        //     );
                        // }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Activity monitoring request failure");
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
            jobs.SetAllJobsFinished();
            setCurrentJob(null);
        }
    }

    List<ActivityState> filterEmptyOrIncomplete(List<ActivityState>? activityStates)
    {
        return activityStates?.FindAll((activityState) => activityState.AgreementId != null);
    }

    /// <param name="activity_state"></param>
    /// <param name="setCurrentJob"></param>
    /// <param name="jobs"></param>
    /// <returns>optional current job</returns>
    private async Task<Job?> updateJob(
        ActivityState activity_state,
        Jobs jobs
    )
    {
        var agreement = await GetAgreement(activity_state.AgreementId);
        if (agreement?.Demand?.RequestorId == null)
        {
            _logger.LogDebug($"No agreement for activity: {activity_state.Id} (agreement: {activity_state.AgreementId})");
            jobs.SetAllJobsFinished();
            return null;
        }

        var jobId = activity_state.AgreementId;
        var job = jobs.GetOrCreateJob(jobId, agreement.Demand.RequestorId);

        if (job.Price is NotInitializedGolemPrice)
        {
            var price = GetPriceFromAgreement(agreement);
            if (price != null)
            {
                job.Price = price;
            }
        }

        var activityState = jobs.GetActivityState(jobId);
        if (activityState != null)
        {
            job.Status = jobs.ResolveStatus(activityState.Value, activity_state.State);
            jobs.UpdateActivityState(jobId, activity_state.State);
        }

        var usage = GetUsage(activity_state.Usage);
        if (usage != null)
            job.CurrentUsage = usage;
        return job;
    }

    private GolemUsage? GetUsage(Dictionary<string, decimal>? usageDict)
    {
        if (usageDict != null)
        {
            var usage = new GolemUsage
            {
                StartPrice = 1,
                GpuPerHour = usageDict["golem.usage.gpu-sec"],
                EnvPerHour = usageDict["golem.usage.duration_sec"],
                NumRequests = usageDict["ai-runtime.requests"],
            };
            return usage;
        }
        return null;
    }

    private GolemPrice? GetPriceFromAgreement(YagnaAgreement agreement)
    {
        if (agreement?.Offer?.Properties != null && agreement.Offer.Properties.TryGetValue("golem.com.usage.vector", out var usageVector))
        {
            if (usageVector != null)
            {
                var list = usageVector is JsonElement element ? element.EnumerateArray().Select(e => e.ToString()).ToList() : null;
                if (list != null)
                {
                    var gpuSecIdx = list.FindIndex(x => x.ToString().Equals("golem.usage.gpu-sec"));
                    var durationSecIdx = list.FindIndex(x => x.ToString().Equals("golem.usage.duration_sec"));
                    var requestsIdx = list.FindIndex(x => x.ToString().Equals("ai-runtime.requests"));

                    if (gpuSecIdx >= 0 && durationSecIdx >= 0 && requestsIdx >= 0)
                    {
                        if (agreement.Offer.Properties.TryGetValue("golem.com.pricing.model.linear.coeffs", out var usageVectorValues))
                        {
                            var vals = usageVectorValues is JsonElement valuesElement ? valuesElement.EnumerateArray().Select(x => x.GetDecimal()).ToList() : null;
                            if (vals != null)
                            {
                                var gpuSec = vals[gpuSecIdx];
                                var durationSec = vals[durationSecIdx];
                                var requests = vals[requestsIdx];
                                var initialPrice = vals.Last();

                                return new GolemPrice
                                {
                                    StartPrice = initialPrice,
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

    private List<ActivityState> parseActivityStates(string message)
    {
        try
        {
            var trackingEvent = JsonSerializer.Deserialize<TrackingEvent>(message, s_serializerOptions);
            var activities = trackingEvent?.Activities ?? new List<ActivityState>();
            _logger.LogDebug("Received {0} activities", activities.Count);
            return activities;
        }
        catch (JsonException e)
        {
            _logger.LogError(e, "Invalid monitoring event: {0}", message);
            throw;
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
