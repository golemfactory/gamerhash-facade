namespace GolemLib;

using GolemLib.Types;

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


    bool StartYagna();
    bool StopYagna();
    void BlacklistNode(string node_id);
}


public interface IGolemConnector
{
    void OnEvent(Events.IGolemEvent golemEvent);
    ApplicationState OnGetAppStatus();
}


