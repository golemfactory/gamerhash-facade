namespace GolemLib;

using Microsoft.Extensions.Logging;

public interface IFactory
{
    public Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory = null, bool mainnet = true, string? dataDir = null);
}
