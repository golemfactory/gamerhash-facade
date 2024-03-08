using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
        public static readonly Network Holesky = new Network("holesky");
    }

    public class PaymentDriver
    {
        public string Id { get; }

        private PaymentDriver(string id)
        {
            Id = id;
        }

        public static readonly PaymentDriver ERC20 = new PaymentDriver("erc20");
        public static readonly PaymentDriver ERC20next = new PaymentDriver("erc20next");
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
}
