namespace GolemLib;

using GolemLib.Types;

public interface IGolem
{
    public event EventHandler<Events.JobStarted> OnJobStarted;
    public event EventHandler<Events.Computing> OnComputing;
    public event EventHandler<Events.JobFinished> OnJobFinished;
    public event EventHandler<Events.PaymentConfirmed> OnPaymentConfirmed;

    bool StartYagna();
    bool StopYagna();
    void SetPrice(GolemPrice price);
    void SetAppState(ApplicationState state);
    void SetWalletAddres(string wallet);
    /// <summary>
    /// Benchmarked network speed in B/s
    /// </summary>
    /// <param name="speed"></param>
    void SetNetworkSpeed(int speed);
    void BlacklistNode(string node_id);
}


public interface IGolemConnector
{
    void OnEvent(Events.IGolemEvent golemEvent);
    ApplicationState OnGetAppStatus();
}


