using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Services.Plugins;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Web.Framework;
using System.Threading.Tasks;
using Nop.Services.Common;

namespace Nop.Plugin.Payments.CyberSource
{
    /// <summary>
    /// CyberSource payment processor
    /// </summary>
    public class CyberSourcePaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly CyberSourcePaymentSettings _cyberSourcePaymentSettings;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly IAddressService _addressService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ICountryService _countryService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        #endregion

        #region Ctor

        public CyberSourcePaymentProcessor(CurrencySettings currencySettings,
            CyberSourcePaymentSettings cyberSourcePaymentSettings,
            ICurrencyService currencyService,
            ILocalizationService localizationService,
            ISettingService settingService,
            IWebHelper webHelper,
            IAddressService addressService,
            IStateProvinceService stateProvinceService,
            ICountryService countryService, 
            IHttpContextAccessor httpContextAccessor)
        {
            _currencySettings = currencySettings;
            _cyberSourcePaymentSettings = cyberSourcePaymentSettings;
            _currencyService = currencyService;
            _localizationService = localizationService;
            _settingService = settingService;
            _webHelper = webHelper;
            _addressService = addressService;
            _stateProvinceService = stateProvinceService;
            _countryService = countryService;
            _httpContextAccessor = httpContextAccessor;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult { NewPaymentStatus = PaymentStatus.Pending };
            return Task.FromResult(result);
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var post = new RemotePost(_httpContextAccessor, _webHelper)
            {
                FormName = "CyberSource",
                Url = _cyberSourcePaymentSettings.GatewayUrl,
                Method = "POST"
            };

            post.Add("merchantID", _cyberSourcePaymentSettings.MerchantId);
            post.Add("orderPage_timestamp", HostedPaymentHelper.OrderPageTimestamp);
            post.Add("orderPage_transactionType", "authorization");
            post.Add("orderPage_version", "4");
            post.Add("orderPage_serialNumber", _cyberSourcePaymentSettings.SerialNumber);

            post.Add("amount", string.Format(CultureInfo.InvariantCulture, "{0:0.00}", postProcessPaymentRequest.Order.OrderTotal));
            post.Add("currency", (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId)).CurrencyCode);
            post.Add("orderNumber", postProcessPaymentRequest.Order.Id.ToString());

            var billingAddress = await _addressService.GetAddressByIdAsync(postProcessPaymentRequest.Order.BillingAddressId);
            post.Add("billTo_firstName", billingAddress.FirstName);
            post.Add("billTo_lastName", billingAddress.LastName);
            post.Add("billTo_street1", billingAddress.Address1);

            var country = await _countryService.GetCountryByIdAsync(billingAddress.CountryId ?? 0);
            var billCountry = country;
            if (billCountry != null)
            {
                post.Add("billTo_country", billCountry.TwoLetterIsoCode);
            }

            var billState = await _stateProvinceService.GetStateProvinceByIdAsync(billingAddress.StateProvinceId ?? 0);
            if (billState != null)
            {
                post.Add("billTo_state", billState.Abbreviation);
            }
            post.Add("billTo_city", billingAddress.City);
            post.Add("billTo_postalCode", billingAddress.ZipPostalCode);
            post.Add("billTo_phoneNumber", billingAddress.PhoneNumber);
            post.Add("billTo_email", billingAddress.Email);

            var shippingAddress = await _addressService.GetAddressByIdAsync(postProcessPaymentRequest.Order.ShippingAddressId ?? 0);
            if (postProcessPaymentRequest.Order.ShippingStatus != ShippingStatus.ShippingNotRequired)
            {
                post.Add("shipTo_firstName", shippingAddress.FirstName);
                post.Add("shipTo_lastName", shippingAddress.LastName);
                post.Add("shipTo_street1", shippingAddress.Address1);
                var shipCountry = await _countryService.GetCountryByIdAsync(shippingAddress.CountryId ?? 0);
                if (shipCountry != null)
                {
                    post.Add("shipTo_country", shipCountry.TwoLetterIsoCode);
                }
                var shipState = await _stateProvinceService.GetStateProvinceByIdAsync(shippingAddress.StateProvinceId ?? 0);
                if (shipState != null)
                {
                    post.Add("shipTo_state", shipState.Abbreviation);
                }
                post.Add("shipTo_city", shippingAddress.City);
                post.Add("shipTo_postalCode", shippingAddress.ZipPostalCode);
            }

            post.Add("orderPage_receiptResponseURL", $"{_webHelper.GetStoreLocation(false)}checkout/completed");
            post.Add("orderPage_receiptLinkText", "Return");

            post.Add("orderPage_signaturePublic", HostedPaymentHelper.CalcRequestSign(post.Params, _cyberSourcePaymentSettings.PublicKey));

            post.Post();
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(_cyberSourcePaymentSettings.AdditionalFee);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //CyberSource is the redirection payment method
            //it also validates whether order is also paid (after redirection) so customers will not be able to pay twice

            //payment status should be Pending
            if (order.PaymentStatus != PaymentStatus.Pending)
                return Task.FromResult(false);

            //let's ensure that at least 1 minute passed after order is placed
            return Task.FromResult(!((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1));
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentCyberSource/Configure";
        }

        public string GetPublicViewComponentName()
        {
            return "PaymentCyberSource";
        }

        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            var warnings = new List<string>();
            return Task.FromResult<IList<string>>(warnings);
        }

        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return Task.FromResult(paymentInfo);
        }

        public override async Task InstallAsync()
        {
            var settings = new CyberSourcePaymentSettings
            {
                GatewayUrl = "https://orderpagetest.ic3.com/hop/orderform.jsp",
                MerchantId = string.Empty,
                PublicKey = string.Empty,
                SerialNumber = string.Empty,
                AdditionalFee = 0,
            };
            await _settingService.SaveSettingAsync(settings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.RedirectionTip", "You will be redirected to CyberSource site to complete the order.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.GatewayUrl", "Gateway URL");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.GatewayUrl.Hint", "Enter gateway URL.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.MerchantId", "Merchant ID");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.MerchantId.Hint", "Enter merchant ID.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.PublicKey", "Public Key");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.PublicKey.Hint", "Enter public key.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.SerialNumber", "Serial Number");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.SerialNumber.Hint", "Enter serial number.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.AdditionalFee", "Additional fee");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.CyberSource.PaymentMethodDescription", "You will be redirected to CyberSource site to complete the order.");

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await _settingService.DeleteSettingAsync<CyberSourcePaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.RedirectionTip");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.GatewayUrl");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.GatewayUrl.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.MerchantId");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.MerchantId.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.PublicKey");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.PublicKey.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.SerialNumber");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.SerialNumber.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.AdditionalFee");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.AdditionalFee.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.CyberSource.PaymentMethodDescription");

            await base.UninstallAsync();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.CyberSource.PaymentMethodDescription");
        }

        #endregion
    }
}
