using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem
{
    using global::Golem.Yagna.Types;
    using global::Golem.Yagna;
    
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
