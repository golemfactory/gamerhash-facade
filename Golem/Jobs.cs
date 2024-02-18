

using System.Text.Json;

using Golem.Model;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

using StateType = Golem.Model.ActivityState.StateType;


public interface IJobsUpdater
{
    Job GetOrCreateJob(string jobId, YagnaAgreement agreement);
    void SetAllJobsFinished();
    void UpdateActivityState(string jobId, ActivityStatePair activityState);
}

class Jobs : IJobsUpdater
{
    private readonly Action<Job?> _setCurrentJob;
    private readonly ILogger _logger;

    /// <summary>
    /// Dictionary mapping AgreementId to aggregated `Job` with its `ActivityState`
    /// </summary>
    private readonly Dictionary<string, Job> _jobs = new();

    public Jobs(Action<Job?> setCurrentJob, ILoggerFactory loggerFactory)
    {
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

    public void UpdateActivityState(string jobId, ActivityStatePair activityState)
    {
        var job = _jobs[jobId] ?? throw new Exception($"Unable to find job {jobId}");
        var currentState = activityState.currentState();
        var nextState = activityState.nextState();
        job.Status = ResolveStatus(currentState, nextState);
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

    private JobStatus ResolveStatus(StateType currentState, StateType? nextState)
    {
        switch (currentState)
        {
            case StateType.Initialized:
                if (nextState == StateType.Deployed)
                    return JobStatus.DownloadingModel;
                break;
            case StateType.Deployed:
                return JobStatus.Computing;
            case StateType.Ready:
                return JobStatus.Computing;
            case StateType.Terminated:
                return JobStatus.Finished;
        }
        return JobStatus.Idle;
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
}
