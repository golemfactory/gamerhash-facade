using System.ComponentModel;
using System.Data.Common;
using GolemLib.Types;

namespace GolemLib;

public abstract class GolemJob : INotifyPropertyChanged
{
    public string Id { get; }
    public GolemPrice Price { get; }
    public JobStatus Status { get; }

    public event PropertyChangedEventHandler? PropertyChanged;


    public abstract Task<GolemUsage> CurrentUsage();
    public async Task<decimal> CurrentReward()
    {
        var usage = await this.CurrentUsage();
        return usage.Reward(this.Price);
    }
}

