using System.ComponentModel;
using System.Runtime.CompilerServices;
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


        private GolemStatus status;
        public GolemStatus Status
        {
            get { return status; }
            set {  status = value; OnPropertyChanged(); }
        }

        public IJob? CurrentJob => throw new NotImplementedException();

        public string NodeId => throw new NotImplementedException();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

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

        public Task Start()
        {
            Status = GolemStatus.Starting;

            var yagnaOptions = new YagnaStartupOptions
            {
                Debug = true,
                OpenConsole = true,
                ForceAppKey = "0x6b0f51cfaae644ee848dfa455dabea5d"
            };
            var success = Yagna.Run(yagnaOptions);

            if (success)
            {
                var defaultKey = Yagna.AppKeyService.Get("default");
                if (defaultKey is not null)
                {
                    var key = defaultKey.Key ?? "";
                    if (Provider.Run(key, Network.Goerli, true, true))
                    {
                        Status = GolemStatus.Ready;
                    }
                    else
                    {
                        Status = GolemStatus.Error;
                    }
                }
            }
            else
            {
                Status = GolemStatus.Error;
            }

            return Task.CompletedTask;
        }

        public async Task Stop()
        {
            await Provider.Stop();
            await Yagna.Stop();
            Status = GolemStatus.Off;
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