using SnapBilling.PushOffers.Services;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.AppSettingClasses;
using System;

namespace SnapBilling.PushOffers.ViewModels
{
    internal class SnapOrderOfferViewModel : ViewModelBase
    {
        private string message = "";
        private readonly IMessageService messageService = null;
        public IPushOfferBase PushOffer { get; set; }

        public SnapOrderOfferViewModel(ICommonServices commonServices) : base(commonServices)
        {
            messageService = commonServices.MessageService;
        }

        public override void Dispose()
        {
            base.Dispose();
            messageService.Unsubscribe(this, "FetchSnaporderOffer");
            GC.Collect();
        }

        public string PushOfferMessage
        {
            get
            {
                return message;
            }
            set
            {
                message = value;
                PushOffer.Message = value;
                NotifyPropertyChanged(nameof(PushOfferMessage));
            }
        }

        internal async void Load()
        {
            messageService.Subscribe("FetchSnaporderOffer", new TopicSubscription(this, OnFetchSnaporderOffer));
            StoreRegistrationDetails store = (await ServiceLocator.Current.GetService<ISettingService>().GetSettingsAsync()).Registration.StoreDetails;
            PushOffer = new SnaporderPushOfferMessageFactory().CreateMessage();
            PushOfferMessage = SnaporderPushOfferMessageBuilder.Instance.BuildMessage(url: CommonSettings.SnapOrderAppPlaystoreUrl);
        }

        private async void OnFetchSnaporderOffer(object arg1, string arg2, object arg3)
        {
            if (await PushOffer.IsValid())
            {
                var offer = new SnaporderPushOfferService().ConvertFromBaseToInfo(PushOffer as SnaporderPushOffer);
                messageService.Send(this, "SendThisOffer", offer);
            }
        }
    }
}