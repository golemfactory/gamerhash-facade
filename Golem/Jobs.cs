using System.Text.Json;
using Golem.Model;
using Golem.Tools;
using Golem.Yagna;
using Golem.Yagna.Types;
using GolemLib;
using GolemLib.Types;
using Microsoft.Extensions.Logging;


public interface IJobs
{
    Task<Job> GetOrCreateJob(string jobId);
    void SetAllJobsFinished();
    Task<Job> UpdateJob(string activityId, Invoice? invoice, GolemUsage? usage);
}

class Jobs : IJobs
{
    private readonly Action<Job?> _setCurrentJob;
    private readonly YagnaService _yagna;
    private readonly ILogger _logger;
    private readonly Dictionary<string, Job> _jobs = new();

    public Jobs(YagnaService yagna, Action<Job?> setCurrentJob, ILoggerFactory loggerFactory)
    {
        _yagna = yagna;
        _setCurrentJob = setCurrentJob;
        _logger = loggerFactory.CreateLogger(nameof(Jobs));
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
                Timestamp = agreement.Timestamp ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}")
            };

            var price = GetPriceFromAgreement(agreement);
            job.Price = price ?? throw new Exception($"Incomplete demand of agreement {agreement.AgreementID}");

            _jobs[jobId] = job;
        }
        
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

    public async Task<List<IJob>> List(DateTime since)
    {
        if(_yagna == null || _yagna.HasExited)
            throw new Exception("Invalid state: yagna is not started");

        var agreements = await _yagna.Api.GetAgreements(since);
        var invoices = await _yagna.Api.GetInvoices(since);

        foreach(var agreement in agreements.Where(a => a!=null && a.AgreementID!=null))
        {
            var activities = await _yagna.Api.GetActivities(agreement!.AgreementID!);
            var invoice = invoices.FirstOrDefault(i => i.AgreementId == agreement.AgreementID);
            foreach(var activityId in activities)
            {
                var usage = await _yagna.Api.GetActivityUsage(activityId);
                if(usage.CurrentUsage != null)
                {
                    var price = GetPriceFromAgreementAndUsage(agreement, usage.CurrentUsage);
                    await UpdateJob(
                        activityId,
                        invoice,
                        price!=null ? new GolemUsage(price) : null);
                }
            }            
        }

        return _jobs.Values.Cast<IJob>().ToList();
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

    public async Task<Job> UpdateJob(string activityId, Invoice? invoice, GolemUsage? usage)
    {
        var agreementId = await _yagna.Api.GetActivityAgreement(activityId);
        await UpdateJobStatus(activityId);
        
        if(invoice != null)
            await UpdateJobPayment(invoice);

        // update usage only if there was any change
        if(usage != null)
            await UpdateJobUsage(agreementId);

        return await GetOrCreateJob(agreementId);
    }


    public async Task<Job> UpdateJobPayment(Invoice invoice)
    {
        var job = await GetOrCreateJob(invoice.AgreementId);

        var paymentStatus = GetPaymentStatus(invoice.Status);
        if (paymentStatus == GolemLib.Types.PaymentStatus.Settled)
        {
            var payments = await _yagna.Api.GetPayments(null);
            var paymentsForRecentJob = payments
                .Where(p => p.AgreementPayments.Exists(ap => ap.AgreementId == invoice.AgreementId) || p.ActivityPayments.Exists(ap => invoice.ActivityIds.Contains(ap.ActivityId)))
                .ToList();
            job.PaymentConfirmation = paymentsForRecentJob;
        }
        job.PaymentStatus = paymentStatus;

        return job;
    }

    private async Task<Job> UpdateJobStatus(string activityId)
    {
        var activityStatePair = await _yagna.Api.GetState(activityId);
        var agreementId = await _yagna.Api.GetActivityAgreement(activityId);
        var agreement = await _yagna.Api.GetAgreement(agreementId);
        var job = await GetOrCreateJob(agreementId);

         if (activityStatePair != null)
            job.UpdateActivityState(activityStatePair);

        // In case activity state wasn't properly updated by Provider or ExeUnit.
        if (agreement.State == "Terminated")
            job.Status = JobStatus.Finished;

        return job;
    }

    // the _usageUpdate is the usage of current activity but we need to gather usage for all activities for a given job
    private async Task<Job> UpdateJobUsage(string agreementId)
    {
        var job = await GetOrCreateJob(agreementId);
        var agreement = await _yagna.Api.GetAgreement(agreementId);
        var activities = await _yagna.Api.GetActivities(agreementId);
        
        var usage = new GolemUsage();
        foreach(var activity in activities)
        {
            var activityUsage = await _yagna.Api.GetActivityUsage(activity);
            if(activityUsage.CurrentUsage == null)
                continue;

            var price = GetPriceFromAgreementAndUsage(agreement, activityUsage.CurrentUsage);
            if(price == null)
                continue;

            usage += new GolemUsage(price);
        }

        job.CurrentUsage = usage;
        return job;
    }

    public static GolemLib.Types.PaymentStatus GetPaymentStatus(InvoiceStatus status)
    {
        return status switch
        {
            InvoiceStatus.ISSUED => GolemLib.Types.PaymentStatus.InvoiceSent,
            InvoiceStatus.RECEIVED => GolemLib.Types.PaymentStatus.InvoiceSent,
            InvoiceStatus.ACCEPTED => GolemLib.Types.PaymentStatus.Accepted,
            InvoiceStatus.REJECTED => GolemLib.Types.PaymentStatus.Rejected,
            InvoiceStatus.FAILED => GolemLib.Types.PaymentStatus.Rejected,
            InvoiceStatus.SETTLED => GolemLib.Types.PaymentStatus.Settled,
            InvoiceStatus.CANCELLED => GolemLib.Types.PaymentStatus.Rejected,
            _ => throw new Exception($"Unknown InvoiceStatus: {status}"),
        };
    }
}
