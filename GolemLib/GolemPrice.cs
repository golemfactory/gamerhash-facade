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
    private decimal gpuPerSec;
    private decimal envPerSec;
    private decimal numRequests;

    public decimal GpuPerSec
    {
        get
        {
            return gpuPerSec;
        }
        set
        {
            if (gpuPerSec != value)
            {
                gpuPerSec = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal EnvPerSec
    {
        get
        {
            return envPerSec;
        }
        set
        {
            if (envPerSec != value)
            {
                envPerSec = value;
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
            { "golem.usage.duration_sec", this.EnvPerSec },
            { "golem.usage.gpu-sec", this.GpuPerSec },
            { "Initial", this.StartPrice }
        };
    }

    public static GolemPrice From(decimal? initialPrice, Dictionary<string, decimal> coeffs)
    {
        if (!coeffs.TryGetValue("ai-runtime.requests", out var numRequests))
            numRequests = 0;
        if (!coeffs.TryGetValue("golem.usage.duration_sec", out var duration))
            duration = 0;
        if (!coeffs.TryGetValue("golem.usage.gpu-sec", out var gpuSec))
            gpuSec = 0;

        var initPrice = initialPrice ?? 0m;

        return new GolemPrice
        {
            EnvPerSec = duration,
            StartPrice = initPrice,
            GpuPerSec = gpuSec,
            NumRequests = numRequests
        };
    }


    public bool Equals(GolemPrice? other)
    {
        if (other == null)
            return false;

        return this.EnvPerSec == other.EnvPerSec &&
            this.GpuPerSec == other.GpuPerSec &&
            this.NumRequests == other.NumRequests &&
            this.StartPrice == other.StartPrice;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
