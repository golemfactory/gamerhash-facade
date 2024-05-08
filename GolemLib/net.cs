namespace GolemLib;

public enum RelayType
{
    /// <summary>
    /// Main public relay at: yacn2.dev.golem.network:7477
    /// </summary>
    Public,
    /// <summary>
    /// Devnet relay at: yacn2a.dev.golem.network:7477
    /// </summary>
    Devnet,
    /// <summary>
    /// Local relay at: 127.0.0.1:16464
    /// </summary>
    Local,
    /// <summary>
    /// Use older centralized solution.
    /// </summary>
    Central,
    /// <summary>
    /// Facade won't set any value. It must be set from outside using env variables.
    /// </summary>
    None,
}
