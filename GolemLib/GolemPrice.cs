namespace GolemLib.Types;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// Represents price settings in Golem pricing model.
/// TODO: We will find out later which of these options make the most sense.
/// </summary>
public class GolemPrice: INotifyPropertyChanged
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
        } set
        {
            gpuPerHour = value;
            OnPropertyChanged();
        }
    }

    public decimal EnvPerHour
    { 
        get
        {
            return envPerHour;
        } set
        {
            envPerHour = value;
            OnPropertyChanged();
        }
    }

    public decimal NumRequests
    { 
        get
        {
            return numRequests;
        } set
        {
            numRequests = value;
            OnPropertyChanged();
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
            startPrice = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
