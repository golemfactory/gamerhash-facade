

using Golem.Model;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

using StateType = Golem.Model.ActivityState.StateType;


public interface IJobsUpdater {
    Job GetOrCreateJob(string jobId, string requestorId);
    void SetAllJobsFinished();
    StateType? GetActivityState(string joibId);
    JobStatus ResolveStatus(StateType newState, StateType? oldState);
    void UpdateActivityState(string joibId, StateType activityState);
}

class Jobs: IJobsUpdater
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

    public Job GetOrCreateJob(string jobId, string requestorId)
    {
        if(_jobs.TryGetValue(jobId, out var job))
            return job.Job;
        var newJob = new Job
        {
            Id = jobId,
            RequestorId = requestorId
        };
        _jobs[jobId] = new JobAndState(newJob, StateType.New);
        return newJob;
    }

    public StateType? GetActivityState(string joibId)
    {
        return _jobs.ContainsKey(joibId) ? _jobs[joibId].State : null;
    }

    public void UpdateActivityState(string joibId, StateType activityState)
    {
        if(_jobs.ContainsKey(joibId))
        {
            _jobs[joibId].State = activityState;
        }
    }

    public void SetAllJobsFinished()
    {
        finishAll();
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

    public JobStatus ResolveStatus(StateType newState, StateType? oldState)
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

    private void finishAll()
    {
        foreach (var jobAndStatus in _jobs.Values) {
            var job = jobAndStatus.Job;
            if (job.Status != JobStatus.Finished) {
                job.Status = JobStatus.Finished;
                job.OnPropertyChanged();
            }
        }
    }

    class JobAndState
    {
        public Job Job { get; set; }
        public StateType State { get; set;  }

        public JobAndState(Job job, StateType state)
        {
            Job = job;
            State = state;
        }
    }
}
