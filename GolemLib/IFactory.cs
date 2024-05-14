namespace GolemLib;

using Microsoft.Extensions.Logging;

public interface IFactory
{
    /// <summary>Creates Golem object</summary>
    /// <param name="mainnet">Enables usage of mainnet payment network. By default `true`</param>
    /// If not specified it will default to `golem-data` dir inside of Golem package directory.
    /// </param>
    /// <returns>Golem object</returns>
    public Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory, bool mainnet = true);

    /// <summary>Creates Golem object</summary>
    /// <param name="mainnet">Enables usage of mainnet payment network. By default `true`</param>
    /// <param name="dataDir">Path where `provider` and `yagna` data directories will be created.
    /// If not specified it will default to `golem-data` dir inside of Golem package directory.
    /// </param>
    /// <returns>Golem object</returns>
    public Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory = null, bool mainnet = true, string? dataDir = null);
}

public interface IFactoryExt
{
    /// <summary>Creates Golem object</summary>
    /// <param name="relayType">Type of relay to use</param>
    public Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory, bool mainnet, string? dataDir, RelayType relayType);
}


