using SnapBilling.Services.AppServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapBilling.PushOffers.Services
{
    public abstract class PushOfferBaseService<T> where T : IPushOfferBase
    {
        public abstract PushOfferInfo ConvertFromBaseToInfo(T pushOffer);
    }
    public class StoreWidePushOfferService : PushOfferBaseService<StoreWidePushOffer>
    {
        public override PushOfferInfo ConvertFromBaseToInfo(StoreWidePushOffer pushOffer)
        {
            return new PushOfferInfo
            {
                Message = pushOffer.Message,
                DiscountOffer = pushOffer.DiscountOffer,
                ValidFrom = pushOffer.ValidFrom,
                ValidTo = pushOffer.ValidTo,
                SendAt = DateTime.Now.Ticks,
                TemplateId = pushOffer.TemplateId
            };
        }
    }
    public class SpecialPushOfferService : PushOfferBaseService<SpecialPushOffer>
    {
        public override PushOfferInfo ConvertFromBaseToInfo(SpecialPushOffer pushOffer)
        {
            return new PushOfferInfo
            {
                Message = pushOffer.Message,
                ProductName = String.Join(",", pushOffer.ProductNameList),
                DiscountOffer = pushOffer.DiscountOffer,
                ValidFrom = pushOffer.ValidFrom,
                ValidTo = pushOffer.ValidTo,
                SendAt = DateTime.Now.Ticks,
                TemplateId = pushOffer.TemplateId
            };
        }
    }
    public class SnaporderPushOfferService : PushOfferBaseService<SnaporderPushOffer>
    {
        public override PushOfferInfo ConvertFromBaseToInfo(SnaporderPushOffer pushOffer)
        {
            return new PushOfferInfo
            {
                Message = pushOffer.Message,
                SendAt = DateTime.Now.Ticks,
                TemplateId= pushOffer.TemplateId
            };
        }
    }
}
