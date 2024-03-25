
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
        IJobs jobs,
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
                    await foreach (var trackingEvent in _yagna.Api.ActivityMonitorStream(token))
                    {
                        var activities = trackingEvent?.Activities ?? new List<ActivityState>();
                        List<Job> currentJobs = await UpdateJobs(jobs, activities);

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
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Activity loop cancelled");
                    return;
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

    public async Task<List<Job>> UpdateJobs(IJobs jobs, List<ActivityState> activityStates)
    {
        var result = await Task.WhenAll(
            activityStates
                .Select(async d => 
                    await jobs.UpdateJob(
                        d.Id,
                        null,
                        d.Usage!=null
                        ? GolemUsage.From(d.Usage)
                        : null)
                )
        );

        return result.ToList();
    }
}
