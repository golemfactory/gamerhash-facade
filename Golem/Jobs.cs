using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Golem.Model;
using Golem.Tools;
using Golem.Yagna;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

using static Golem.Model.ActivityState;


public interface IJobs
{
    Task<Job> GetOrCreateJob(string jobId);
    Task<Job> UpdateJob(string agreementId, Invoice? invoice, GolemUsage? usage);
    Task<Job> UpdateJobStatus(string agreementId);
    Task<Job> UpdateJobUsage(string agreementId);
    Task<Job> UpdateJobByActivity(string activityId, Invoice? invoice, GolemUsage? usage);
    Task UpdatePartialPayment(Payment payment);

    void SetCurrentJob(Job? job);
    Job? GetCurrentJob();
}

class Jobs : IJobs, INotifyPropertyChanged
{
    private readonly YagnaService _yagna;
    private readonly ILogger _logger;
    private readonly Dictionary<string, Job> _jobs = new();
    private DateTime _lastJob { get; set; }
    public Job? CurrentJob { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public Jobs(YagnaService yagna, ILoggerFactory loggerFactory)
    {
        _yagna = yagna;
        _logger = loggerFactory.CreateLogger(nameof(Jobs));
        _lastJob = DateTime.Now;
    }

    public async Task<Job> GetOrCreateJob(string jobId)
    {
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

            _jobs[jobId] = job;
            _lastJob = job.Timestamp;
        }

        return job;
    }

    public async Task<List<IJob>> List(DateTime since)
    {
        if (_yagna == null || _yagna.HasExited)
            throw new Exception("Invalid state: yagna is not started");

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
            job.Status = JobStatus.Finished;
        }
        else if (agreement.Timestamp < _lastJob)
        {
            // This is pureest form of hack. We are not able to infer from yagna API if task was really interrupted.
            // We have a few scenarios that can be misleading:
            // - Agreement is not terminated, because Provider was unable to reach Requestor.
            // - Last Activity in Agreement was destroyed by Requestor immediately before forced shutdown,
            //   but Agreement wasn't. In this case all activities are `Finished`, but Agreement is still hanging.
            // Normally we treat Agreements without Activity as `Idle`, but this means that in all these scenarios
            // Agreements from the past would be marked as `Idle`, what is definately not true.
            job.Status = JobStatus.Interrupted;
        }
        else
        {
            var activities = await _yagna.Api.GetActivities(agreementId);
            foreach (var activity in activities)
            {
                var activityStatePair = await _yagna.Api.GetState(activity);

                // Assumption: Only single activity is allowed at the same time and rest of
                // them will be terminated properly by Reqestor or Provider agent.
                // If assumption is not valid, then Job state will change in strange way depending on activities order.
                // This won't rather happen in correct cases. In incorrect case we will have incorrect state anyway.
                if (activityStatePair.currentState() != StateType.Terminated)
                    job.UpdateActivityState(activityStatePair);
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

    public void SetCurrentJob(Job? job)
    {
        _logger.LogDebug($"Attempting to set current job to {job?.Id}, status {job?.Status}");

        if (CurrentJob != job && (CurrentJob == null || !CurrentJob.Equals(job)))
        {
            if (job == null && CurrentJob != null && CurrentJob.Status != JobStatus.Finished)
            {
                _logger.LogWarning("Changing CurrentJob to null, despite it not being Finished. Setting status to Interrupted.");

                CurrentJob.Status = JobStatus.Interrupted;
                _lastJob = DateTime.Now;
            }

            _logger.LogInformation("New job. Id: {0}, Requestor id: {1}, Status: {2}", job?.Id, job?.RequestorId, job?.Status);

            CurrentJob = job;
            OnPropertyChanged(nameof(CurrentJob));
        }
        else
        {
            _logger.LogDebug($"Job has not changed ({job?.Id}).");
        }
    }
}
