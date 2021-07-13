using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.CyberSource.Models;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.CyberSource.Controllers
{
    public class PaymentCyberSourceController : BasePaymentController
    {
        private readonly CyberSourcePaymentSettings _cyberSourcePaymentSettings;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly INotificationService _notificationService;

        public PaymentCyberSourceController(CyberSourcePaymentSettings cyberSourcePaymentSettings,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentService paymentService,
            IPermissionService permissionService,
            ISettingService settingService,
            INotificationService notificationService)
        {
            _cyberSourcePaymentSettings = cyberSourcePaymentSettings;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentService = paymentService;
            _settingService = settingService;
            _notificationService = notificationService;
            _permissionService = permissionService;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                GatewayUrl = _cyberSourcePaymentSettings.GatewayUrl,
                MerchantId = _cyberSourcePaymentSettings.MerchantId,
                PublicKey = _cyberSourcePaymentSettings.PublicKey,
                SerialNumber = _cyberSourcePaymentSettings.SerialNumber,
                AdditionalFee = _cyberSourcePaymentSettings.AdditionalFee
            };

            return View("~/Plugins/Payments.CyberSource/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //save settings
            _cyberSourcePaymentSettings.GatewayUrl = model.GatewayUrl;
            _cyberSourcePaymentSettings.MerchantId = model.MerchantId;
            _cyberSourcePaymentSettings.PublicKey = model.PublicKey;
            _cyberSourcePaymentSettings.SerialNumber = model.SerialNumber;
            _cyberSourcePaymentSettings.AdditionalFee = model.AdditionalFee;
            await _settingService.SaveSettingAsync(_cyberSourcePaymentSettings);

            return RedirectToAction("Configure");
        }

        public async Task<IActionResult> IPNHandler(IFormCollection form)
        {
            var reasonCode = form["reasonCode"];

            if (HostedPaymentHelper.ValidateResponseSign(form, _cyberSourcePaymentSettings.PublicKey) &&
                !string.IsNullOrEmpty(reasonCode) && reasonCode.Equals("100") &&
                int.TryParse(form["orderNumber"], out int orderId))
            {
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order != null && _orderProcessingService.CanMarkOrderAsAuthorized(order))
                {
                    await _orderProcessingService.MarkAsAuthorizedAsync(order);
                }
            }

            return Content("");
        }
    }
}