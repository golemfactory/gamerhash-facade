namespace GolemLib.Types;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// Represents price settings in Golem pricing model.
/// TODO: We will find out later which of these options make the most sense.
/// </summary>
public class NotInitializedGolemPrice : GolemPrice
{

}

public class GolemPrice : INotifyPropertyChanged, IEquatable<GolemPrice>
{

    private decimal startPrice;
    private decimal gpuPerHour;
    private decimal envPerHour;
    private decimal numRequests;

    public decimal GpuPerHour
    {
        get
        {
            return gpuPerHour;
        }
        set
        {
            if (gpuPerHour != value)
            {
                gpuPerHour = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal EnvPerHour
    {
        get
        {
            return envPerHour;
        }
        set
        {
            if (envPerHour != value)
            {
                envPerHour = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal NumRequests
    {
        get
        {
            return numRequests;
        }
        set
        {
            if (numRequests != value)
            {
                numRequests = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal StartPrice
    {
        get
        {
            return startPrice;
        }
        set
        {
            if (startPrice != value)
            {
                startPrice = value;
                OnPropertyChanged();
            }
        }
    }

    public Dictionary<string, decimal> GolemCounters()
    {
        return new Dictionary<string, decimal>
        {
            { "ai-runtime.requests", this.NumRequests },
            { "golem.usage.duration_sec", this.EnvPerHour },
            { "golem.usage.gpu-sec", this.GpuPerHour },
            { "Initial", this.StartPrice }
        };
    }


    public bool Equals(GolemPrice? other)
    {
        if (other == null)
            return false;

        return this.EnvPerHour == other.EnvPerHour &&
            this.GpuPerHour == other.GpuPerHour &&
            this.NumRequests == other.NumRequests &&
            this.StartPrice == other.StartPrice;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
