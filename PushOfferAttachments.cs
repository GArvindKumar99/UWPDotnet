using SnapBilling.Services.AppServices;

namespace SnapBilling.PushOffers.Services
{
    public sealed class PushOfferAttachments : IPushOfferAttachments
    {
        public string StoreName { get ; set ; }
        public string StorePhone { get ; set ; }

        private static PushOfferAttachments instance = null;
        public static PushOfferAttachments Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PushOfferAttachments();
                }
                return instance;
            }
        }
        private PushOfferAttachments()
        {
            StoreName = SettingsContext.CurrentSettings.PrinterCustomization?.StoreName?? SettingsContext.CurrentSettings.Registration.StoreDetails.Name;
            StorePhone = SettingsContext.CurrentSettings.PrinterCustomization?.ContactNumber?? SettingsContext.CurrentSettings.Registration.StoreDetails.Phone;
            //StorePhone = SettingsContext.CurrentSettings.Registration.StoreDetails.Phone;
        }
    }
}
