using Microsoft.Toolkit.Collections;
using Microsoft.Toolkit.Uwp;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SnapBilling.PushOffers
{
    public class PreviousOffersViewModel : ViewModelBase
    {
        private readonly IMessageService messageService = null;

        public PreviousOffersViewModel(ICommonServices commonServices) : base(commonServices)
        {
            messageService = commonServices.MessageService;
        }

        public ICommand View => new RelayCommand(ViewPreviousOffers);
        public PushOfferIncrementalCollection PushOffersIncrementalCollection { get; set; }
        private PushOfferInfo _selectedOffer;

        public PushOfferInfo SelectedOffer
        {
            get
            {
                return _selectedOffer;
            }
            set
            {
                _selectedOffer = value;
                PushOfferMessage = SelectedOffer == null ? "" : _selectedOffer.Message;
                NotifyPropertyChanged(nameof(PushOfferMessage));
            }
        }

        private async void ViewPreviousOffers()
        {
            PushOffersIncrementalCollection = new PushOfferIncrementalCollection();
            PushOffersIncrementalCollection.SetSearchString(FromDate.Ticks, ToDate.Ticks);
            await PushOffersIncrementalCollection.RefreshAsync();
            Notify();
        }

        private void Notify()
        {
            NotifyPropertyChanged(nameof(PushOffersIncrementalCollection));
        }
        
        public DateTimeOffset FromDate { get; set; }

        public DateTimeOffset ToDate { get; set; }

        public override void Dispose()
        {
            base.Dispose();
            messageService.Unsubscribe(this, "ClearView");
        }

        public string PushOfferMessage { get; set; }


        private void ClearGrid()
        {
            if (PushOffersIncrementalCollection != null)
            {
                int len = PushOffersIncrementalCollection.Count;
                for (int i = 0; i < len; i++)
                {
                    var x = PushOffersIncrementalCollection.ElementAt(i);
                    x = null;
                }

                PushOffersIncrementalCollection = null;
                NotifyPropertyChanged(nameof(PushOffersIncrementalCollection));
            }
        }

        internal void Loaded()
        {
            messageService.Subscribe("ClearView", new TopicSubscription(this, ClearView));
            messageService.Subscribe("FetchPreviousOffer", new TopicSubscription(this, OnFetchPreviousOffer));

            FromDate = new DateTimeOffset(DateTime.Today);
            ToDate = new DateTimeOffset(DateTime.Today.AddDays(1));
        }

        private async void OnFetchPreviousOffer(object arg1, string arg2, object arg3)
        {
            if (SelectedOffer == null)
            {
                var language = GlobalStyles.Instance.Language;
                var sele = GlobalStyles.Instance.Get(language, "plz_select_some_offer");
                await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast(sele, AlertType.Warning);
            }
            else
            {
                SelectedOffer.SendAt = DateTime.Now.Ticks;
                SelectedOffer.Message = PushOfferMessage;
                //messageService.Send(this, "SendThisOldOffer", SelectedOffer);
            }
        }

        private void ClearView(object arg1, string arg2, object arg3)
        {
            ClearGrid();
            PushOfferMessage = "";
            NotifyPropertyChanged(nameof(PushOfferMessage));

            FromDate = new DateTimeOffset(DateTime.Today);
            ToDate = new DateTimeOffset(DateTime.Today.AddDays(1));
            NotifyPropertyChanged(nameof(FromDate));
            NotifyPropertyChanged(nameof(ToDate));
            GC.Collect();
        }
    }

    public class PushOfferSource : IIncrementalSource<PushOfferInfo>
    {
        public PushOfferSource()
        {
        }

        public long ValidFrom { get; set; }
        public long ValidTo { get; set; }

        public async Task<IEnumerable<PushOfferInfo>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
        {
            return await ServiceLocator.Current.GetService<IPushOffersService>().FetchPreviousPushOffers(ValidFrom, ValidTo, pageSize, pageIndex * pageSize);
        }
    }

    public class PushOfferIncrementalCollection : IncrementalLoadingCollection<PushOfferSource, PushOfferInfo>
    {
        public PushOfferIncrementalCollection() : base(100)
        {
        }

        internal void SetSearchString(long from, long to)
        {
            Source.ValidFrom = from;
            Source.ValidTo = to;
        }
    }
}