namespace GolemLib;

using GolemLib.Types;
using System.Threading.Tasks;

public interface IGolem
{
    public event EventHandler<Events.JobStarted> OnJobStarted;
    public event EventHandler<Events.Computing> OnComputing;
    public event EventHandler<Events.JobFinished> OnJobFinished;
    public event EventHandler<Events.PaymentConfirmed> OnPaymentConfirmed;


    GolemPrice Price { get; set; }
    string WalletAddress { get; set; }
    ApplicationState AppState { get; set; }
    /// <summary>
    /// Benchmarked network speed in B/s
    /// </summary>
    /// <param name="speed"></param>
    int SetNetworkSpeed { get; set; }


    Task<Result<Void, Error>> StartYagna();
    Task<Result<Void, Error>> StopYagna();
    Task<Result<Void, Error>> BlacklistNode(string node_id);
}

