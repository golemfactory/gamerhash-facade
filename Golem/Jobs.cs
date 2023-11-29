

using Golem.Model;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

class Jobs
{
    private readonly Action<Job?> _setCurrentJob;
    private readonly ILogger _logger;

    /// <summary>
    /// Dictionary mapping AgreementId to aggregated `Job` with its previous `ActivityState`
    /// </summary>
    private readonly Dictionary<string, JobAndPreviousState> _jobs = new Dictionary<string, JobAndPreviousState>();

    private readonly ReaderWriterLock _jobsLock = new ReaderWriterLock();

    public Jobs(Action<Job?> setCurrentJob, ILoggerFactory loggerFactory)
    {
        _setCurrentJob = setCurrentJob;
        _logger = loggerFactory.CreateLogger(nameof(Jobs));
    }

    public void ApplyJob(Job? job, ActivityState? activityState)
    {
        if (job?.Id != null && activityState != null)
        {
            var jobAndPreviousState = new JobAndPreviousState(job, activityState);
            _jobs[job.Id] = jobAndPreviousState;
        }
        _setCurrentJob(job);
    }

    public void UpdateUsage(string jobId, GolemUsage usage)
    {
        if (_jobs.TryGetValue(jobId, out var jobAndPreviousState))
        {
            var job = jobAndPreviousState.Job;
            job.CurrentUsage = usage;
        }
        else
        {
            _logger.LogError("Job not found: {}", jobId);
        }
    }

    public void UpdatePaymentStatus(string id, GolemLib.Types.PaymentStatus paymentStatus)
    {
        if (_jobs.TryGetValue(id, out var jobAndPreviousState))
        {
            var job = jobAndPreviousState.Job;
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
        if (_jobs.TryGetValue(jobId, out var jobAndPreviousState))
        {
            var job = jobAndPreviousState.Job;
            _logger.LogInformation("Payments confirmation for job {0}:", job.Id);

            job.PaymentConfirmation = payments;
        }
        else
        {
            _logger.LogError("Job not found: {0}", jobId);
        }
    }

    public Job? Get(String jobId)
    {
        return _jobs[jobId]?.Job;
    }

    public bool Contains(String jobId)
    {
        return _jobs.ContainsKey(jobId);
    }

    public Task<List<IJob>> List()
    {
        return Task.FromResult(_jobs.Values.Select(j => j.Job as IJob).ToList());
    }

    class JobAndPreviousState
    {
        public Job Job { get; set; }

        public JobAndPreviousState(Job job, ActivityState previousState)
        {
            Job = job;
            PreviousState = previousState;
        }

        public ActivityState PreviousState { get; set; }
    }
}
