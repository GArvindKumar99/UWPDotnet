using Microsoft.Toolkit.Collections;
using Microsoft.Toolkit.Uwp;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.AppSettingClasses;
using SnapBilling.Services.AppServices.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WeihanLi.Extensions;

namespace SnapBilling.PushOffers
{
    public class CustomerIncrementalCollection : IncrementalLoadingCollection<CustomerSource, CustomerViewInfo>
    {
        public CustomerIncrementalCollection() : base(25)
        {
        }
    }

    public class CustomerSource : IIncrementalSource<CustomerViewInfo>
    {
        public CustomerSource()
        {
        }

        public async Task<IEnumerable<CustomerViewInfo>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
        {
            var customers_list = await ServiceLocator.Current.GetService<ICustomerService>().GetCustomersAsync(null, pageSize, pageIndex * pageSize); ;
            return customers_list.Where(x => !x.IsDisabled).ToList();
        }
    }

    public class PushOffersViewModel : ViewModelBase
    {
        public static int SelectedPushOffer = 0;
        private bool _isAllCustomersSelected = false;
        private IMessageService messageService = null;
        public PushOfferSmsCounter SmsCounter { get; set; }
        public PushOffersViewModel(ICommonServices commonServices) : base(commonServices)
        {
            messageService = commonServices.MessageService;
        }

        public ObservableCollection<CustomerViewInfo> CustomerAutoSuggestions { get; set; }

        public ObservableCollection<CustomerViewInfo> FinalList { get; set; }

        public CustomerIncrementalCollection IncrementalDisplayCustomerInfo { get; set; }

        public bool IsAllCustomersSelected
        {
            get
            {
                return _isAllCustomersSelected;
            }
            set
            {
                var language = GlobalStyles.Instance.Language;
                var all = GlobalStyles.Instance.Get(language, "will_send_to_all_customers");
                _isAllCustomersSelected = value;
                if (_isAllCustomersSelected)
                {
                    ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast(all, AlertType.Info);
                }
                //else
                //{
                //}
            }
        }

        public ICommand SelectCustomers => new RelayCommand(OnSelectCustomers);

        public ICommand Send => new RelayCommand(SendPushOffer);

        public string TotalCustomersCount { get; set; }

        public override void Dispose()
        {
            messageService.Send(this, "ClearView", null);
            messageService.Unsubscribe(this, "SendThisOffer");
            ClearView();
            base.Dispose();
        }

        internal async void Init()
        {
            FinalList = new ObservableCollection<CustomerViewInfo>();
            CustomerAutoSuggestions = new ObservableCollection<CustomerViewInfo>();
            IsAllCustomersSelected = false;
            NotifyPropertyChanged(nameof(IsAllCustomersSelected));
            //messageService.Subscribe("SendThisOldOffer", new TopicSubscription(this, OnSendThisOldOffer));
            messageService.Subscribe("SendThisOffer", new TopicSubscription(this, SendPushOffer));
            CreateEmptyCollection();
            await IncrementalDisplayCustomerInfo.RefreshAsync();
            Notify();
            await PushOfferHeadersCache.Instance.Init();
            SmsCounter = await ServiceLocator.Current.GetService<IPushOffersService>().GetPushOfferSmsCounter();
            NotifyPropertyChanged(nameof(SmsCounter));
        }

        internal void RemoveCustomerFromList(CustomerViewInfo customer)
        {
            try
            {
                var item = FinalList.Where(x => x.DisplayPhone == customer.DisplayPhone).FirstOrDefault();
                if (item != null)
                {
                    FinalList.Remove(item);
                }
                item = IncrementalDisplayCustomerInfo.Where(x => x.DisplayPhone == customer.DisplayPhone).FirstOrDefault();
                item.IsSelected = false;
                NotifyPropertyChanged(nameof(IncrementalDisplayCustomerInfo));
                NotifyPropertyChanged(nameof(FinalList));
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(PushOffersViewModel), ex);
            }
        }

        internal async void Search(string text)
        {
            try
            {
                CustomerAutoSuggestions = new ObservableCollection<CustomerViewInfo>(await ServiceLocator.Current.GetService<ICustomerService>().GetCustomersAsync(text, null, 0));
                NotifyPropertyChanged(nameof(CustomerAutoSuggestions));
            }
            catch (Exception ex)
            {
                LogError(this, ex);
            }
        }

        internal void SetPushOfferContext(int item)
        {
            SelectedPushOffer = item;
        }

        internal void UpdateList(object customer)
        {
            try
            {
                if (FinalList == null)
                {
                    FinalList = new ObservableCollection<CustomerViewInfo>();
                }
                var x = customer as CustomerViewInfo;
                bool match = false;
                foreach (var f in FinalList)
                {
                    if (x.DisplayPhone == f.DisplayPhone)
                    {
                        f.IsSelected = true;
                        match = true;
                    }
                }
                if (!match)
                {
                    x.IsSelected = true;
                    FinalList.Add(x);
                }

                ClearAutoSuggestBox();
                NotifyPropertyChanged(nameof(FinalList));
            }
            catch (Exception ex)
            {
                LogError(this, ex);
            }
        }

        private void ClearAutoSuggestBox()
        {
            if (CustomerAutoSuggestions != null)
            {
                int len = CustomerAutoSuggestions.Count;
                for (int i = 0; i < len; i++)
                {
                    var x = CustomerAutoSuggestions.ElementAt(i);
                    x = null;
                }

                CustomerAutoSuggestions = null;
                CustomerAutoSuggestions = new ObservableCollection<CustomerViewInfo>();
                NotifyPropertyChanged(nameof(CustomerAutoSuggestions));
            }
        }

        private void ClearGrid()
        {
            if (IncrementalDisplayCustomerInfo != null)
            {
                int len = IncrementalDisplayCustomerInfo.Count;
                for (int i = 0; i < len; i++)
                {
                    var x = IncrementalDisplayCustomerInfo.ElementAt(i);
                    x = null;
                }

                IncrementalDisplayCustomerInfo = null;
                NotifyPropertyChanged(nameof(IncrementalDisplayCustomerInfo));
            }
            if (FinalList != null)
            {
                int len = FinalList.Count;
                for (int i = 0; i < len; i++)
                {
                    var x = FinalList.ElementAt(i);
                    x = null;
                }

                FinalList = null;
                NotifyPropertyChanged(nameof(FinalList));
            }
        }

        private void ClearView()
        {
            ClearGrid();
            GC.Collect();
        }

        private void CreateEmptyCollection()
        {
            IncrementalDisplayCustomerInfo = null;
            IncrementalDisplayCustomerInfo = new CustomerIncrementalCollection();
            IncrementalDisplayCustomerInfo.OnEndLoading += Notify;
        }

        private void Notify()
        {
            try
            {
                NotifyPropertyChanged(nameof(IncrementalDisplayCustomerInfo));
            }
            catch (Exception ex)
            {
                LogError(this, ex);
            }
        }

        private void OnSelectCustomers()
        {
            var selectedCustomers = IncrementalDisplayCustomerInfo.Where(x => x.IsSelected);

            foreach (var s in selectedCustomers)
            {
                bool match = false;
                foreach (var f in FinalList)
                {
                    if (s.DisplayPhone == f.DisplayPhone)
                    {
                        f.IsSelected = true;
                        match = true;
                    }
                }
                if (!match)
                {
                    CustomerViewInfo customer = new CustomerViewInfo
                    {
                        Name = s.Name,
                        Phone = s.DisplayPhone,
                        IsSelected = true
                    };
                    FinalList.Add(customer);
                }
            }
            NotifyPropertyChanged(nameof(FinalList));
        }

        //private async void OnSendThisOldOffer(object arg1, string arg2, object arg3)
        //{
        //    try
        //    {
        //        var offer = arg3 as PushOfferInfo;
        //        if (string.IsNullOrEmpty(offer.Message) || string.IsNullOrWhiteSpace(offer.Message))
        //        {
        //            await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Message is invalid!", AlertType.Error);
        //        }
        //        else
        //        {
        //            var x = await SendPushOffer(offer.Message);
        //            if (x)
        //            {
        //                var result = await ServiceLocator.Current.GetService<IPushOffersService>().SavePushOfferAsync(offer);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(this, ex);
        //    }
        //}

        private async void SendPushOffer(object arg1, string arg2, object arg3)
        {
            var offer = arg3 as PushOfferInfo;

            try
            {
                List<long> phones = new List<long>();
                if (IsAllCustomersSelected)
                {
                    phones = await ServiceLocator.Current.GetService<IPushOffersService>().GetAllCustomersPhones();
                }
                else
                {
                    phones = FinalList.Where(x => x.IsSelected).Select(x => x.DisplayPhone).ToList();
                }
                if (phones.Count() == 0)
                {
                    await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Please select some/all customers", AlertType.Warning);
                }
                else
                {
                    if (!string.IsNullOrEmpty(offer.Message))
                    {
                        var isSent = await ServiceLocator.Current.GetService<IPushOffersService>().SendPushOffer(phones, offer);
                        if (isSent)
                        {
                            await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast(CommonSettings.PUSHOFFER_SUCCESS_MSG, AlertType.Success);
                            ServiceLocator.Current.GetService<ILogService>().Info(nameof(PushOffersViewModel), CommonSettings.PUSHOFFER_SUCCESS_MSG);
                            SmsCounter = await ServiceLocator.Current.GetService<IPushOffersService>().GetPushOfferSmsCounter();
                            NotifyPropertyChanged(nameof(SmsCounter));
                        }
                        else
                        {
                            throw new Exception("CommonSettings.PUSHOFFER_FAILURE_MSG");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast(CommonSettings.PUSHOFFER_FAILURE_MSG, AlertType.Error);
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(PushOffersViewModel), ex);
                var result = await ServiceLocator.Current.GetService<IPushOffersService>().SavePushOfferAsync(offer);
            }            
        }

        private void SendPushOffer()
        {
            switch (SelectedPushOffer)
            {
                case 0:
                    messageService.Send(this, "FetchStorewideOffer", null);
                    break;

                case 1:
                    messageService.Send(this, "FetchSpecialOffer", null);
                    break;

                case 2:
                    messageService.Send(this, "FetchSnaporderOffer", null);
                    break;
            }
        }
        
    }
}