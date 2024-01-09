using GolemLib.Types;

interface IValidatorDb
{
    /// <summary>
    /// Query from database all Payment confirmations for specified TransactionId.
    /// </summary>
    /// <param name="TransactionId">Filter by `TransactionId` field from `Payment` structure.</param>
    /// <returns></returns>
    public Task<List<Payment>> QueryPayments(string TransactionId);
    /// <summary>
    /// Adds validated Payment confirmation to database. Next call to `QueryPayments`
    /// should return this Payment.
    /// </summary>
    /// <param name="payment"></param>
    /// <returns></returns>
    public Task InsertPayment(Payment payment);
}

interface IPaymentValidator
{
    /// <summary>
    /// Validates if Providers are entitled to payments they are claiming.
    /// There are 3 levels of validation:
    /// 1. Signatures validation
    /// 2. Blockchain transaction and basic payment information correctness
    /// 3. Protection against multi-spend, by cross-checking with all previous confirmations
    ///    claiming tokens from the same transaction.
    /// 
    /// For trusted Requestors only first 2 levels are necessary and they don't require history
    /// of Payment confirmations.
    /// 
    /// Notes:
    /// - PaymentValidator will add all confirmations to database, unless first 2-levels of checks
    ///   will fail. 3rd level check failures should still be added to database.
    /// - Function doesn't validate signatures of Payments queried from db.
    /// </summary>
    /// <returns>Throws exception if validation will fail.</returns>
    public Task Validate(IValidatorDb db, List<Payment> confirmations);
}


