
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
        IJobsUpdater jobs
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

                        List<Job> currentJobs = await updateJobs(activityStates, jobs);
                        
                        if (currentJobs.Count == 0) {
                            _logger.LogDebug("Cleaning current job field");
                            setCurrentJob(null);
                        } else if (currentJobs.Count == 1) {
                            var job = currentJobs[0];
                            _logger.LogDebug($"Setting current job to {job.Id}, status {job.Status}");
                            setCurrentJob(job);
                        } else {
                            _logger.LogWarning($"Multiple ({currentJobs.Count}) non terminated jobs");
                            currentJobs.ForEach(job => _logger.LogWarning($"Non terminated job {job.Id}, status {job.Status}"));
                            Job? job = null;
                            if ((job = currentJobs.First(job => job.Status == JobStatus.DownloadingModel || job.Status == JobStatus.Computing)) != null) {
                                setCurrentJob(job);
                            } else if ((job = currentJobs.First(job => job.Status == JobStatus.Idle)) != null) {
                                setCurrentJob(job);
                            } else {
                                setCurrentJob(currentJobs[0]);
                            }
                        }
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

    private Task<List<Job>> updateJobs(
        List<ActivityState> activityStates,
        IJobsUpdater jobs
    ) {
        return Task.FromResult(activityStates
            .Select(async state => await updateJob(state, jobs))
                .Select(task => task.Result)
                .Where(job => job != null)
                .Cast<Job>()
                .ToList());
    }

    /// <param name="activityState"></param>
    /// <param name="setCurrentJob"></param>
    /// <param name="jobs"></param>
    /// <returns>optional current job</returns>
    private async Task<Job?> updateJob(
        ActivityState activityState,
        IJobsUpdater jobs
    )
    {
        if (activityState.AgreementId == null)
            return null;
        var agreement = await GetAgreement(activityState.AgreementId);
        if (agreement?.Demand?.RequestorId == null)
        {
            _logger.LogDebug($"No agreement for activity: {activityState.Id} (agreement: {activityState.AgreementId})");
            jobs.SetAllJobsFinished();
            return null;
        }

        var jobId = activityState.AgreementId;
        var job = jobs.GetOrCreateJob(jobId, agreement);

        var usage = GetUsage(activityState.Usage);
        if (usage != null)
            job.CurrentUsage = usage;

        var oldActivityState = jobs.GetActivityState(jobId);
        if (oldActivityState != null)
        {
            job.Status = jobs.ResolveStatus(activityState.State, oldActivityState.Value);
            jobs.UpdateActivityState(jobId, activityState.State);
        }

        if (job.Status == JobStatus.Finished)
            return null;

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
