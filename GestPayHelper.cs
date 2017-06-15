using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.GestPay
{
    /// <summary>
    /// Represents GestPay helper
    /// </summary>
    public class GestPayHelper
    {
        /// <summary>
        /// Gets a payment status
        /// </summary>
        /// <param name="transactionResult">GestPay payment status</param>
        /// <param name="pendingReason">GestPay pending reason</param>
        /// <returns>Payment status</returns>
        public static PaymentStatus GetPaymentStatus(string transactionResult, string pendingReason)
        {
            /*
             * Risultato transazione:
             * E' possibile interpretare l'esito di una transazione verificando 
             * il valore del campo: TransactionResult.
             * I valori possibili sono:
             * OK  :Esito transazione positivo
             * KO  :Esito transazione negativo
             * XX  :Esito transazione sospeso (solo in caso di pagamento con bonifico)
            */
            var result = PaymentStatus.Pending;

            if (transactionResult == null)
                transactionResult = string.Empty;

            if (pendingReason == null)
                pendingReason = string.Empty;

            switch (transactionResult.ToLowerInvariant())
            {
                case "xx": //Pending Solo per bonifico
                    switch (pendingReason.ToLowerInvariant())
                    {
                        case "authorization":
                            result = PaymentStatus.Authorized;
                            break;
                        default:
                            result = PaymentStatus.Pending;
                            break;
                    }
                    break;
                case "ok": //Esito transazione positivo
                    result = PaymentStatus.Paid;
                    break;
                case "ko": //Esito transazione negativo
                    result = PaymentStatus.Voided;
                    break;
            }
            return result;
        }
    }
}

