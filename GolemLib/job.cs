using System.ComponentModel;
using GolemLib.Types;

namespace GolemLib;

public interface Job : INotifyPropertyChanged
{
    /// <summary>
    /// Internally should be implemented as Agreement id.
    /// </summary>
    public string Id { get; init; }
    /// <summary>
    /// Requestor Node identification in Golem network.
    /// </summary>
    public string RequestorId { get; }
    /// <summary>
    /// Price vector for which this job was initialized.
    /// Note that even if you change `IGolem.Price` this field won't be
    /// affected, only new jobs will be served using new price.
    /// </summary>
    public GolemPrice Price { get; init; }
    public JobStatus Status { get; }
    /// <summary>
    /// Property is set after Provider sends Invoice to Requestor.
    /// </summary>
    public PaymentStatus? PaymentStatus { get; }

    /// <summary>
    /// Calling this function in other state than `PaymentStatus.Settled`, will
    /// result in exception.
    /// 
    /// TODO: We can have multiple `Payment` confirmation structs.
    /// </summary>
    /// <returns></returns>
    public Task<Payment> PaymentConfirmation();
    /// <summary>
    /// Get usage counters during task execution, what allows to estimate
    /// reward for the job done.
    /// </summary>
    /// <returns></returns>
    public Task<GolemUsage> CurrentUsage();
    /// <summary>
    /// Get amount that should be paid for the task until this point in time.
    /// After task was done this function will return final amount to be paid.
    /// </summary>
    /// <returns></returns>
    public async Task<decimal> CurrentReward()
    {
        var usage = await this.CurrentUsage();
        return usage.Reward(this.Price);
    }
}

