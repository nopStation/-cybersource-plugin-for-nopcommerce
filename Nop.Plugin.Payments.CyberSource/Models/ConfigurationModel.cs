using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.CyberSource.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payments.CyberSource.GatewayUrl")]
        public string GatewayUrl { get; set; }

        [NopResourceDisplayName("Plugins.Payments.CyberSource.MerchantId")]
        public string MerchantId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.CyberSource.PublicKey")]
        public string PublicKey { get; set; }

        [NopResourceDisplayName("Plugins.Payments.CyberSource.SerialNumber")]
        public string SerialNumber { get; set; }

        [NopResourceDisplayName("Plugins.Payments.CyberSource.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
    }
}