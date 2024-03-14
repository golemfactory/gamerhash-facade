

using System.Text.Json;

using Golem.Model;
using Golem.Yagna;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

using StateType = Golem.Model.ActivityState.StateType;


public interface IJobsUpdater
{
    Job GetOrCreateJob(string jobId, YagnaAgreement agreement);
    void SetAllJobsFinished();
    void UpdatePaymentStatus(string id, GolemLib.Types.PaymentStatus paymentStatus);
    void UpdatePaymentConfirmation(string jobId, List<Payment> payments);
    Task<List<Job>> UpdateJobs(List<ActivityState> activityStates);
}

class Jobs : IJobsUpdater
{
    private readonly Action<Job?> _setCurrentJob;
    private readonly YagnaService _yagna;
    private readonly ILogger _logger;

    /// <summary>
    /// Dictionary mapping AgreementId to aggregated `Job` with its `ActivityState`
    /// </summary>
    private readonly Dictionary<string, Job> _jobs = new();

    public Jobs(YagnaService yagna, Action<Job?> setCurrentJob, ILoggerFactory loggerFactory)
    {
        _yagna = yagna;
        _setCurrentJob = setCurrentJob;
        _logger = loggerFactory.CreateLogger(nameof(Jobs));
    }

    public Job GetOrCreateJob(string jobId, YagnaAgreement agreement)
    {
        var requestorId = agreement.Demand?.RequestorId
                ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}");
        Job? job;
        if (_jobs.TryGetValue(jobId, out var j))
            job = j;
        else
        {
            job = new Job
            {
                Id = jobId,
                RequestorId = requestorId,
                Timestamp = agreement.Timestamp
            };
            _jobs[jobId] = job;
        }

        var price = GetPriceFromAgreement(agreement);
        job.Price = price ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}");
        return job;
    }

    public void SetAllJobsFinished()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.Status != JobStatus.Finished)
            {
                job.Status = JobStatus.Finished;
                job.OnPropertyChanged();
            }
        }
    }

    public void UpdatePaymentStatus(string id, GolemLib.Types.PaymentStatus paymentStatus)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            _logger.LogInformation($"New payment status for job {job.Id}: {paymentStatus} requestor: {job.RequestorId}");
            job.PaymentStatus = paymentStatus;
            job.OnPropertyChanged();
        }
        else
        {
            _logger.LogWarning($"Failed to update payment status. Job not found: {id}");
        }
    }

    public void UpdatePaymentConfirmation(string jobId, List<Payment> payments)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            _logger.LogInformation("Payments confirmation for job {0}:", job.Id);

            job.PaymentConfirmation = payments;
            job.OnPropertyChanged();
        }
        else
        {
            _logger.LogWarning($"Failed to update payment confirmation. Job not found: {jobId}");
        }
    }

    public async Task<List<IJob>> List(DateTime since)
    {
        if(_yagna == null || _yagna.HasExited)
            return new List<IJob>();
        var activities = await _yagna.Api.GetActivities();
        var invoices = await _yagna.Api.GetInvoices(since);
        var payments = await _yagna.Api.GetPayments(since);

        var activityAgreements = new Dictionary<string, YagnaAgreement>();
        var activityStates = new Dictionary<string, ActivityState>();

        foreach(var activityId in activities)
        {
            var agreementId = await _yagna.Api.GetActivityAgreement(activityId);
            
            var (agreement, state) = await GetAgreementAndState(agreementId, activityId);

            if(agreement == null || state == null)
                continue;

            if (agreement.Timestamp < since)
                continue;

            activityAgreements[activityId] = agreement;

            activityStates[activityId] = new ActivityState
            {
                AgreementId = agreementId,
                Id = activityId,
                State = state.currentState()
            };
        }

        foreach(var activityAgreement in activityAgreements)
        {    
            var agreement = activityAgreement.Value;
            var activityId = activityAgreement.Key;
        
            if(agreement.AgreementID == null || agreement.Demand?.RequestorId == null)
                continue;

            var agreementId = agreement.AgreementID;

            if(!_jobs.ContainsKey(agreementId))
            {
                _jobs[agreementId] = GetOrCreateJob(agreementId, agreement);
            }
            var job = _jobs[agreementId];

            var agreementInvoices = invoices.Where(i => i.AgreementId == agreementId).ToList();

            var invoiceStatus = agreementInvoices.Select(a => a.Status).First();
            job.PaymentStatus = InvoiceEventsLoop.GetPaymentStatus(invoiceStatus);
            if (job.PaymentStatus == GolemLib.Types.PaymentStatus.Settled)
            {
                var paymentsForRecentJob = payments
                    .Where(p => p.AgreementPayments.Exists(ap => ap.AgreementId == agreementId))
                    .ToList();

                job.PaymentConfirmation = paymentsForRecentJob;
            }

            _jobs[agreementId] = job;
        }

        await UpdateJobs(activityStates.Select(p => p.Value).ToList());

        return _jobs.Values.Select(job => job as IJob).ToList();
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
                                    GpuPerSec = gpuSec,
                                    EnvPerSec = durationSec,
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

    public async Task<List<Job>> UpdateJobs(List<ActivityState> activityStates)
    {
        var jobs = await Task.WhenAll(activityStates
            .Select(UpdateJob));

        return jobs
            .Where(j => j != null)
            .Cast<Job>()
            .ToList();
    }

    /// <param name="activityState"></param>
    /// <param name="jobs"></param>
    /// <returns>optional current job</returns>
    private async Task<Job?> UpdateJob(ActivityState activityState)
    {
        if (activityState.AgreementId == null || activityState.Id == null)
            return null;
        var (agreement, state) = await GetAgreementAndState(activityState.AgreementId, activityState.Id);

        if (agreement?.Demand?.RequestorId == null)
        {
            _logger.LogDebug("No agreement for activity: {activitId} (agreement: {agreementId})", activityState.Id, activityState.AgreementId);
            return null;
        }

        var jobId = activityState.AgreementId;
        var job = this.GetOrCreateJob(jobId, agreement);

        if (activityState.Usage != null)
            job.CurrentUsage = GolemUsage.From(activityState.Usage);
        if (state != null)
            job.UpdateActivityState(state);

        // In case activity state wasn't properly updated by Provider or ExeUnit.
        if (agreement.State == "Terminated")
            job.Status = JobStatus.Finished;

        return job.Status == JobStatus.Finished ? null : job;
    }

    public async Task<(YagnaAgreement?, ActivityStatePair?)> GetAgreementAndState(string agreementId, string activityId)
    {
        try
        {
            var getAgreementTask = _yagna.Api.GetAgreement(agreementId);
            var getStateTask = _yagna.Api.GetState(activityId);
            await Task.WhenAll(getAgreementTask, getStateTask);
            var agreement = await getAgreementTask;
            var state = await getStateTask;

            _logger.LogInformation($"Got agreement: {agreement}");
            _logger.LogInformation($"Got activity state: {state}");

            return (agreement, state);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed get Agreement {agreementId} or Acitviy {activityId} information. {e}", agreementId, activityId, e);
            return (null, null);
        }
    }
}
