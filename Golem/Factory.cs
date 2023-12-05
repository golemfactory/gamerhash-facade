using GolemLib;

using Microsoft.Extensions.Logging;

namespace Golem
{
    public class Factory : IFactory
    {
        public Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory)
        {
            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            return Task.FromResult(new Golem(binaries, datadir, loggerFactory) as IGolem);
        }
    }

}
