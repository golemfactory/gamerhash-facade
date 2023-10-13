namespace GolemLib.Types;

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;


/// <summary>
/// Represents price settings in Golem pricing model.
/// TODO: We will find out later which of these options make the most sense.
/// </summary>
public record class GolemPrice
{
    public decimal GpuPerHour { get; set; }
    public decimal EnvPerHour { get; set; }
    public decimal NumRequests { get; set; }
    public decimal StartPrice { get; set; }
}

public record class GolemUsage : GolemPrice
{
    public decimal Reward(GolemPrice prices)
    {
        return prices.StartPrice * this.StartPrice
            + prices.GpuPerHour * this.GpuPerHour
            + prices.NumRequests * this.NumRequests
            + prices.EnvPerHour * this.EnvPerHour;
    }
}

public enum JobStatus
{
    Idle,
    DownloadingModel,
    Computing,
    Finished,
}

public enum PaymentStatus
{
    /// <summary>
    /// Provider sends Invoice to Requestor almost immediately after job was finished.
    /// If this won't happen in period of few minutes, it indicates incorrect Provider's
    /// behavior or Provider just can't reach Requestor anymore.
    /// </summary>
    InvoiceSent,
    /// <summary>
    /// Requestor accepted Invoice and transfer was scheduled on blockchain.
    /// 
    /// Period between acceptance and blockchain confirmation can be long.
    /// We should assume, that if payment wasn't settled during 24h from acceptance,
    /// it indicates incorrect behavior on Requestor side (but most probably not malicious).
    /// 
    /// Invoice should be accepted by Requestor within a few minutes after receiving.
    /// If it wasn't it can either indicate incorrect network behavior or malicious
    /// Requestor avoiding payments.
    /// Reaction to such situations needs further discussions.
    /// </summary>
    Accepted,
    /// <summary>
    /// Payment was confirmed on blockchain.
    /// </summary>
    Settled,
    /// <summary>
    /// Requestor could reject Invoice in case of incorrect Provider behavior.
    /// Note that this ability is not implemented in current yagna version,
    /// so this status always indicates bad bahavior in the network.
    /// </summary>
    Rejected,
}

/// <summary>
/// Golem consists of `yagna` and `ya-provider`, but this enum
/// aims to hide the complexity and provide only meaningful statuses
/// from UI perspective.
/// </summary>
public enum GolemStatus
{
    Off,
    Starting,
    Ready,
    /// <summary>
    /// Yagna daemon is running, but Golem will not accept tasks.
    /// (That can be implemented as `ya-provider` not running)
    /// </summary>
    Suspended,
    Error,
}

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record class ActivityPayment
{
    public string ActivityId { get; set; }
    public decimal Amount { get; set; }
}

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record class AgreementPayment
{
    public string AgreementId { get; set; }
    public decimal Amount { get; set; }
}

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record class Payment
{
    public string PaymentId { get; set; }
    public string PayerId { get; set; }
    public string PayeeId { get; set; }
    public string PayerAddr { get; set; }
    public string PayeeAddr { get; set; }
    public string PaymentPlatform { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public List<ActivityPayment> ActivityPayments { get; set; }
    public List<AgreementPayment> AgreementPayments { get; set; }

    public string TransactionId { get; set; }
    public byte[] Signature { get; set; }

    public bool ValidateSignature()
    {
        return false;
    }
}
