namespace GolemLib.Types;

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;

using Microsoft.Extensions.Options;

/// <summary>
/// Resources usage reported by ExeUnit.
/// </summary>
public class GolemUsage : GolemPrice
{
    public static decimal Round(decimal v) => GolemUsage.RustCompatibilityRound(v);

    // I couldn't find definite answer if C#'s `double` is IEEE-754 compliant floating number:
    // https://csharpindepth.com/Articles/FloatingPoint claims that it is, stackoverflow claimed that it isn't.
    // Still, both `double` and IEEE-754 64-bit floating numbers use 52 bits for binary significant digits,
    // which correspond to - depending or value of the highest mantisse bits - 15 to 17 decimal digits.
    // Rust library `bignum` in version 0.2 currently used in `yagna` incorrectly handles the most significant
    // digits when parsing floats. In order to get the same results in Rust and C#, we need to match its behavior.
    private static decimal RustCompatibilityRound(decimal i)
    {
        // `decimal` constructor using `double` argument truncates the value to 15 significant digits.
        // To receive a more accurate result (needed to match Rust's behavior), we need to use the `string`-based construction method.

        // Decimal -> double default conversion rounds decimal in a way that after converting back to decimal we get different number.
        // FOr this reason we need to use string as an intermediate step.
        var formattedDecimal = double.Parse(i.ToString("E15", CultureInfo.InvariantCulture));

        // Print to string with exponential notation including 16 significant digits (1 integer digit and 15 fractional digits).
        var formattedDouble = formattedDecimal.ToString("E15", CultureInfo.InvariantCulture);
        return decimal.Parse(formattedDouble, NumberStyles.Float);
    }

    public decimal Reward(GolemPrice prices)
    {
        return Round(prices.StartPrice) * Round(this.StartPrice)
                + Round(prices.GpuPerSec) * Round(this.GpuPerSec)
                + Round(prices.NumRequests) * Round(this.NumRequests)
                + Round(prices.EnvPerSec) * Round(this.EnvPerSec);
    }

    // Constructor from GolemPrice
    public GolemUsage(GolemPrice price)
    {
        StartPrice = 1;
        GpuPerSec = price.GpuPerSec;
        EnvPerSec = price.EnvPerSec;
        NumRequests = price.NumRequests;
    }

    public GolemUsage() { }

    public static GolemUsage From(Dictionary<string, decimal> coeffs)
    {
        return new GolemUsage(GolemPrice.From(1, coeffs));
    }

    public static GolemUsage operator +(GolemUsage a, GolemUsage b)
    {
        return new GolemUsage
        {
            EnvPerSec = a.EnvPerSec + b.EnvPerSec,
            GpuPerSec = a.GpuPerSec + b.GpuPerSec,
            NumRequests = a.NumRequests + b.NumRequests,
            StartPrice = a.StartPrice + b.StartPrice
        };
    }
}

public enum JobStatus
{
    /// Default job state.
    Idle,
    /// When job's activity transitions from `Initialized` to `Deployed` state
    DownloadingModel,
    /// When job's activity state is set to `Ready`.
    Computing,
    /// When  job's activity state is set to `Terminated`.
    Finished,
    /// This status is reported when Job was finished inproperly due to problems:
    /// - User stopped Golem during task execution
    /// - Provider crashed
    /// - Requestor was unreachable due to network conditions
    /// - Agreement couldn't be terminated for whatever reasons
    /// In this payments isn't guranteed.
    Interrupted
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
    Stopping,
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

public class ApplicationEventArgs : EventArgs
{
    public enum SeverityLevel { Error, Warning };

    public ApplicationEventArgs(string source, string message, SeverityLevel severity, Exception? exception, DateTime? timestamp = null)
    {
        Message = message;
        Source = source;
        Severity = severity;
        Exception = exception;
        Timestamp = timestamp ?? DateTime.Now;
    }

    public string Message { get; set; }
    public string Source { get; set; }
    public SeverityLevel Severity { get; set; }
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; }
    public string Context { get; set; } = "";
}
