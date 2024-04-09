namespace GolemLib;

using Microsoft.Extensions.Logging;

public interface IFactory
{
    /// <summary>Creates Golem object</summary>
    /// <param name="mainnet">Enables usage of mainnet payment network. By default `true`</param>
    /// <param name="dataDir">Path where `provider` and `yagna` data directories will be created.
    /// If not specified it will default to `golem-data` dir inside of Golem package directory.
    /// </param>
    /// <returns>Golem object</returns>
    public Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory, bool mainnet = true);
}
