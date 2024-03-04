namespace GolemLib;

using Microsoft.Extensions.Logging;

public interface IFactory
{
    public bool Mainnet { get; set; }

    public Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory);
}
