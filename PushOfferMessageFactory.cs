using SnapBilling.Services.AppServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapBilling.PushOffers.Services
{
    public interface AbstractPushOfferMessageFactory<T> where T: IPushOfferBase
    {
        T CreateMessage(); 
       
    }
    public class StoreWidePushOfferMessageFactory : AbstractPushOfferMessageFactory<StoreWidePushOffer>
    {
        public StoreWidePushOffer CreateMessage()
        {
            return new StoreWidePushOffer();
        }
        
    }

    public class SpecialPushOfferMessageFactory : AbstractPushOfferMessageFactory<SpecialPushOffer>
    {
        public SpecialPushOffer CreateMessage()
        {
            return new SpecialPushOffer();
        }
    }

    public class SnaporderPushOfferMessageFactory : AbstractPushOfferMessageFactory<SnaporderPushOffer>
    {
        public SnaporderPushOffer CreateMessage()
        {
            return new SnaporderPushOffer();
        }
    }

}
