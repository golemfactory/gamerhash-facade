namespace GolemLib;

/// <summary>
/// Represents price settings in Golem pricing model.
/// TODO: We will find out later which of these options make the most sense.
/// </summary>
public class GolemPrice
{
    decimal GpuPerHour { get; set; }
    decimal EnvPerHour { get; set; }
    decimal NumRequests { get; set; }
    decimal StartPrice { get; set; }
}

public class ApplicationState
{ }

public interface IProvider
{
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
}


public interface IGolemConnector
{
    void OnEvent<T>(T golemEvent);
    ApplicationState OnGetAppStatus();
}

public class GolemConfiguration
{
    public string WalletAddress { get; set; }
    public GolemPrice Price { get; set; }
}

