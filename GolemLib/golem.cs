namespace GolemLib;

using GolemLib.Types;
using System.Threading.Tasks;
using System.ComponentModel;

public interface IGolem : INotifyPropertyChanged
{
    public event EventHandler<Events.JobStarted> OnJobStarted;
    public event EventHandler<Events.JobFinished> OnJobFinished;
    public event EventHandler<Events.PaymentConfirmed> OnPaymentConfirmed;


    public GolemPrice Price { get; set; }
    public string WalletAddress { get; set; }
    public ApplicationState AppState { get; set; }
    /// <summary>
    /// Benchmarked network speed in B/s
    /// </summary>
    /// <param name="speed"></param>
    public uint SetNetworkSpeed { get; set; }
    public GolemStatus Status { get; }
    public GolemJob? CurrentJob { get; }

    /// <summary>
    /// Node identification in Golem network.
    /// </summary>
    public string NodeId { get; }

    public Task StartYagna();
    /// <summary>
    /// Shutdown all Golem processes even if any job is in progress.
    /// </summary>
    /// <returns></returns>
    public Task StopYagna();
    /// <summary>
    /// Returns true if process can be suspended without stopping computations.
    /// When job is in progress, `Provider` will be stopped after it is finished.
    /// `JobFinished` event will be generated then.
    /// If you want to stop anyway, use `StopYagna` method.
    /// </summary>
    /// <returns></returns>
    public Task<bool> Suspend();
    /// <summary>
    /// Allow Provider to run tasks again.
    /// TODO: Might be redundant. Consider leaving only `StartYagna`.
    /// </summary>
    /// <returns></returns>
    public Task Resume();
    public Task BlacklistNode(string node_id);
}

