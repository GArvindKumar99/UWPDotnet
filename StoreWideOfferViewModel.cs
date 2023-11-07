using SnapBilling.Data;
using SnapBilling.PushOffers.Services;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.AppSettingClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.UI.Xaml;

namespace SnapBilling.PushOffers
{
    public class DiscountType
    {
        public int Id { get; set; }
        public string Value { get; set; }
    }

    public class StoreWideOfferViewModel : ViewModelBase
    {
        private readonly IMessageService messageService = null;
        private string _discount;
        private string _discountText;
        private string message = "";
        private string suffix = "";
        private DateTimeOffset _fromDate;
        private DateTimeOffset _toDate;
        private double _discountAmount = 0;

        public StoreWideOfferViewModel(ICommonServices commonServices) : base(commonServices)
        {
            messageService = commonServices.MessageService;
        }
        public StoreWidePushOffer PushOffer { get; set; }

        public double DiscountLong {
            get
            {
                return _discountAmount;
            }
            set
            {
                _discountAmount = value;
                PushOffer.DiscountOffer = value;
            }
        }
        public string Discountoffer
        {
            get
            {
                return _discount;
            }
            set
            {
                _discount = value;
                UpdatePushOfferMessage();
            }
        }

        public int DiscountOffer { get; set; }
        public List<int> DiscountPercents { get; set; }
        public List<int> DiscountPrices { get; set; }
        public string DiscountText
        {
            get
            {
                return _discountText;
            }
            set
            {
                _discountText = string.IsNullOrEmpty(value) ? "0" : value;
                if (SelectedDiscountType.Id == 2)
                {
                    Discountoffer = string.Format("Rs. {0}", _discountText);
                }
                else if (SelectedDiscountType.Id == 1)
                {
                    Discountoffer = string.Format("{0} %", _discountText);
                }
                else
                {
                    Discountoffer = _discountText;
                }
                DiscountLong = Convert.ToDouble(_discountText);
            }
        }

        public List<DiscountType> DiscountTypes { get; set; }
        public bool DiscountTypeSelectEnable
        {
            get;
            set;
        }

        public string DiscPercText { get; set; }
        public string DiscPriceText { get; set; }
        public DateTimeOffset FromDate
        {
            get
            {
                return _fromDate;
            }
            set
            {
                _fromDate = value;
                PushOffer.ValidFrom = value.Date;
                UpdatePushOfferMessage();
            }
        }
        public Visibility PercBoxVisible { get; set; }
        public Visibility PriceBoxVisible { get; set; }
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

        public DiscountType SelectedDiscountType { get; set; }
        public DateTimeOffset ToDate
        {
            get
            {
                return _toDate;
            }
            set
            {
                _toDate = value;
                PushOffer.ValidTo = value.Date;
                UpdatePushOfferMessage();
            }
        }
        public static string GetMessageSuffix(string storeName, string phone)
        {
            return $"{storeName} {phone}";
        }

        public override void Dispose()
        {
            base.Dispose();
            messageService.Unsubscribe(this, "FetchStorewideOffer");
            messageService.Unsubscribe(this, "ClearView");
        }

        internal void DisableDiscountOptions()
        {
            PercBoxVisible = Visibility.Collapsed;
            PriceBoxVisible = Visibility.Collapsed;
            NotifyPropertyChanged(nameof(PercBoxVisible));
            NotifyPropertyChanged(nameof(PriceBoxVisible));
        }
        public StoreRegistrationDetails StoreRegistrationDetails { get; set; }

        internal void EnablePerc()
        {
            PercBoxVisible = Visibility.Visible;
            PriceBoxVisible = Visibility.Collapsed;
            NotifyPropertyChanged(nameof(PercBoxVisible));
            NotifyPropertyChanged(nameof(PriceBoxVisible));
        }

        internal void EnablePrice()
        {
            PercBoxVisible = Visibility.Collapsed;
            PriceBoxVisible = Visibility.Visible;
            NotifyPropertyChanged(nameof(PercBoxVisible));
            NotifyPropertyChanged(nameof(PriceBoxVisible));
        }

        internal async void Load()
        {
            PushOffer = new StoreWidePushOffer();
            StoreRegistrationDetails = (await ServiceLocator.Current.GetService<ISettingService>().GetSettingsAsync()).Registration.StoreDetails;
            PushOffer = new StoreWidePushOfferMessageFactory().CreateMessage();

            messageService.Subscribe("ClearView", new TopicSubscription(this, ClearView));
            MessageService.Subscribe("FetchStorewideOffer", new TopicSubscription(this, OnFetchStorewideOffer));

            DiscountPercents = new List<int>() { 5, 10, 15, 20 };
            DiscountPrices = new List<int>() { 50, 100, 150, 200 };
            InitDiscountTypes();
            DiscountTypeSelectEnable = true;
            PercBoxVisible = Visibility.Collapsed;
            PriceBoxVisible = Visibility.Collapsed;
            FromDate = new DateTimeOffset(DateTime.Now);
            ToDate = new DateTimeOffset(DateTime.Now);
            _discount = string.Empty;
            SettingInfo settings = await ServiceLocator.Current.GetService<ISettingService>().GetSettingsAsync();
            StoreRegistrationDetails store = settings.Registration.StoreDetails;
            suffix = GetMessageSuffix(store.Name, store.Phone);
            UpdatePushOfferMessage();
        }

        private void ClearView(object arg1, string arg2, object arg3)
        {
            InitDiscountTypes();
            FromDate = new DateTimeOffset(DateTime.Now);
            ToDate = new DateTimeOffset(DateTime.Now);
            NotifyPropertyChanged(nameof(FromDate));
            NotifyPropertyChanged(nameof(ToDate));
            GC.Collect();
        }

        private void InitDiscountTypes()
        {
            var language = GlobalStyles.Instance.Language;
            DiscountTypes = new List<DiscountType>()
            {

                new DiscountType(){Id=0, Value= GlobalStyles.Instance.Get(language, "none")},
                new DiscountType(){Id=1, Value=GlobalStyles.Instance.Get(language, "percentages") },
                new DiscountType(){Id=2, Value=GlobalStyles.Instance.Get(language, "prices") },
            };
            SelectedDiscountType = new DiscountType();
            SelectedDiscountType = DiscountTypes.ElementAt(0);
            NotifyPropertyChanged(nameof(DiscountTypes));
            NotifyPropertyChanged(nameof(SelectedDiscountType));
        }

        private async void OnFetchStorewideOffer(object arg1, string arg2, object arg3)
        {
            if (await PushOffer.IsValid())
            {
                var offer = new StoreWidePushOfferService().ConvertFromBaseToInfo(PushOffer);
                messageService.Send(this, "SendThisOffer", offer);
            }
        }

        private void UpdatePushOfferMessage()
        {
            PushOfferMessage = StoreWidePushOfferMessageBuilder.Instance.BuildMessage(discount: Discountoffer, fromDate: FromDate, toDate: ToDate);
        }
    }
}