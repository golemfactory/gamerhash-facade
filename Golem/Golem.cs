using System.ComponentModel;
using Golem;
using Golem.Yagna;
using Golem.Yagna.Types;
using GolemLib;
using GolemLib.Types;

namespace Golem
{
    public class Golem : GolemLib.IGolem
    {
        private YagnaService Yagna { get; set; }
        private Provider Provider { get; set; }

        public GolemPrice Price { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string WalletAddress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public uint NetworkSpeed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public GolemStatus Status => throw new NotImplementedException();

        public IJob? CurrentJob => throw new NotImplementedException();

        public string NodeId => throw new NotImplementedException();

        public event PropertyChangedEventHandler? PropertyChanged;

        public Task BlacklistNode(string node_id)
        {
            throw new NotImplementedException();
        }

        public Task<List<IJob>> ListJobs(DateTime since)
        {
            throw new NotImplementedException();
        }

        public Task Resume()
        {
            throw new NotImplementedException();
        }

        public Task StartYagna()
        {
            var yagnaOptions = new YagnaStartupOptions
            {
                Debug = true,
                OpenConsole = true,
                ForceAppKey = "0x6b0f51cfaae644ee848dfa455dabea5d"
            };
            Yagna.Run(yagnaOptions);

            var keys = Yagna.AppKeyService.List();
            if(keys is not null && keys.Count > 0)
            {
                string id = keys[0].Id;
                if (keys.Count > 1)
                {
                    var key = keys.Where(x => x.Name == "default").FirstOrDefault();
                    if (key is not null)
                        id = key.Id;
                }
                Provider.Run(id, Network.Goerli, true, true);
            }

            return Task.CompletedTask;
        }

        public async Task StopYagna()
        {
            await Yagna.Stop();
        }

        public Task<bool> Suspend()
        {
            throw new NotImplementedException();
        }

        public Golem(string golemPath)
        {
            Yagna = new YagnaService(golemPath);
            Provider = new Provider(golemPath);
        }
    }
}