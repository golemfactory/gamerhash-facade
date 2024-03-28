using System.ComponentModel;

using GolemLib.Types;

namespace GolemLib;

public interface IJob : INotifyPropertyChanged
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
    public GolemPrice Price { get; }
    public JobStatus Status { get; }
    /// <summary>
    /// Property is set after Provider sends Invoice to Requestor.
    /// </summary>
    public PaymentStatus? PaymentStatus { get; }

    /// <summary>
    /// The list will be complete, when PaymentStatus is `Settled`.
    /// 
    /// In case of long running tasks new `Payment` objects can be added
    /// during execution.
    /// </summary>
    /// <returns>List of payments made for this Job.</returns>
    public List<Payment> PaymentConfirmation { get; }
    /// <summary>
    /// Get usage counters during task execution, what allows to estimate
    /// reward for the job done.
    /// </summary>
    /// <returns></returns>
    public GolemUsage CurrentUsage { get; }
    /// <summary>
    /// Get amount that should be paid for the task until this point in time.
    /// After task was done this function will return final amount to be paid.
    /// </summary>
    /// <returns></returns>
    public decimal CurrentReward
    {
        get;
    }
}

