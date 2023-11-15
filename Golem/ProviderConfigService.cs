using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem
{
    using global::Golem.Yagna.Types;
    using global::Golem.Yagna;
    using GolemLib.Types;

    namespace GolemUI.Src
    {
        public class ProviderConfigService
        {
            private readonly Provider _provider;
            public Network Network { get; private set; }

            public ProviderConfigService(Provider provider, Network network)
            {
                _provider = provider;
                Network = network;
            }

            public string WalletAddress
            {
                get
                {
                    return _provider.Config?.Account ?? "";
                }
                set
                {
                    UpdateWalletAddress(value);
                }
            }

            public GolemPrice GolemPrice
            {
                get
                {
                    var preset = _provider.PresetConfig.GetPreset(_provider.PresetConfig.DefaultPresetName);
                    
                    if(preset == null)
                        return new GolemPrice();

                    if(!preset.UsageCoeffs.TryGetValue("ai-runtime.requests", out var numRequests))
                        numRequests = 0;
                    if(!preset.UsageCoeffs.TryGetValue("golem.usage.duration_sec", out var duration))
                        duration = 0;
                    if(!preset.UsageCoeffs.TryGetValue("golem.usage.gpu-sec", out var gpuSec))
                        gpuSec = 0;

                    var initPrice = preset.InitialPrice ?? 0m;
                    
                    return new GolemPrice
                    {
                        EnvPerHour = duration,
                        StartPrice = initPrice,
                        GpuPerHour = gpuSec,
                        NumRequests = numRequests
                    };
                }

                set
                {
                    _provider.PresetConfig.UpdatePrices(_provider.PresetConfig.DefaultPresetName, new Dictionary<string, decimal>
                    {
                        { "num-requests", value.NumRequests },
                        { "golem.usage.duration_sec", value.EnvPerHour },
                        { "gpu-sec", value.GpuPerHour },
                        { "Initial", value.StartPrice }
                    });
                }
            }

            private void UpdateWalletAddress(string? walletAddress = null)
            {
                var config = _provider.Config;
                if (config != null)
                {
                    config.Account = walletAddress;
                    _provider.Config = config;
                }
            }
        }
    }

}
