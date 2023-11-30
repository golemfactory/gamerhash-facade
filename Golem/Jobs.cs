

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
    private readonly Dictionary<string, JobAndState> _jobs = new Dictionary<string, JobAndState>();

    private readonly ReaderWriterLock _jobsLock = new ReaderWriterLock();

    public Jobs(Action<Job?> setCurrentJob, ILoggerFactory loggerFactory)
    {
        _setCurrentJob = setCurrentJob;
        _logger = loggerFactory.CreateLogger(nameof(Jobs));
    }

    public void ApplyJob(Job? job, StateType? activityState)
    {
        if (job?.Id != null && activityState != null)
        {
            StateType? oldActivityState = null;
            if(_jobs.TryGetValue(job.Id, out var jobAndState)) {
                var oldJob = jobAndState.Job;
                oldActivityState = jobAndState.State;
                oldJob.Status = status((StateType)activityState, oldActivityState);
                oldJob.OnPropertyChanged(nameof(oldJob.Status));
                job = oldJob;
            } else {
                job.Status = status((StateType)activityState, oldActivityState);
                _jobs[job.Id] = new JobAndState(job, (StateType)activityState);
            }
        } else {
            //TODO fix it when handling of activities list will be supported
            finishAll();
        }
        //TODO clean current job when Status == Finished ?
        _setCurrentJob(job);
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

    private JobStatus status(StateType newState, StateType? oldState = null)
    {
        if (oldState == StateType.Initialized && newState == StateType.Deployed) {
            return JobStatus.DownloadingModel;
        } else if (newState == StateType.Ready) {
            return JobStatus.Computing;
        } else if (newState == StateType.Terminated) {
            return JobStatus.Finished;
        }
        return JobStatus.Idle;
    }

    public Job? Get(String jobId)
    {
        return _jobs[jobId]?.Job;
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
