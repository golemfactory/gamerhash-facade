using System.ComponentModel;
using System.Data.Common;
using GolemLib.Types;

namespace GolemLib;

public abstract class Job : INotifyPropertyChanged
{
    public string Id { get; }
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

    public event PropertyChangedEventHandler? PropertyChanged;


    public abstract Task<GolemUsage> CurrentUsage();
    public async Task<decimal> CurrentReward()
    {
        var usage = await this.CurrentUsage();
        return usage.Reward(this.Price);
    }
}

