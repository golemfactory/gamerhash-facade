namespace GolemLib;

using GolemLib.Types;
using System.Threading.Tasks;
using System.ComponentModel;

public interface IGolem : INotifyPropertyChanged
{
    public GolemPrice Price { get; set; }
    public string WalletAddress { get; set; }
    /// <summary>
    /// Benchmarked network speed in B/s.
    /// </summary>
    /// <param name="speed"></param>
    public uint NetworkSpeed { get; set; }
    /// <summary>
    /// Indicates whether Mainnet network or a test payment network is used.
    /// </summary>
    public bool Mainnet { get; }
    /// <summary>
    /// Payment network name.
    /// </summary>
    public string Network { get; }

    public GolemStatus Status { get; }
    /// <summary>
    /// You can either listen to PropertyChanged notifications for this property
    /// or use `OnJobStarted` and `OnJobFinished` events.
    /// This property is designed to work better with WPF binding contexts.
    /// </summary>
    public IJob? CurrentJob { get; }

    /// <summary>
    /// Node identification in Golem network.
    /// </summary>
    public string NodeId { get; }

    /// <summary>
    /// Enable Requestors filtering based on certificate.
    /// If set to false, all Requestors will be allowed to use this Provider.
    /// </summary>
    public bool FilterRequestors { get; set; }

    public Task Start();
    /// <summary>
    /// Shutdown all Golem processes even if any job is in progress.
    /// </summary>
    /// <returns></returns>
    public Task Stop();
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

    /// <summary>
    /// Don't accept tasks from this Node.
    /// Use in case of malicious Requestors.
    /// </summary>
    /// <param name="nodeId"></param>
    /// <returns></returns>
    public Task BlacklistNode(string nodeId);
    /// <summary>
    /// List all jobs that were running during period of time.
    /// In normal flow events api should be used to track jobs, but in case of application
    /// crash events can be lost and there might be a need to verify historical jobs.
    /// </summary>
    /// <param name="since">Only jobs started after this timestamp will be returned.</param>
    /// <returns></returns>
    public Task<List<IJob>> ListJobs(DateTime since);
}
