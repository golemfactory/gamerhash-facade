namespace GolemLib.Types;

using System;
using System.Collections.Generic;


/// <summary>
/// Represents price settings in Golem pricing model.
/// TODO: We will find out later which of these options make the most sense.
/// </summary>
public class GolemPrice
{
    public decimal GpuPerHour { get; set; }
    public decimal EnvPerHour { get; set; }
    public decimal NumRequests { get; set; }
    public decimal StartPrice { get; set; }
}

public class GolemUsage : GolemPrice
{
    public decimal reward(GolemPrice prices)
    {
        return prices.StartPrice * this.StartPrice
            + prices.GpuPerHour * this.GpuPerHour
            + prices.NumRequests * this.NumRequests
            + prices.EnvPerHour * this.EnvPerHour;
    }
}

public class ApplicationState
{ }

public class GolemConfiguration
{
    string WalletAddress { get; set; }
    GolemPrice Price { get; set; }
}

public enum JobStatus
{
    Idle,
    DownloadingModel,
    Computing,
    Finished,
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
    string activity_id;
    decimal amount;
}

public class AgreementPayment
{
    string agreement_id;
    decimal amount;
}

public class Payment
{
    string payment_id;
    string payer_id;
    string payee_id;
    string payer_addr;
    string payee_addr;
    string payment_platform;
    decimal amount;
    DateTime timestamp;
    List<ActivityPayment> activity_payments;
    List<AgreementPayment> agreement_payments;

    string transaction_id;
    byte[] signature;

    bool ValidateSignature()
    {
        return false;
    }
}
