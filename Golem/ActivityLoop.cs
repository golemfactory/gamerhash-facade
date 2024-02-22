
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Golem.Model;
using Golem.Yagna;
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

    private readonly YagnaService _yagna;
    private readonly CancellationToken _token;
    private readonly ILogger _logger;

    public ActivityLoop(YagnaService yagna, CancellationToken token, ILogger logger)
    {
        _yagna = yagna;
        _token = token;
        _logger = logger;
    }

    public async Task Start(
        Action<Job?> setCurrentJob,
        IJobsUpdater jobs,
        CancellationToken token)
    {
        _logger.LogInformation("Starting monitoring activities");

        DateTime newReconnect = DateTime.Now;

        try
        {
            while (!token.IsCancellationRequested)
            {
                _logger.LogDebug("Monitoring activities");
                var now = DateTime.Now;
                if (newReconnect > now)
                {
                    await Task.Delay(newReconnect - now);
                }
                newReconnect = now + s_reconnectDelay;
                token.ThrowIfCancellationRequested();

                try
                {
                    await foreach (var trackingEvent in _yagna.ActivityMonitorStream(token).WithCancellation(token))
                    {
                        var activities = trackingEvent?.Activities ?? new List<ActivityState>();
                        List<Job> currentJobs = await updateJobs(activities, jobs);

                        if (currentJobs.Count == 0)
                        {
                            _logger.LogDebug("Cleaning current job field");
                            setCurrentJob(null);
                            //TODO why jobs terminated as Idle stay Idle?
                            jobs.SetAllJobsFinished();
                        }
                        else if (currentJobs.Count == 1)
                        {
                            var job = currentJobs[0];
                            _logger.LogDebug($"Setting current job to {job.Id}, status {job.Status}");
                            setCurrentJob(job);
                        }
                        else
                        {
                            _logger.LogWarning($"Multiple ({currentJobs.Count}) non terminated jobs");
                            currentJobs.ForEach(job => _logger.LogWarning($"Non terminated job {job.Id}, status {job.Status}"));
                            Job? job = null;
                            if ((job = currentJobs.First(job => job.Status == JobStatus.DownloadingModel || job.Status == JobStatus.Computing)) != null)
                            {
                                setCurrentJob(job);
                            }
                            else if ((job = currentJobs.First(job => job.Status == JobStatus.Idle)) != null)
                            {
                                setCurrentJob(job);
                            }
                            else
                            {
                                setCurrentJob(null);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Activity monitoring request failure");
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
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
    )
    {
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
        if (activityState.AgreementId == null || activityState.Id == null)
            return null;
        var (agreement, state) = await getAgreementAndState(activityState.AgreementId, activityState.Id);

        if (agreement?.Demand?.RequestorId == null)
        {
            _logger.LogDebug($"No agreement for activity: {activityState.Id} (agreement: {activityState.AgreementId})");
            return null;
        }

        var jobId = activityState.AgreementId;
        var job = jobs.GetOrCreateJob(jobId, agreement);

        var usage = GetUsage(activityState.Usage);
        if (usage != null)
            job.CurrentUsage = usage;
        if (state != null)
            job.UpdateActivityState(state);

        if (job.Status == JobStatus.Finished)
            return null;

        return job;
    }

    /// TODO: replcae with GolemPrice::From after https://github.com/golemfactory/gamerhash-facade/pull/70
    /// will be merged.
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

    public async Task<(YagnaAgreement?, ActivityStatePair?)> getAgreementAndState(string agreementId, string activityId)
    {
        var getAgreementTask = GetAgreement(agreementId);
        var getStateTask = GetState(activityId);
        await Task.WhenAll(getAgreementTask, getStateTask);
        var agreement = await getAgreementTask;
        var state = await getStateTask;
        return (agreement, state);
    }

    /// TODO: Consider removing this function
    public async Task<YagnaAgreement?> GetAgreement(string agreementId)
    {
        try
        {
            var agreement = await _yagna.GetAgreement(agreementId);
            _logger.LogInformation("got agreement {0}", agreement);
            return agreement;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed GetAgreement: " + ex.Message);
            return null;
        }
    }

    /// TODO: Consider removing this function
    public async Task<ActivityStatePair?> GetState(string activityId)
    {
        try
        {
            var activityStatePair = await _yagna.GetState(activityId);
            _logger.LogInformation("got activity state {0}", activityStatePair);
            return activityStatePair;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed GetState: " + ex.Message);
            return null;
        }
    }
}
