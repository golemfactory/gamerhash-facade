using Golem.Yagna.Types;

using GolemLib;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Golem
{
    public class Factory : IFactory
    {
        public async Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory, bool mainnet = true)
        {
            return await Create(modulesDir, loggerFactory, mainnet, null);
        }


        public async Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory = null, bool mainnet = true, string? dataDir = null)
        {
            return await Create(modulesDir, loggerFactory, mainnet, dataDir, DecideRelayType());
        }

        public async Task<IGolem> Create(string modulesDir, ILoggerFactory? loggerFactory, bool mainnet, string? dataDir, RelayType relayType)
        {
            var binaries = Path.Combine(modulesDir, "golem");

            dataDir ??= Path.Combine(modulesDir, "golem-data");

            var logger = loggerFactory ?? NullLoggerFactory.Instance;
            var network = Factory.Network(mainnet);
            var golem = new Golem(binaries, dataDir, logger, network, relayType);

            await ConfigureAccess(golem, binaries, mainnet, logger);

            return golem as IGolem;
        }

        public static Network Network(bool mainnet)
        {
            return mainnet ? Yagna.Types.Network.Polygon : Yagna.Types.Network.Holesky;
        }

        public static RelayType DecideRelayType()
        {
            var env = Environment.GetEnvironmentVariable("GOLEM_RELAY_TYPE") ?? "";
            return Enum.TryParse(env, out RelayType relay) ? relay : RelayType.Central;
        }

        private static async Task ConfigureAccess(Golem golem, string dir, bool mainnet, ILoggerFactory loggerFactory)
        {
            // Requstors are filtered only on mainnet. We assume that on testnet Provider
            // will work in developer mode for testing purposes, so blocking requestors
            // would make testing harder.
            golem.FilterRequestors = mainnet;
            golem.BlacklistEnabled = true;

            foreach (var url in CertificatesUrls())
            {
                try
                {
                    var path = Path.Combine(dir, Path.GetFileName(url));
                    await DownloadCert(url, path);
                    await golem.AllowCertified(path);
                }
                catch (Exception e)
                {
                    loggerFactory.CreateLogger<Factory>().LogError(e, $"Failed to download certificate: {url}");
                }

            }
        }

        private static List<string> CertificatesUrls()
        {
            return new List<string>
            {
                "https://ca.golem.network/cert/scalepoint.signed.json",
                "https://ca.golem.network/cert/modelserve.signed.json",
            };
        }

        private static async Task DownloadCert(string url, string filePath)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                using var fs = new FileStream(filePath, FileMode.OpenOrCreate);
                fs.SetLength(0);
                await response.Content.CopyToAsync(fs);
            }
            else
            {
                throw new Exception("Failed to download: " + response.ToString());
            }
        }
    }
}
