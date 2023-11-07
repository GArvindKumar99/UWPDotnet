using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace SnapBilling.PushOffers.Services
{
    public interface IPushOfferBase
    {
        Task<bool> IsValid();
        string Message { get; set; }
        long TemplateId { get; }
    }
    public interface IDiscountBasedPushOffer : IPushOfferBase
    {
        double DiscountOffer { get; set; }

    }
    public interface IDateRangeTypePushOffer : IPushOfferBase
    {
        DateTime ValidFrom { get; set; }
        DateTime ValidTo { get; set; }
    }

    public interface IProductTypePushOffer : IPushOfferBase
    {
        IEnumerable<string> ProductNameList { get; set; }
    }
}
