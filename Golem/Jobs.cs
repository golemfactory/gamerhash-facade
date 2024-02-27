

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

    public void UpdateUsage(string jobId, GolemUsage usage)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.CurrentUsage = usage;
        }
        else
        {
            _logger.LogWarning($"Failed to update usage. Job not found: {jobId}");
        }
    }

    public void UpdatePaymentStatus(string id, GolemLib.Types.PaymentStatus paymentStatus)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            _logger.LogInformation($"New payment status for job {job.Id}: {paymentStatus} requestor: {job.RequestorId}");
            job.PaymentStatus = paymentStatus;
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
        }
        else
        {
            _logger.LogWarning($"Failed to update payment confirmation. Job not found: {jobId}");
        }
    }

    public Task<List<IJob>> List()
    {
        return Task.FromResult(_jobs.Values.Select(job => job as IJob).ToList());
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

    public Task<List<Job>> UpdateJobs(List<ActivityState> activityStates)
    {
        return Task.FromResult(activityStates
            .Select(async state => await updateJob(state))
                .Select(task => task.Result)
                .Where(job => job != null)
                .Cast<Job>()
                .ToList());
    }

    /// <param name="activityState"></param>
    /// <param name="jobs"></param>
    /// <returns>optional current job</returns>
    private async Task<Job?> updateJob(ActivityState activityState)
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
        var job = this.GetOrCreateJob(jobId, agreement);

        if (activityState.Usage != null)
            job.CurrentUsage = GolemUsage.From(activityState.Usage);
        if (state != null)
            job.UpdateActivityState(state);

        // In case activity state wasn't properly updated by Provider or ExeUnit.
        if (agreement.State == "Terminated")
            job.Status = JobStatus.Finished;

        if (job.Status == JobStatus.Finished)
            return null;

        return job;
    }

    public async Task<(YagnaAgreement?, ActivityStatePair?)> getAgreementAndState(string agreementId, string activityId)
    {
        try
        {
            var getAgreementTask = _yagna.GetAgreement(agreementId);
            var getStateTask = _yagna.GetState(activityId);
            await Task.WhenAll(getAgreementTask, getStateTask);
            var agreement = await getAgreementTask;
            var state = await getStateTask;

            _logger.LogInformation($"Got agreement: {agreement}");
            _logger.LogInformation($"Got activity state: {state}");

            return (agreement, state);
        }
        catch (Exception e)
        {
            _logger.LogError($"Failed get Agreement {agreementId} or Acitviy {activityId} information. {e}");
            return (null, null);
        }
    }
}
