namespace GolemLib;

using GolemLib.Types;
using System.Threading.Tasks;
using System.ComponentModel;

public interface IGolem : INotifyPropertyChanged
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
    GolemStatus Status { get; set; }

    /// <summary>
    /// Node identification in Golem network.
    /// </summary>
    string NodeId { get; }


    Task<Result<Void, Error>> StartYagna();
    Task<Result<Void, Error>> StopYagna();
    /// <summary>
    /// Returns true if process can be suspended without stopping computations.
    /// When job is in progress, `Provider` will be stopped after it is finished.
    /// `JobFinished` event will be generated then.
    /// If you want to stop anyway, use `StopYagna` method.
    /// </summary>
    /// <returns></returns>
    Task<Result<bool, Error>> Suspend();
    /// <summary>
    /// Allow Provider to run tasks again.
    /// TODO: Might be redundant. Consider leaving only `StartYagna`.
    /// </summary>
    /// <returns></returns>
    Task<Result<Void, Error>> Resume();
    Task<Result<Void, Error>> BlacklistNode(string node_id);
}

