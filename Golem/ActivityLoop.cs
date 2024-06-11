
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
    private readonly CancellationToken _token;

    public ActivityLoop(YagnaApi yagnaApi, IJobs jobs, CancellationToken token, EventsPublisher events, ILogger logger)
    {
        _yagnaApi = yagnaApi;
        _jobs = jobs;
        _logger = logger;
        _events = events;
        _token = token;
    }

    public Task Start()
    {
        return Task.WhenAll(Task.Run(ActivitiesLoop), Task.Run(AgreementsLoop));
    }

    public async Task ActivitiesLoop()
    {
        _logger.LogInformation("Starting monitoring activities");

        DateTime newReconnect = DateTime.Now;

        try
        {
            while (true)
            {
                _logger.LogDebug("Monitoring activities");
                newReconnect = await ReconnectDelay(newReconnect, _token);

                _token.ThrowIfCancellationRequested();

                try
                {
                    await foreach (var trackingEvent in _yagnaApi.ActivityMonitorStream(_token))
                    {
                        var activities = trackingEvent?.Activities ?? new List<ActivityState>();

                        List<Job> currentJobs = await UpdateJobs(_jobs, activities);
                        _jobs.SetCurrentJob(SelectCurrentJob(currentJobs));
                    }
                }
                catch (OperationCanceledException e)
                {
                    _events.Raise(new ApplicationEventArgs("ActivityLoop", "OperationCanceledException", ApplicationEventArgs.SeverityLevel.Error, e));
                    _logger.LogDebug("Activity loop cancelled");
                    return;
                }
                catch (Exception e)
                {
                    _events.Raise(new ApplicationEventArgs("ActivityLoop", $"Exception {e.Message}", ApplicationEventArgs.SeverityLevel.Warning, e));
                    _logger.LogError(e, "Activity monitoring request failure");
                    await Task.Delay(TimeSpan.FromSeconds(5), _token);
                }
            }
        }
        catch (Exception e)
        {
            _events.Raise(new ApplicationEventArgs("ActivityLoop", $"Exception {e.Message}", ApplicationEventArgs.SeverityLevel.Error, e));
            _logger.LogError(e, "Activity monitoring loop failure");
        }
        finally
        {
            _logger.LogInformation("Activity monitoring loop closed. Current job clenup");
            _jobs.SetCurrentJob(null);
        }
    }

    public async Task AgreementsLoop()
    {
        _logger.LogInformation("Starting monitoring agreement events");

        DateTime since = DateTime.Now;
        while (true)
        {
            try
            {
                _token.ThrowIfCancellationRequested();
                _logger.LogDebug("Checking for new Agreement events since: {}", since);

                var events = await _yagnaApi.GetAgreementEvents(since, _token);
                if (events.Count > 0)
                {
                    var agreements = events.Select(evt => evt.AgreementID).Distinct();

                    List<Job> currentJobs = await UpdateJobs(_jobs, agreements.ToList());
                    _jobs.SetCurrentJob(SelectCurrentJob(currentJobs));

                    since = events.Max(evt => evt.EventDate);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agreement loop cancelled");
                return;
            }
            catch (Exception e)
            {
                _events.Raise(new ApplicationEventArgs("Agreement", $"Exception {e.Message}", ApplicationEventArgs.SeverityLevel.Error, e));
                _logger.LogError("Error in Agreement loop: {e}", e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), _token);
            }
        }
    }

    private Job? SelectCurrentJob(List<Job> currentJobs)
    {
        if (currentJobs.Count == 0)
        {
            _logger.LogDebug("Cleaning current job field");
            return null;
        }
        else
        {
            currentJobs.Sort((job1, job2) => job1.Timestamp.CompareTo(job2.Timestamp));
            currentJobs.Reverse();

            return currentJobs[0].Active ? currentJobs[0] : null;
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
        var agreements = activityStates.Select(state => state.AgreementId).Distinct();
        return await UpdateJobs(jobs, agreements.ToList());
    }

    public async Task<List<Job>> UpdateJobs(IJobs jobs, List<string> jobIds)
    {
        // If Activity was destroyed, we can get list that doesn't contain current job.
        var currentJob = _jobs.GetCurrentJob();
        if (currentJob != null)
        {
            jobIds.Add(currentJob.Id);
        }

        _logger.LogDebug("Updating jobs: {jobs}", string.Join(", ", jobIds));

        var result = await Task.WhenAll(
            jobIds
                .Select(async jobId =>
                {
                    await jobs.UpdateJobStatus(jobId);
                    return await jobs.UpdateJobUsage(jobId);
                })
        );

        return result.ToList();
    }
}
