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
                    if (!_provider.ClearHandle())
                    {
                        _provider.PresetConfig.InitilizeDefaultPresets();
                    }

                    var presets = _provider.PresetConfig.DefaultPresets;

                    if (presets.Count == 0)
                        return new GolemPrice();

                    var golemPrice = presetIntoPrice(presets[0]);

                    if (presets.Count == 1)
                        return golemPrice;

                    var golemPriceDict = golemPriceToDict(golemPrice);
                    // Unify Preset prices
                    for (int i = 1; i < presets.Count; i++)
                    {
                        var otherPreset = presets[i];
                        var otherPrice = presetIntoPrice(otherPreset);
                        var otherPriceDict = golemPriceToDict(otherPrice);
                        if (!golemPriceDict.Equals(otherPriceDict))
                        {
                            var args = _provider.PresetConfig.PriceDictToPriceArgs(golemPriceDict);
                            _provider.PresetConfig.UpdatePrices(otherPreset.Name, args);
                        }
                    }

                    return golemPrice;
                }

                set
                {
                    if (!_provider.ClearHandle())
                    {
                        _provider.PresetConfig.InitilizeDefaultPresets();
                    }
                    var priceDict = golemPriceToDict(value);
                    _provider.PresetConfig.UpdatePrices(priceDict);
                }
            }

            Dictionary<string, decimal> golemPriceToDict(GolemPrice price)
            {
                return new Dictionary<string, decimal>
                    {
                        { "ai-runtime.requests", price.NumRequests },
                        { "golem.usage.duration_sec", price.EnvPerHour },
                        { "golem.usage.gpu-sec", price.GpuPerHour },
                        { "Initial", price.StartPrice }
                    };
            }

            private GolemPrice presetIntoPrice(Preset preset)
            {
                if (!preset.UsageCoeffs.TryGetValue("ai-runtime.requests", out var numRequests))
                    numRequests = 0;
                if (!preset.UsageCoeffs.TryGetValue("golem.usage.duration_sec", out var duration))
                    duration = 0;
                if (!preset.UsageCoeffs.TryGetValue("golem.usage.gpu-sec", out var gpuSec))
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
