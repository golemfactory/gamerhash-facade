using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AsyncKeyedLock;

using Golem.Model;
using Golem.Tools;
using Golem.Yagna;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

using static Golem.Model.ActivityState;

namespace Golem;

public interface IJobs
{
    Task<Job> GetOrCreateJob(string jobId);
    Task<Job> UpdateJob(string agreementId, Invoice? invoice, GolemUsage? usage);
    Task<Job> UpdateJobStatus(string agreementId);
    Task<Job> UpdateJobUsage(string agreementId);
    Task<Job> UpdateJobByActivity(string activityId, Invoice? invoice, GolemUsage? usage);
    Task UpdatePartialPayment(Payment payment);

    void SetCurrentJob(Job? job);
    Job? SelectCurrentJob(List<Job> currentJobs);
    Job? GetCurrentJob();
    void CleanupJobs();
}

class Jobs : IJobs, INotifyPropertyChanged
{
    private readonly YagnaService _yagna;
    private readonly ILogger _logger;
    private readonly EventsPublisher _events;
    private readonly Dictionary<string, Job> _jobs = new();
    private DateTime _lastJobTimestamp { get; set; }
    public Job? CurrentJob { get; private set; }

    private readonly AsyncKeyedLocker<string> _jobLocker = new(o =>
    {
        o.PoolSize = 20; // this is NOT a concurrency limit
        o.PoolInitialFill = 1;
    });


    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public Jobs(YagnaService yagna, EventsPublisher events, ILoggerFactory loggerFactory)
    {
        _yagna = yagna;
        _logger = loggerFactory.CreateLogger<Jobs>();
        _events = events;
        _lastJobTimestamp = DateTime.Now;
    }

    public async Task<Job> GetOrCreateJob(string jobId)
    {
        using var locker = await _jobLocker.LockAsync(jobId);

        if (!_jobs.TryGetValue(jobId, out Job? job))
        {
            var agreement = await _yagna.Api.GetAgreement(jobId);
            var requestorId = agreement.Demand?.RequestorId
                ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}");

            job = new Job
            {
                Id = jobId,
                RequestorId = requestorId,
                Timestamp = agreement.Timestamp ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}"),
                Logger = _logger,
            };

            var price = GetPriceFromAgreement(agreement);
            job.Price = price ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}");

            _logger.LogDebug($"Created new job object {job.Id}");
            _jobs[jobId] = job;
        }

        return job;
    }

    public async Task<List<IJob>> List(DateTime since)
    {
        if (_yagna == null || _yagna.HasExited)
            throw new Exception("Invalid state: yagna is not started");

        _logger.LogDebug($"Listing Jobs since: {since}");

        var agreementInfos = await _yagna.Api.GetAgreements(since);
        var invoices = await _yagna.Api.GetInvoices(since);

        foreach (var agreementInfo in agreementInfos.Where(a => a != null && a.AgreementID != null))
        {
            await UpdateJobStatus(agreementInfo.Id);
            await UpdateJobUsage(agreementInfo.Id);

            var invoice = invoices.FirstOrDefault(i => i.AgreementId == agreementInfo.Id);
            if (invoice != null)
                await UpdateJobPayment(invoice);
        }

        return _jobs.Values.Where(job => job.Timestamp >= since).OrderByDescending(job => job.Timestamp).Cast<IJob>().ToList();
    }

    private GolemPrice? GetPriceFromAgreement(YagnaAgreement agreement)
    {
        if (agreement?.Offer?.Properties != null && agreement.Offer.Properties.TryGetValue("golem.com.pricing.model.linear.coeffs", out var usageVectorValues))
        {
            var vals = usageVectorValues is JsonElement valuesElement ? valuesElement.EnumerateArray().Select(x => x.GetDecimal()).ToList() : null;
            if (vals != null)
            {
                return GetPriceFromAgreementAndUsage(agreement, vals);
            }
        }

        return null;
    }

    private GolemPrice? GetPriceFromAgreementAndUsage(YagnaAgreement agreement, List<decimal> vals)
    {
        if (agreement?.Offer?.Properties != null
            && agreement.Offer.Properties.TryGetValue("golem.com.usage.vector", out var usageVector)
            && usageVector != null)
        {
            var list = usageVector is JsonElement element ? element.EnumerateArray().Select(e => e.ToString()).ToList() : null;
            if (list != null)
            {
                var gpuSecIdx = list.FindIndex(x => x.ToString().Equals("golem.usage.gpu-sec"));
                var durationSecIdx = list.FindIndex(x => x.ToString().Equals("golem.usage.duration_sec"));
                var requestsIdx = list.FindIndex(x => x.ToString().Equals("ai-runtime.requests"));

                if (gpuSecIdx >= 0 && durationSecIdx >= 0 && requestsIdx >= 0)
                {
                    var gpuSec = vals[gpuSecIdx];
                    var durationSec = vals[durationSecIdx];
                    var requests = vals[requestsIdx];
                    var initialPrice = vals.Last();

                    return new GolemPrice
                    {
                        StartPrice = initialPrice,
                        GpuPerSec = gpuSec,
                        EnvPerSec = durationSec,
                        NumRequests = requests,
                    };
                }
            }
        }
        return null;
    }

    public Job? GetCurrentJob()
    {
        return (Job?)CurrentJob;
    }

    public async Task<Job> UpdateJobByActivity(string activityId, Invoice? invoice, GolemUsage? usage)
    {
        var agreementId = await _yagna.Api.GetActivityAgreement(activityId);
        return await UpdateJob(agreementId, invoice, usage);
    }

    public async Task<Job> UpdateJob(string agreementId, Invoice? invoice, GolemUsage? usage)
    {
        await UpdateJobStatus(agreementId);

        if (invoice != null)
            await UpdateJobPayment(invoice);

        // update usage only if there was any change
        if (usage != null)
            await UpdateJobUsage(agreementId);

        return await GetOrCreateJob(agreementId);
    }


    public async Task<Job> UpdateJobPayment(Invoice invoice)
    {
        var job = await GetOrCreateJob(invoice.AgreementId);

        var payments = await _yagna.Api.GetInvoicePayments(invoice.InvoiceId);
        var paymentsForRecentJob = payments
            .Where(p => p.AgreementPayments.Exists(ap => ap.AgreementId == invoice.AgreementId) || p.ActivityPayments.Exists(ap => invoice.ActivityIds.Contains(ap.ActivityId)))
            .ToList();
        job.PaymentConfirmation = paymentsForRecentJob;
        job.PaymentStatus = job.EvaluatePaymentStatus(Job.IntoPaymentStatus(invoice.Status));

        return job;
    }

    public async Task UpdatePartialPayment(Payment payment)
    {
        foreach (var activityPayment in payment.ActivityPayments)
        {
            var agreementId = await this._yagna.Api.GetActivityAgreement(activityPayment.ActivityId);
            var job = await GetOrCreateJob(agreementId);
            job.AddPartialPayment(payment);
        }

        foreach (var agreementPayment in payment.AgreementPayments)
        {
            var job = await GetOrCreateJob(agreementPayment.AgreementId);
            job.AddPartialPayment(payment);
        }
    }

    public async Task<Job> UpdateJobStatus(string agreementId)
    {
        var job = await GetOrCreateJob(agreementId);
        var agreement = await _yagna.Api.GetAgreement(agreementId);

        // Agreement state has precedens over individual activity states.
        if (agreement.State == "Terminated")
        {
            var termination = await _yagna.Api.GetTerminationReason(agreementId);
            job.Status = Job.ResolveTerminationReason(termination.Code);
        }
        else if (agreement.Timestamp < _lastJobTimestamp)
        {
            // This is pureest form of hack. We are not able to infer from yagna API if task was really interrupted.
            // We have a few scenarios that can be misleading:
            // - Agreement is not terminated, because Provider was unable to reach Requestor.
            // - Last Activity in Agreement was destroyed by Requestor immediately before forced shutdown,
            //   but Agreement wasn't. In this case all activities are `Finished`, but Agreement is still hanging.
            // Normally we treat Agreements without Activity as `Idle`, but this means that in all these scenarios
            // Agreements from the past would be marked as `Idle`, what is definately not true.
            job.Status = JobStatus.Interrupted;

            try
            {
                var reason = new Reason("Interrupted", "Agreement was interrupted and never terminated afterwards");
                await _yagna.Api.TerminateAgreement(job.Id, reason);
            }
            catch (Exception e)
            {
                _events.RaiseAndLog(new ApplicationEventArgs("Jobs", $"Failed to terminate hanging Agreement {e.Message}", ApplicationEventArgs.SeverityLevel.Warning, e), _logger);
            }
        }
        else
        {
            bool allTerminated = true;
            var activities = await _yagna.Api.GetActivities(agreementId);
            foreach (var activity in activities)
            {
                var activityStatePair = await _yagna.Api.GetState(activity);

                // Assumption: Only single activity is allowed at the same time and rest of
                // them will be terminated properly by Reqestor or Provider agent.
                // If assumption is not valid, then Job state will change in strange way depending on activities order.
                // This won't rather happen in correct cases. In incorrect case we will have incorrect state anyway.
                if (activityStatePair.currentState() != StateType.Terminated)
                {
                    _logger.LogDebug($"Activity {activity} state: ({activityStatePair.currentState()} -> {activityStatePair.nextState()}).");

                    allTerminated = false;
                    job.UpdateActivityState(activityStatePair);
                }
                else
                {
                    if (activityStatePair.reason != null || activityStatePair.error_message != null)
                        _logger.LogInformation($"Activity {activity}: Reason: {activityStatePair.reason}, error: {activityStatePair.error_message}.");
                }
            }

            // Agreement is still not terminated, so we should be in Idle state.
            if (allTerminated)
            {
                // This solves scenario when Requestor yagna daemon disappeared and
                // our Provider agent is unable to terminate Agreement.
                // Note that Provider will be able to pick up next task anyway.
                if (job.StartIdling())
                {
                    // We have to spawn task with delayed execution, because there might be
                    // no trigger which can update task status afterwards.
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(101));
                        await UpdateJobStatus(job.Id);
                        SetCurrentJob(SelectCurrentJob(_jobs.Values.ToList()));
                    });
                }

                if (job.IdlingTimeout())
                {
                    _logger.LogInformation($"Job {job.Id} Interrupted because of exceded no activity timeout.");

                    job.Status = JobStatus.Interrupted;
                    job.StopIdling();
                    _lastJobTimestamp = DateTime.Now;
                }
                else
                {
                    job.Status = JobStatus.Idle;
                }
            }
            else
            {
                job.StopIdling();
            }
        }

        return job;
    }

    // the _usageUpdate is the usage of current activity but we need to gather usage for all activities for a given job
    public async Task<Job> UpdateJobUsage(string agreementId)
    {
        var job = await GetOrCreateJob(agreementId);
        var agreement = await _yagna.Api.GetAgreement(agreementId);
        var activities = await _yagna.Api.GetActivities(agreementId);

        var usage = new GolemUsage();
        foreach (var activity in activities)
        {
            var activityUsage = await _yagna.Api.GetActivityUsage(activity);
            if (activityUsage.CurrentUsage == null)
                continue;

            var price = GetPriceFromAgreementAndUsage(agreement, activityUsage.CurrentUsage);
            if (price == null)
                continue;

            usage += new GolemUsage(price);
        }

        job.CurrentUsage = usage;
        return job;
    }

    public Job? SelectCurrentJob(List<Job> currentJobs)
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

    public void SetCurrentJob(Job? job)
    {
        _logger.LogDebug($"Attempting to set current job to {job?.Id}, status {job?.Status}");

        if (CurrentJob != job)
        {
            if (job != null)
                _logger.LogInformation("New job. Id: {0}, Requestor id: {1}, Status: {2}", job?.Id, job?.RequestorId, job?.Status);
            else
                _logger.LogInformation($"Last job {CurrentJob?.Id} finished. Setting to null.");

            CurrentJob = job;
            _lastJobTimestamp = CurrentJob != null ? CurrentJob.Timestamp : DateTime.Now;
            OnPropertyChanged(nameof(CurrentJob));
        }
        else
        {
            _logger.LogDebug($"Job has not changed ({job?.Id}).");
        }
    }

    public void CleanupJobs()
    {
        foreach (var job in _jobs.Values)
        {
            // If yagna was stopped, we can get no chance of changing status based on
            // REST api reponses, but at this point we are sure that CurrentJob was interrupted.
            if (job != null && job.Status != JobStatus.Finished)
            {
                _logger.LogDebug($"Job cleanup: changing {job.Id} status to Interrupted");
                job.Status = JobStatus.Interrupted;
            }
        }
    }
}
