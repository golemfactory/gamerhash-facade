using System.ComponentModel;
using Golem;
using Golem.Yagna;
using GolemLib;
using GolemLib.Types;

namespace Golem
{
    public class Golem : GolemLib.IGolem
    {
        private YagnaService Yagna { get; set; }

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
            var yagnaOptions = new YagnaStartupOptions();
            Yagna.Run(yagnaOptions);
            return Task.CompletedTask;
        }

        public Task StopYagna()
        {
            throw new NotImplementedException();
        }

        public Task<bool> Suspend()
        {
            throw new NotImplementedException();
        }

        public Golem(string golemPath)
        {
            Yagna = new YagnaService(golemPath);
        }
    }
}