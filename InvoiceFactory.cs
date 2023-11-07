using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using System.Collections.Generic;
using ZXing.Aztec.Internal;

namespace SnapBilling.CartModule.Services
{
    public class InvoiceFactory
    {
        public static InvoiceInfo CreateDefault()
        {
            var invoice = new InvoiceInfo();
            invoice.BillStartedAt = invoice.createdAt = DateUtils.GetCurrentTime();
            invoice.IsMemo = false;
            invoice.IsGst = true;
            invoice.IsDelivery = false;
            invoice.IsDelivered = false;
            invoice.PosName = ServiceLocator.Current.GetService<ISettingService>().PosId?.ToString();
            //invoice.BillerName = ServiceLocator.Current.GetService<ILoginService>().GetUserInfo()?.Username;
            invoice.BillerName = SessionInfo.Current.LoggedInUser?.Username;
            List<PromotionDiscountPair> promotionDiscountPairs = new List<PromotionDiscountPair>();
            invoice.AppliedPromos = promotionDiscountPairs;
            return invoice;
        }

        public static InvoiceInfo CreateReturnDefault()
        {
            var invoice = new InvoiceInfo();
            invoice.BillStartedAt = invoice.createdAt = DateUtils.GetCurrentTime();
            invoice.IsMemo = false;
            invoice.IsGst = true;
            invoice.IsDelivery = false;
            invoice.IsDelivered = true;
            invoice.PosName = ServiceLocator.Current.GetService<ISettingService>().PosId?.ToString();
            //invoice.BillerName = ServiceLocator.Current.GetService<ILoginService>().GetUserInfo()?.Username;
            invoice.BillerName = SessionInfo.Current.LoggedInUser.Username;
            return invoice;
        }
    }
}