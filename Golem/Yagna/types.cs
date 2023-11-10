using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GolemLib;
using GolemLib.Types;

namespace Golem.Yagna.Types
{
    public class Result<T> where T : class
    {
        public T? Ok { get; set; }
        public object? Err { get; set; }
    }

    public class Network
    {
        public string Id { get; }
        private Network(string _id)
        {
            Id = _id;
        }

        public static readonly Network Mainnet = new Network("mainnet");
        public static readonly Network Rinkeby = new Network("rinkeby");
        public static readonly Network Polygon = new Network("polygon");
        public static readonly Network Mumbai = new Network("mumbai");
        public static readonly Network Goerli = new Network("goerli");
    }

    public class PaymentDriver
    {
        public string Id { get; }

        private PaymentDriver(string id)
        {
            Id = id;
        }

        public static readonly PaymentDriver ERC20 = new PaymentDriver("erc20");
        public static readonly PaymentDriver ZkSync = new PaymentDriver("zksync");
    }

    //[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class PaymentStatus
    {
        public decimal Amount { get; set; }
        public decimal Reserved { get; set; }

        public StatusNotes? Outgoing { get; set; }
        public StatusNotes? Incoming { get; set; }
        public string? Driver { get; set; }

        public string? Network { get; set; }

        public string? Token { get; set; }

    }

    public class ActivityCounters
    {
        public int? New;
        public int? Ready;
        public int? Terminated;
        public int? Deployed;
    }
    public class ActivityStatus
    {
        public ActivityCounters? last1h { get; set; }
        public ActivityCounters? total { get; set; }

        //public 
    }


    //[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class StatusNotes
    {
        public StatValue? Requested { get; set; }

        public StatValue? Accepted { get; set; }

        public StatValue? Confirmed { get; set; }

    }

    //[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class StatValue
    {
        public decimal TotalAmount { get; set; }

        public uint AgreementsCount { get; set; }

    }

    public class Job : IJob
    {
        public required string Id { get; init; }

        //TODO
        public string RequestorId => "";

        //TODO
        public GolemPrice Price { get => new GolemPrice(); init => this.Price = new GolemPrice(); }

        //TODO
        public JobStatus Status => JobStatus.Idle;

        //TODO
        public GolemLib.Types.PaymentStatus? PaymentStatus => null;

        public event PropertyChangedEventHandler? PropertyChanged;

        //TODO
        public Task<GolemUsage> CurrentUsage()
        {
            return new Task<GolemUsage>(() => new GolemUsage());
        }

        //TODO
        public Task<Payment> PaymentConfirmation()
        {
            return new Task<Payment>(() => throw new NotImplementedException());
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Id, this.RequestorId, this.Status, this.PaymentStatus);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null)
            {
                return false;
            }
            Job? job = obj as Job;
            return Id.Equals(job?.Id)
                && RequestorId.Equals(job.RequestorId)
                && Status.Equals(job.Status)
                && PaymentStatus.Equals(job.PaymentStatus);
        }
    }

}
