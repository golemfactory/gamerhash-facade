using System.ComponentModel;
using System.Runtime.CompilerServices;

using Golem.Model;

using GolemLib;
using GolemLib.Types;

using static Golem.Model.ActivityState;

namespace Golem.Yagna.Types
{
    public class Job : IJob
    {
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

        public void UpdateActivityState(ActivityStatePair activityState)
        {
            var currentState = activityState.currentState();
            var nextState = activityState.nextState();
            this.Status = ResolveStatus(currentState, nextState);
        }

        public void PartialPayment(Payment payment)
        {
            if (!PaymentConfirmation.Exists(pay => pay.PaymentId == payment.PaymentId))
            {
                PaymentConfirmation.Add(payment);
            }
        }

        private JobStatus ResolveStatus(StateType currentState, StateType? nextState)
        {
            switch (currentState)
            {
                case StateType.Initialized:
                    if (nextState == StateType.Deployed)
                        return JobStatus.DownloadingModel;
                    break;
                case StateType.Deployed:
                    return JobStatus.Computing;
                case StateType.Ready:
                    return JobStatus.Computing;
                case StateType.Terminated:
                    return JobStatus.Finished;
            }
            return JobStatus.Idle;
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
