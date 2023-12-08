

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
    void UpdateActivityState(string jobId, StateType activityState);
}

class Jobs : IJobsUpdater
{
    private readonly Action<Job?> _setCurrentJob;
    private readonly ILogger _logger;

    /// <summary>
    /// Dictionary mapping AgreementId to aggregated `Job` with its `ActivityState`
    /// </summary>
    private readonly Dictionary<string, JobAndState> _jobs = new();

    private readonly ReaderWriterLock _jobsLock = new ReaderWriterLock();

    public Jobs(Action<Job?> setCurrentJob, ILoggerFactory loggerFactory)
    {
        _setCurrentJob = setCurrentJob;
        _logger = loggerFactory.CreateLogger(nameof(Jobs));
    }

    public Job GetOrCreateJob(string jobId, YagnaAgreement agreement)
    {
        var requestorId = agreement.Demand?.RequestorId
                ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}");
        Job? job = null;
        if (_jobs.TryGetValue(jobId, out var j))
            job = j.Job;
        else
        {
            job = new Job
            {
                Id = jobId,
                RequestorId = requestorId,
            };
            _jobs[jobId] = new JobAndState(job, StateType.New);
        }

        var price = GetPriceFromAgreement(agreement);
        job.Price = price ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}");
        return job;
    }

    public StateType? GetActivityState(string jobId)
    {
        return _jobs.ContainsKey(jobId) ? _jobs[jobId].State : null;
    }

    public void UpdateActivityState(string jobId, StateType activityState)
    {
        var job = _jobs[jobId]?.Job ?? throw new Exception($"Unable to find job {jobId}");
        var oldActivityState = GetActivityState(jobId);
        if (oldActivityState != null)
        {
            job.Status = ResolveStatus(activityState, oldActivityState.Value);
            _jobs[jobId].State = activityState;
        }
    }

    public void SetAllJobsFinished()
    {
        foreach (var jobAndStatus in _jobs.Values)
        {
            var job = jobAndStatus.Job;
            if (job.Status != JobStatus.Finished)
            {
                job.Status = JobStatus.Finished;
                job.OnPropertyChanged();
            }
        }
    }

    public void UpdateUsage(string jobId, GolemUsage usage)
    {
        if (_jobs.TryGetValue(jobId, out var jobAndState))
        {
            var job = jobAndState.Job;
            job.CurrentUsage = usage;
        }
        else
        {
            _logger.LogError("Job not found: {}", jobId);
        }
    }

    public void UpdatePaymentStatus(string id, GolemLib.Types.PaymentStatus paymentStatus)
    {
        if (_jobs.TryGetValue(id, out var jobAndState))
        {
            var job = jobAndState.Job;
            _logger.LogInformation($"New payment status for job {job.Id}: {paymentStatus} requestor: {job.RequestorId}");
            job.PaymentStatus = paymentStatus;
        }
        else
        {
            _logger.LogError("Job not found: {}", id);
        }
    }

    public void UpdatePaymentConfirmation(string jobId, List<Payment> payments)
    {
        if (_jobs.TryGetValue(jobId, out var jobAndState))
        {
            var job = jobAndState.Job;
            _logger.LogInformation("Payments confirmation for job {0}:", job.Id);

            job.PaymentConfirmation = payments;
        }
        else
        {
            _logger.LogError("Job not found: {0}", jobId);
        }
    }

    private JobStatus ResolveStatus(StateType newState, StateType? oldState)
    {
        switch (newState)
        {
            case StateType.Deployed:
                if (oldState == StateType.Initialized)
                    return JobStatus.DownloadingModel;
                break;
            case StateType.Ready:
                return JobStatus.Computing;
            case StateType.Terminated:
                return JobStatus.Finished;
        }
        return JobStatus.Idle;
    }

    public Task<List<IJob>> List()
    {
        return Task.FromResult(_jobs.Values.Select(j => j.Job as IJob).ToList());
    }

    class JobAndState
    {
        public Job Job { get; set; }
        public StateType State { get; set; }

        public JobAndState(Job job, StateType state)
        {
            Job = job;
            State = state;
        }
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
