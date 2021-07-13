using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.CyberSource
{
    public class CyberSourcePaymentSettings : ISettings
    {
        public string GatewayUrl { get; set; }
        public string MerchantId { get; set; }
        public string PublicKey { get; set; }
        public string SerialNumber { get; set; }
        public decimal AdditionalFee { get; set; }
    }
}
