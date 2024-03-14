using Golem.Yagna.Types;

using GolemLib;

using Microsoft.Extensions.Logging;

namespace Golem
{
    public class Factory : IFactory
    {
        public Task<IGolem> Create(string modulesDir, ILoggerFactory loggerFactory, bool mainnet = true)
        {
            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            var network = Factory.Network(mainnet);

            return Task.FromResult(new Golem(binaries, datadir, loggerFactory, network) as IGolem);
        }

        public static Network Network(bool mainnet) {
            return mainnet ? Yagna.Types.Network.Polygon : Yagna.Types.Network.Holesky;
        }
    }

}
