using Golem.Yagna.Types;

using GolemLib;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Golem
{
    public class Factory : IFactory
    {
        public Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory, bool mainnet = true)
        {
            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            var network = Factory.Network(mainnet);
            var golem = new Golem(binaries, datadir, loggerFactory ?? NullLoggerFactory.Instance, network);

            ConfigureAccess(golem, mainnet);

            return Task.FromResult(golem as IGolem);
        }

        public static Network Network(bool mainnet)
        {
            return mainnet ? Yagna.Types.Network.Polygon : Yagna.Types.Network.Holesky;
        }

        private static Task ConfigureAccess(Golem golem, bool mainnet)
        {
            // Requstors are filtered only on mainnet. We assume that on testnet Provider
            // will work in developer mode for testing purposes, so blocking requestors
            // would make testing harder.
            golem.FilterRequestors = mainnet;
            golem.BlacklistEnabled = true;


            return Task.CompletedTask;
        }
    }

}
