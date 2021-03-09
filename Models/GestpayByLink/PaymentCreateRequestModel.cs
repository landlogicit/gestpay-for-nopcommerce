using System;
using System.Collections.Generic;

namespace Nop.Plugin.Payments.GestPay.Models.GestpayByLink
{
    [Serializable]
    public class PaymentCreateRequestModel
    {
        public string shopLogin { get; set; }
        public string amount { get; set; }
        public string currency { get; set; }
        public string shopTransactionID { get; set; }
        public string buyerName { get; set; }
        public string buyerEmail { get; set; }
        public string languageId { get; set; }
        public CustomInfo customInfo { get; set; }
        public PaymentChannel paymentChannel { get; set; }
        public string requestToken { get; set; }
        public string clientIP { get; set; }
        public string itemType { get; set; }
        public string recurrent { get; set; }
        public List<object> paymentType { get; set; }
        public ShippingDetails shippingDetails { get; set; }
        public OrderDetails orderDetails { get; set; }
        public TransDetails transDetails { get; set; }
    }

    [Serializable]
    public class CustomInfo
    {
        public Dictionary<string, string> customInfo { get; set; }
    }

    [Serializable]
    public class PaymentChannel
    {
        public List<string> channelType { get; set; }
    }

    [Serializable]
    public class ShippingDetails
    {
        public string shipToName { get; set; }
        public string shipToStreet { get; set; }
        public string shipToState { get; set; }
        public string shipToCountryCode { get; set; }
        public string shipToZip { get; set; }
        public string shipToStreet2 { get; set; }
    }

    [Serializable]
    public class FraudPrevention
    {
        public string submitForReview { get; set; }
        public string orderDateTime { get; set; }
        public string orderNote { get; set; }
        public string submissionReason { get; set; }
        public string beaconSessionID { get; set; }
        public string vendorID { get; set; }
        public string vendorName { get; set; }
        public string source { get; set; }
    }

    [Serializable]
    public class Social
    {
        public string network { get; set; }
        public string publicUsername { get; set; }
        public string communityScore { get; set; }
        public string profilePicture { get; set; }
        public string email { get; set; }
        public string bio { get; set; }
        public string accountUrl { get; set; }
        public string following { get; set; }
        public string followed { get; set; }
        public string posts { get; set; }
        public string id { get; set; }
        public string authToken { get; set; }
        public string socialData { get; set; }
    }

    [Serializable]
    public class CustomerDetail
    {
        public string profileID { get; set; }
        public string merchantCustomerID { get; set; }
        public string firstName { get; set; }
        public string middleName { get; set; }
        public string lastname { get; set; }
        public string primaryEmail { get; set; }
        public string secondaryEmail { get; set; }
        public string homePhone { get; set; }
        public string mobilePhone { get; set; }
        public string dateOfBirth { get; set; }
        public string gender { get; set; }
        public string socialSecurityNumber { get; set; }
        public string company { get; set; }
        public string createdAtDate { get; set; }
        public string verifiedEmail { get; set; }
        public string accountType { get; set; }
        public Social social { get; set; }
    }

    [Serializable]
    public class ShippingAddress
    {
        public string profileID { get; set; }
        public string firstName { get; set; }
        public string middleName { get; set; }
        public string lastname { get; set; }
        public string streetName { get; set; }
        public string streetname2 { get; set; }
        public string houseNumber { get; set; }
        public string houseExtention { get; set; }
        public string city { get; set; }
        public string zipCode { get; set; }
        public string state { get; set; }
        public string countryCode { get; set; }
        public string email { get; set; }
        public string primaryPhone { get; set; }
        public string secondaryPhone { get; set; }
        public string company { get; set; }
        public string stateCode { get; set; }
    }

    [Serializable]
    public class BillingAddress
    {
        public string profileID { get; set; }
        public string firstName { get; set; }
        public string middleName { get; set; }
        public string lastname { get; set; }
        public string streetName { get; set; }
        public string streetname2 { get; set; }
        public string houseNumber { get; set; }
        public string houseExtention { get; set; }
        public string city { get; set; }
        public string zipCode { get; set; }
        public string state { get; set; }
        public string countryCode { get; set; }
        public string email { get; set; }
        public string primaryPhone { get; set; }
        public string secondaryPhone { get; set; }
        public string company { get; set; }
        public string stateCode { get; set; }
    }

    [Serializable]
    public class ProductDetail
    {
        public string ProductCode { get; set; }
        public string SKU { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Quantity { get; set; }
        public string Price { get; set; }
        public string UnitPrice { get; set; }
        public string Type { get; set; }
        public string Vat { get; set; }
        public string Category { get; set; }
        public string Subcategory { get; set; }
        public string Brand { get; set; }
        public string RequiresShipping { get; set; }
        public string DeliveryAt { get; set; }
        public string Condition { get; set; }

        public DigitalGiftCardDetails DigitalGiftCardDetails { get; set; }
    }

    [Serializable]
    public class DigitalGiftCardDetails
    {
        public string SenderName { get; set; }
        public string DisplayName { get; set; }
        public string GreetingMessage { get; set; }

        public Recipient Recipient { get; set; }
    }

    [Serializable]
    public class Recipient
    {
        public string Email { get; set; }
    }

    [Serializable]
    public class ProductDetails
    {
        public List<ProductDetail> ProductDetail { get; set; }
    }

    [Serializable]
    public class DiscountCode
    {
        public string Amount { get; set; }
        public string Code { get; set; }
    }

    [Serializable]
    public class ShippingLine
    {
        public string Price { get; set; }
        public string Title { get; set; }
        public string Code { get; set; }
    }

    [Serializable]
    public class OrderDetails
    {
        public FraudPrevention fraudPrevention { get; set; }
        public CustomerDetail customerDetail { get; set; }
        public ShippingAddress shippingAddress { get; set; }
        public BillingAddress billingAddress { get; set; }
        public ProductDetails ProductDetails { get; set; }
        public List<DiscountCode> discountCodes { get; set; }
        public List<ShippingLine> shippingLines { get; set; }
    }

    [Serializable]
    public class TransDetails
    {
    }

}