
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Golem;
using Golem.Model;
using Golem.Yagna;
using Golem.Yagna.Types;

using GolemLib.Types;

using Microsoft.Extensions.Logging;

class ActivityLoop
{
    private static readonly TimeSpan s_reconnectDelay = TimeSpan.FromSeconds(10);

    private readonly YagnaApi _yagnaApi;
    private readonly IJobs _jobs;
    private readonly ILogger _logger;
    private readonly EventsPublisher _events;

    public ActivityLoop(YagnaApi yagnaApi, IJobs jobs, EventsPublisher events, ILogger logger)
    {
        _yagnaApi = yagnaApi;
        _jobs = jobs;
        _logger = logger;
        _events = events;
    }

    public async Task Start(
        Action<Job?> setCurrentJob,
        CancellationToken token)
    {
        _logger.LogInformation("Starting monitoring activities");

        DateTime newReconnect = DateTime.Now;

        try
        {
            while (true)
            {
                _logger.LogDebug("Monitoring activities");
                newReconnect = await ReconnectDelay(newReconnect, token);

                token.ThrowIfCancellationRequested();

                try
                {
                    await foreach (var trackingEvent in _yagnaApi.ActivityMonitorStream(token))
                    {
                        var activities = trackingEvent?.Activities ?? new List<ActivityState>();
                        
                        List<Job> currentJobs = await UpdateJobs(_jobs, activities);
                        
                        var job = SelectCurrentJob(currentJobs);
                        setCurrentJob(job);
                        if(job == null)
                        {
                            // Sometimes finished jobs end up in Idle state
                            _jobs.SetAllJobsFinished();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _events.Raise(new ApplicationEventArgs($"[ActivityLoop]: OperationCanceledException"));
                    _logger.LogDebug("Activity loop cancelled");
                    return;
                }
                catch (Exception e)
                {
                    _events.Raise(new ApplicationEventArgs($"[ActivityLoop]: exception: {e.Message}"));
                    _logger.LogError(e, "Activity monitoring request failure");
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }
        }
        catch (Exception e)
        {
            _events.Raise(new ApplicationEventArgs($"[ActivityLoop]: exception: {e.Message}"));
            _logger.LogError(e, "Activity monitoring loop failure");
        }
        finally
        {
            _logger.LogInformation("Activity monitoring loop closed. Current job clenup");
            _jobs.SetAllJobsFinished();
            setCurrentJob(null);
        }
    }

    private Job? SelectCurrentJob(List<Job> currentJobs)
    {
        if (currentJobs.Count == 0)
        {
            _logger.LogDebug("Cleaning current job field");
            
            return null;
        }
        else if (currentJobs.Count == 1)
        {
            return currentJobs[0];
        }
        else
        {
            _logger.LogWarning($"Multiple ({currentJobs.Count}) non terminated jobs");
            currentJobs.ForEach(job => _logger.LogWarning($"Non terminated job {job.Id}, status {job.Status}"));

            var job = currentJobs
                .Where(job => new[] {
                                        JobStatus.DownloadingModel,
                                        JobStatus.Computing,
                                        JobStatus.Idle
                    }.Contains(job.Status))
                .OrderByDescending(job => job.Status == JobStatus.DownloadingModel)
                .ThenByDescending(job => job.Status == JobStatus.Computing)
                .ThenByDescending(job => job.Status == JobStatus.Idle)
                .FirstOrDefault();

            return job;
        }
    }

    private static async Task<DateTime> ReconnectDelay(DateTime newReconnect, CancellationToken token)
    {
        var now = DateTime.Now;
        if (newReconnect > now)
        {
            await Task.Delay(newReconnect - now, token);
        }
        newReconnect = now + s_reconnectDelay;
        return newReconnect;
    }

    public async Task<List<Job>> UpdateJobs(IJobs jobs, List<ActivityState> activityStates)
    {
        var result = await Task.WhenAll(
            activityStates
                .Select(async d => {
                        var usage = d.Usage!=null
                                ? GolemUsage.From(d.Usage)
                                : null;
                        return await jobs.UpdateJob(d.Id, null, usage);
                    }
                )
        );

        return result.ToList();
    }
}
