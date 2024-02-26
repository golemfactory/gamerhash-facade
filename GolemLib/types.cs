namespace GolemLib.Types;

using System;
using System.Collections.Generic;
using System.Dynamic;

/// <summary>
/// Resources usage reported by ExeUnit.
/// </summary>
public class GolemUsage : GolemPrice
{
    public decimal Reward(GolemPrice prices)
    {
        return prices.StartPrice * this.StartPrice
            + prices.GpuPerSec * this.GpuPerSec
            + prices.NumRequests * this.NumRequests
            + prices.EnvPerSec * this.EnvPerSec;
    }
}

public enum JobStatus
{
    /// Default job state.
    Idle,
    /// When job's activity transitions from `Initialized` to `Deployed` state
    DownloadingModel,
    //TODO exe-unit should set `Computing` state when it receives GSB computation requests.
    /// When job's activity state is set to `Ready`.
    Computing,
    /// When  job's activity state is set to `Terminated`.
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

public class ActivityPayment
{
    public required string ActivityId { get; init; }
    public required string Amount { get; init; }

    public override int GetHashCode()
    {
        return HashCode.Combine(ActivityId, Amount);
    }

    public override bool Equals(object? obj)
    {
        if (obj == null)
        {
            return false;
        }
        ActivityPayment? payment = obj as ActivityPayment;
        return ActivityId.Equals(payment?.ActivityId)
            && Amount.Equals(payment?.Amount)
            ;
    }
}

public class AgreementPayment
{
    public required string AgreementId { get; init; }
    public required string Amount { get; init; }

    public override int GetHashCode()
    {
        return HashCode.Combine(AgreementId, Amount);
    }

    public override bool Equals(object? obj)
    {
        if (obj == null)
        {
            return false;
        }
        AgreementPayment? payment = obj as AgreementPayment;
        return AgreementId.Equals(payment?.AgreementId)
            && Amount.Equals(payment?.Amount)
            ;
    }
}

public class Payment
{
    public required string PaymentId { get; init; }
    public required string PayerId { get; init; }
    public required string PayeeId { get; init; }
    public required string PayerAddr { get; init; }
    public required string PayeeAddr { get; init; }
    public required string PaymentPlatform { get; init; }
    public required string Amount { get; init; }
    public required DateTime Timestamp { get; init; }
    public required List<ActivityPayment> ActivityPayments { get; init; }
    public required List<AgreementPayment> AgreementPayments { get; init; }

    public required string Details { get; init; }
    public List<byte>? Signature { get; init; }
    public List<byte>? SignedBytes { get; init; }

    public string TransactionId
    {
        get
        {
            return "0x" + Convert.ToHexString(Convert.FromBase64String(Details));
        }
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            HashCode.Combine(PaymentId, PayerId, PayeeId, PayerAddr, PayeeAddr, PaymentPlatform, Amount, Timestamp),
            ActivityPayments, AgreementPayments, Details
        );
    }

    public override bool Equals(object? obj)
    {
        if (obj == null)
        {
            return false;
        }
        Payment? payment = obj as Payment;
        return PaymentId.Equals(payment?.PaymentId)
            && PayerId.Equals(payment?.PayerId)
            && PayeeId.Equals(payment?.PayeeId)
            && PayerAddr.Equals(payment?.PayerAddr)
            && PayeeAddr.Equals(payment?.PayeeAddr)
            && PaymentPlatform.Equals(payment?.PaymentPlatform)
            && Amount.Equals(payment?.Amount)
            && Amount.Equals(payment?.Amount)
            && Timestamp.Equals(payment?.Timestamp)
            && ActivityPayments.Equals(payment?.ActivityPayments)
            && AgreementPayments.Equals(payment?.AgreementPayments)
            && Details.Equals(payment?.Details)
            ;
    }
}
