using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

using Golem.Model;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using static Golem.Model.ActivityState;

namespace Golem.Yagna.Types
{
    public class Job : IJob
    {
        public ILogger Logger { get; set; } = NullLogger.Instance;

        public required string Id { get; init; }

        public string RequestorId { get; init; } = "";

        private GolemPrice _price = new NotInitializedGolemPrice();
        public GolemPrice Price
        {
            get => _price;
            set
            {
                if (_price != value)
                {
                    _price = value;
                    OnPropertyChanged();
                }
            }
        }

        private JobStatus _status = JobStatus.Idle;
        public JobStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        private GolemLib.Types.PaymentStatus? _paymentStatus;
        public GolemLib.Types.PaymentStatus? PaymentStatus
        {
            get => _paymentStatus;
            set
            {
                if (_paymentStatus != value)
                {
                    _paymentStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private GolemUsage _currentUsage = new GolemUsage();
        public GolemUsage CurrentUsage
        {
            get => _currentUsage;
            set
            {
                if (_currentUsage != value)
                {
                    _currentUsage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentReward));
                }
            }
        }

        public decimal CurrentReward => CurrentUsage.Reward(Price);


        private List<Payment> _paymentConfirmation = new List<Payment>();
        public List<Payment> PaymentConfirmation
        {
            get
            {
                return _paymentConfirmation;
            }
            set
            {
                if (_paymentConfirmation != value)
                {
                    _paymentConfirmation = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime Timestamp { get; init; }
        private DateTime? IdleStart { get; set; }

        public bool Active
        {
            get
            {
                return Status != JobStatus.Finished && Status != JobStatus.Interrupted;
            }
        }


        public void UpdateActivityState(ActivityStatePair activityState)
        {
            var currentState = activityState.currentState();
            var nextState = activityState.nextState();
            this.Status = ResolveStatus(currentState, nextState);
        }

        public void AddPartialPayment(Payment payment)
        {
            if (!PaymentConfirmation.Exists(pay => pay.PaymentId == payment.PaymentId))
            {
                PaymentConfirmation.Add(payment);
                PaymentStatus = EvaluatePaymentStatus(PaymentStatus);
            }
        }

        /// <summary>
        /// Track Job idle time to Interrupt Job in case Provider won't be able to do it.
        /// </summary>
        /// <returns>Returns true if Idle time exceeded timeout.</returns>
        public bool StartIdling()
        {
            if (IdleStart == null)
            {
                IdleStart = DateTime.Now;
                return true;
            }
            return false;
        }

        public bool IdlingTimeout()
        {
            if (IdleStart != null)
                return IdleStart + TimeSpan.FromSeconds(100) < DateTime.Now;
            return false;
        }

        public void StopIdling()
        {
            IdleStart = null;
        }

        private JobStatus ResolveStatus(StateType currentState, StateType? nextState)
        {
            switch (currentState)
            {
                case StateType.New:
                    return JobStatus.Idle;
                case StateType.Initialized:
                    if (nextState == StateType.Deployed)
                        return JobStatus.DownloadingModel;
                    return JobStatus.Idle;
                case StateType.Deployed:
                    return JobStatus.Computing;
                case StateType.Ready:
                    return JobStatus.Computing;
                // Note: We don't set Interrupted, because Requestor could create next Activity
                // as long as Agreement is still alive.
                case StateType.Unresponsive:
                    return JobStatus.Idle;
            }
            return JobStatus.Idle;
        }

        public static JobStatus ResolveTerminationReason(string? code)
        {
            return code switch
            {
                "InitializationError" => JobStatus.Interrupted,
                "NoActivity" => JobStatus.Interrupted,
                "DebitNotesDeadline" => JobStatus.Interrupted,
                "DebitNoteRejected" => JobStatus.Interrupted,
                "DebitNoteCancelled" => JobStatus.Interrupted,
                "DebitNoteNotPaid" => JobStatus.Interrupted,
                "RequestorUnreachable" => JobStatus.Interrupted,
                "Shutdown" => JobStatus.Interrupted,
                "Interrupted" => JobStatus.Interrupted,
                "HealthCheckFailed" => JobStatus.Interrupted,
                "ConnectionTimedOut" => JobStatus.Interrupted,
                "ProviderUnreachable" => JobStatus.Interrupted,
                "Expired" => JobStatus.Finished,
                "Cancelled" => JobStatus.Finished,
                _ => JobStatus.Finished,
            };
        }

        public GolemLib.Types.PaymentStatus? EvaluatePaymentStatus(GolemLib.Types.PaymentStatus? suggestedPaymentStatus)
        {
            var confirmedSum = this.PaymentConfirmation.Sum(payment => Convert.ToDecimal(payment.Amount, CultureInfo.InvariantCulture));

            Logger.LogInformation($"Job: {this.Id}, confirmed sum: {confirmedSum}, job expected reward: {this.CurrentReward}");

            // Workaround for yagna unable to change status to SETTLED when using partial payments
            if (suggestedPaymentStatus == GolemLib.Types.PaymentStatus.Accepted
                && this.CurrentReward == confirmedSum)
            {
                return IntoPaymentStatus(InvoiceStatus.SETTLED);
            }
            return suggestedPaymentStatus;
        }

        public static GolemLib.Types.PaymentStatus IntoPaymentStatus(InvoiceStatus status)
        {
            return status switch
            {
                InvoiceStatus.ISSUED => GolemLib.Types.PaymentStatus.InvoiceSent,
                InvoiceStatus.RECEIVED => GolemLib.Types.PaymentStatus.InvoiceSent,
                InvoiceStatus.ACCEPTED => GolemLib.Types.PaymentStatus.Accepted,
                InvoiceStatus.REJECTED => GolemLib.Types.PaymentStatus.Rejected,
                InvoiceStatus.FAILED => GolemLib.Types.PaymentStatus.Rejected,
                InvoiceStatus.SETTLED => GolemLib.Types.PaymentStatus.Settled,
                InvoiceStatus.CANCELLED => GolemLib.Types.PaymentStatus.Rejected,
                _ => throw new Exception($"Unknown InvoiceStatus: {status}"),
            };
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, RequestorId, Status, PaymentStatus, PaymentConfirmation, CurrentUsage);
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
                && PaymentStatus.Equals(job.PaymentStatus)
                && PaymentConfirmation.Equals(job.PaymentConfirmation)
                && CurrentUsage.Equals(job.CurrentUsage)
                ;
        }
    }
}
