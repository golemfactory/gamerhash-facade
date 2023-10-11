namespace GolemLib.Types;

using System;
using System.Collections.Generic;


public class Result<TOk, TError>
{
    public TOk? Ok { get; set; }
    public TError? Err { get; set; }
}

/// <summary>
/// Represents error, for now only wraps error message, but it might
/// be more complicated in the future.
/// </summary>
public class Error
{
    string message;
}

public class Void { }

/// <summary>
/// Represents price settings in Golem pricing model.
/// TODO: We will find out later which of these options make the most sense.
/// </summary>
public class GolemPrice
{
    decimal GpuPerHour { get; set; }
    decimal EnvPerHour { get; set; }
    decimal NumRequests { get; set; }
    decimal StartPrice { get; set; }
}

public class GolemUsage : GolemPrice { }

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
