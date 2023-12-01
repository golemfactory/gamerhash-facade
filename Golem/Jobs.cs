

using Golem.Model;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

using StateType = Golem.Model.ActivityState.StateType;

class Jobs
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

    public void ApplyJob(Job? job, StateType? activityState)
    {
        if (job?.Id != null && activityState != null)
        {
            StateType? oldActivityState = null;
            if(_jobs.TryGetValue(job.Id, out var jobAndState)) {
                var oldJob = jobAndState.Job;
                oldActivityState = jobAndState.State;
                oldJob.Status = ResolveStatus(activityState.Value, oldActivityState);
                oldJob.OnPropertyChanged(nameof(oldJob.Status));
                job = oldJob;
            } else {
                job.Status = ResolveStatus((StateType)activityState, oldActivityState);
                _jobs[job.Id] = new JobAndState(job, (StateType)activityState);
                return;
            }
        } else {
            //TODO fix it when handling of activities list will be supported
            finishAll();
        }
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
            _logger.LogInformation("New payment status for job {}: {}", job.Id, paymentStatus);
            Console.WriteLine($"New payment status for job {job.Id}: {paymentStatus} requestor: {job.RequestorId}");
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
