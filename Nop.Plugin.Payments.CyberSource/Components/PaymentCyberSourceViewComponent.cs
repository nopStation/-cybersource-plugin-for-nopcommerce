using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.CyberSource.Components
{
    [ViewComponent(Name = "PaymentCyberSource")]
    public class PaymentCyberSourceViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.CyberSource/Views/PaymentInfo.cshtml");
        }
    }
}
