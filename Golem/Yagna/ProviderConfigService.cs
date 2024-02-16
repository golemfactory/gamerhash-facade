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

                    var golemPrice = presets[0].ToPrice();

                    if (presets.Count == 1)
                        return golemPrice;

                    // All preset prices should have the same value. Unify if they don't.
                    for (int i = 1; i < presets.Count; i++)
                    {
                        var otherPreset = presets[i];
                        var otherPrice = otherPreset.ToPrice();

                        if (!golemPrice.Equals(otherPrice))
                        {
                            _provider.PresetConfig.UpdatePrice(otherPreset.Name, golemPrice);
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
                    _provider.PresetConfig.UpdateAllPrices(value);
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
