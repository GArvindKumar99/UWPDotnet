using SnapBilling.PushOffers.Services;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.AppSettingClasses;
using SnapBilling.Services.AppServices.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;

namespace SnapBilling.PushOffers
{
    public class ProductList : ProductPackCompact
    {
        public bool IsSelected { get; set; } = true;
    }

    public class SpecialOffersViewModel : ViewModelBase
    {
        private readonly IMessageService messageService = null;

        private string _discount;
        private double _discountAmount = 0;
        private string _discountText;
        private DateTimeOffset _fromDate;
        private DateTimeOffset _toDate;
        private string message = "";

        public SpecialOffersViewModel(ICommonServices commonServices) : base(commonServices)
        {
            messageService = commonServices.MessageService;
        }

        public decimal Discount { get; set; }

        public double DiscountLong
        {
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

        public ObservableCollection<ProductList> MasterChosenProductList { get; set; }
        public Visibility PercBoxVisible { get; set; }
        public Visibility PriceBoxVisible { get; set; }
        public string ProductListText { get; set; } = "";
        public SpecialPushOffer PushOffer { get; set; }

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
        public ObservableCollection<ProductList> SlaveChosenProductList { get; set; }
        public StoreRegistrationDetails StoreRegistrationDetails { get; set; }
        public ObservableCollection<ProductPackCompact> Suggestions { get; set; }

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

        public override void Dispose()
        {
            base.Dispose();
            messageService.Unsubscribe(this, "ClearView");
            messageService.Unsubscribe(this, "FetchSpecialOffer");
        }

        public async void Load()
        {
            PushOffer = new SpecialPushOffer();
            StoreRegistrationDetails = (await ServiceLocator.Current.GetService<ISettingService>().GetSettingsAsync()).Registration.StoreDetails;
            PushOffer = new SpecialPushOfferMessageFactory().CreateMessage();

            messageService.Subscribe("ClearView", new TopicSubscription(this, ClearView));
            messageService.Subscribe("FetchSpecialOffer", new TopicSubscription(this, OnFetchSpecialOffer));
            DiscountPercents = new List<int>() { 5, 10, 15, 20 };
            DiscountPrices = new List<int>() { 50, 100, 150, 200 };
            InitDiscountTypes();
            DiscountTypeSelectEnable = true;
            PercBoxVisible = Visibility.Collapsed;
            PriceBoxVisible = Visibility.Collapsed;

            Suggestions = new ObservableCollection<ProductPackCompact>();
            MasterChosenProductList = new ObservableCollection<ProductList>();
            FromDate = new DateTimeOffset(DateTime.Now);
            ToDate = new DateTimeOffset(DateTime.Now);

            UpdatePushOfferMessage();
        }

        //Shows auto suggest suggestions
        public async void Search(string text)
        {
            try
            {
                Suggestions = new ObservableCollection<ProductPackCompact>(await ServiceLocator.Current.GetService<IProductCatalogService>().SearchProductCompact(text));

                NotifyPropertyChanged(nameof(Suggestions));
                if (Suggestions.Count == 0)
                {
                    await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("No product found!", AlertType.Warning);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(SpecialOffersViewModel), ex);
            }
        }

        public void UpdateMasterProductList(object product)
        {
            var x = product as ProductPackCompact;
            bool duplicate = false;
            foreach (var c in MasterChosenProductList)
            {
                if (c.ProductCode == x.ProductCode)
                {
                    duplicate = true;
                }
            }

            if (!duplicate)
            {
                MasterChosenProductList.Add(new ProductList() { Name = x.Name, ProductCode = x.ProductCode });
            }
            NotifyPropertyChanged(nameof(MasterChosenProductList));
        }

        public void UpdateSlaveProductsList()
        {
            SlaveChosenProductList = new ObservableCollection<ProductList>(MasterChosenProductList.Where(x => x.IsSelected));
            UpdateProductListText();
        }

        internal void DisableDiscountOptions()
        {
            PercBoxVisible = Visibility.Collapsed;
            PriceBoxVisible = Visibility.Collapsed;
            NotifyPropertyChanged(nameof(PercBoxVisible));
            NotifyPropertyChanged(nameof(PriceBoxVisible));
        }

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

        private void ClearGrid()
        {
            if (Suggestions != null)
            {
                int len = Suggestions.Count;
                for (int i = 0; i < len; i++)
                {
                    var x = Suggestions.ElementAt(i);
                    x = null;
                }

                Suggestions = null;
                NotifyPropertyChanged(nameof(Suggestions));
            }
            if (MasterChosenProductList != null)
            {
                var len = MasterChosenProductList.Count;
                for (int i = 0; i < len; i++)
                {
                    var x = MasterChosenProductList.ElementAt(i);
                    x = null;
                }

                MasterChosenProductList = null;
                NotifyPropertyChanged(nameof(MasterChosenProductList));
            }
            if (SlaveChosenProductList != null) 
            { 
                var len = SlaveChosenProductList.Count;
                for (int i = 0; i < len; i++)
                {
                    var x = SlaveChosenProductList.ElementAt(i);
                    x = null;
                }

                SlaveChosenProductList = null;
                NotifyPropertyChanged(nameof(SlaveChosenProductList));
            }
        }

        private void ClearView(object arg1, string arg2, object arg3)
        {
            InitDiscountTypes();
            ClearGrid();
            ProductListText = String.Empty;
            FromDate = new DateTimeOffset(DateTime.Now);
            ToDate = new DateTimeOffset(DateTime.Now);
            NotifyPropertyChanged(nameof(FromDate));
            NotifyPropertyChanged(nameof(ToDate));
            GC.Collect();
        }

        private void InitDiscountTypes()
        {
            DiscountTypes = new List<DiscountType>()
            {
                new DiscountType(){Id=0, Value="None" },
                new DiscountType(){Id=1, Value="Percentages" },
                new DiscountType(){Id=2, Value="Prices" },
            };
            SelectedDiscountType = new DiscountType();
            SelectedDiscountType = DiscountTypes.ElementAt(0);
            NotifyPropertyChanged(nameof(DiscountTypes));
            NotifyPropertyChanged(nameof(SelectedDiscountType));
        }

        private async void OnFetchSpecialOffer(object arg1, string arg2, object arg3)
        {
            if (await PushOffer.IsValid())
            {
                var offer = new SpecialPushOfferService().ConvertFromBaseToInfo(PushOffer as SpecialPushOffer);
                messageService.Send(this, "SendThisOffer", offer);
            }
        }

        private void UpdateProductListText()
        {
            var products = SlaveChosenProductList.Select(x => x.Name);
            ProductListText = string.Join(",", products);
            PushOffer.ProductNameList = products.ToList();
            UpdatePushOfferMessage();
        }

        private void UpdatePushOfferMessage()
        {
            PushOfferMessage = SpecialPushOfferMessageBuilder.Instance.BuildMessage(products: ProductListText, discount: Discountoffer, fromDate: FromDate, toDate: ToDate);
            NotifyPropertyChanged(nameof(PushOfferMessage));
        }
    }
}