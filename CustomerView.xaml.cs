using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using SnapBilling.Core.UI;
using SnapBilling.Customer.ContentDialogs;
using SnapBilling.Customer.ViewModel;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.Services;
using System;
using System.Linq;
using System.Reactive.Linq;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace SnapBilling.Customer.Views
{
    public sealed partial class CustomerView : AuthorizablePrimaryViewPage
    {
        private CustomerViewModel vm;
        private Flyout BillDetailsFlyout { get; set; }

        private BillDetailView BillDetailView { get; set; }
        public CustomerView()
        {
            Loaded += CustomerViewLoaded;
            Unloaded += CustomerView_Unloaded;          
        }
        public override void Initialize()
        {
            //Add(new InvoicesListView());

            this.Clear();
            Add(new BillDetailView());
            this.Add(new FrameworkAuthorizableResource("buttonNew") { Description = "Add New Customer" });
            this.Add(new FrameworkAuthorizableResource("ClearCustomerDueButton") { Description = "Pay Due" });
            //this.Add(new FrameworkAuthorizableResource("BillItem") { Description = "Bill details" });
            //this.Add(new FrameworkAuthorizableResource("Wallet") { Description = "Customer Wallet" });
            //this.Add(new FrameworkAuthorizableResource("CustomerDetails") { Description = "Customer Details" });
        }

        private void Kb_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            pivotMain.SelectedIndex = 1;
        }

        private void CustomerView_Unloaded(object sender, RoutedEventArgs e)
        {
            search.Text = "";
            vm.Dispose();
        }

        internal CustomerViewModel ViewModel
        {
            get { return vm; }
        }

        private async void BillDatePickerDateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            try
            {
                var datePicker = sender as DatePicker;
                if (datePicker == null) return;
                vm.CurrentMonth = datePicker.Date.DateTime;
                await vm.GetBillsAsync();
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CustomerView), "", ex.Message, ex.StackTrace);
            }
        }

        private void CustomerViewLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.InitializeComponent();
            if (vm == null)
            {
                vm = new CustomerViewModel(ServiceLocator.Current.GetService<ICommonServices>());
                DataContext = vm;
                KeyboardAccelerators.Clear();

                var language = GlobalStyles.Instance.Language;
                var kb = new SnapVKey();
                kb.Key = Windows.System.VirtualKey.F;
                kb.Modifiers = Windows.System.VirtualKeyModifiers.Control;
                kb.Invoked += KeyboardAccelerator_Invoked_Search;
                kb.Description = GlobalStyles.Instance.Get(language, "customer_search");
                KeyboardAccelerators.Add(kb);

                var kb1 = new SnapVKey();
                kb1.Key = Windows.System.VirtualKey.T;
                kb1.Modifiers = Windows.System.VirtualKeyModifiers.Control;
                kb1.Invoked += FocusGrid;
                kb1.Description = GlobalStyles.Instance.Get(language, "customer_table");

                KeyboardAccelerators.Add(kb1);
                var isClient = ServiceLocator.Current.GetService<ISettingService>().IsMultiPosEnabled().Result.Value;
                if (!isClient)
                {
                    var kb2 = new SnapVKey();
                    kb2.Key = Windows.System.VirtualKey.F2;
                    kb2.Invoked += Kb_Invoked;
                    kb2.Description = GlobalStyles.Instance.Get(language, "customer_bills");
                    KeyboardAccelerators.Add(kb2);
                }
                else
                {
                    var c = pivotMain.Items;
                    c.Remove(BillItem);
                    c.Remove(Wallet);
                }

            }
            var test = this.FindChildByName("buttonNew");
            vm.Loaded();
            this.ValidateResources();
        }

        private async void PivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pivotItem = (pivotMain.SelectedItem as Windows.UI.Xaml.Controls.PivotItem).Name;
            await vm.PivotSelectionChanged(pivotItem);

            CustomerPivotContext.Instance.Update(pivotItem);

            //if (e.AddedItems.Count > 0)
            //{
            //    var selectedPivotItem = e.AddedItems[0] as PivotItem;
            //}
            //if (e.RemovedItems.Count > 0)
            //{
            //    var unselectedPivotItem = e.RemovedItems[0] as PivotItem;
            //}

            //var pivotIndex = pivotMain.SelectedIndex;
            //this.ValidateResources();
            //switch (pivotIndex)
            //{
            //    case 0:
            //        break;

            //    case 1:
            //        await vm.GetBillsAsync();
            //        break;

            //    case 2:
            //        await vm.GetCustomerWalletAsync();
            //        break;                
            //}
        }

        private void SearchTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var searchString = search?.Text;

                if (string.IsNullOrEmpty(searchString))
                {
                    vm.MessageService.Send(this, "OnCustomerSearch", searchString);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(CustomerView),ex);
            }
        }

        private void SearchEnterClicked(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    var searchString = search?.Text;
                    vm.MessageService.Send(this, "OnCustomerSearch", searchString);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(CustomerView), ex);
            }
        }

        private async void WalletDatePickerDateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            try
            {
                var datePicker = sender as DatePicker;
                if (datePicker == null) return;
                vm.CurrentMonth = datePicker.Date.DateTime;
                await vm.GetCustomerWalletAsync();
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CustomerView), "", ex.Message, ex.StackTrace);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            vm.Dispose();
        }

        public string GetViewName()
        {
            return "CustomerView";
        }

        private void KeyboardAccelerator_Invoked_Search(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            search.Focus(FocusState.Keyboard);
            args.Handled = true;
        }

        private void FocusGrid(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            customerListView.FocusDataGrid(sender, args);
        }

        
        public bool CheckIfCustomerTagged()
        {
            return BillingContext.Current.ActiveBillingCarts.SingleOrDefault(x => x.CurrentInvoice.Customer?.Phone == vm.CustomerInfo?.Phone) != null;
        }

        private async void Flyout_Opening(object sender, object e)
        {
            var language = GlobalStyles.Instance.Language;
            var err = GlobalStyles.Instance.Get(language, "error");
            var curr = GlobalStyles.Instance.Get(language, "current_customer_is_already_tagged_in_one_one_of_the_carts");
            var ok = GlobalStyles.Instance.Get(language, "ok");
            var sen = sender as Flyout;
            if (CheckIfCustomerTagged())
            {
                sen.Hide();
                await ServiceLocator.Current.GetService<IDialogService>().ShowAsync(err,curr,ok);
            }
            if (vm.CustomerInfo.IsDisabled)
            {
                sen.Hide();
                var ava = GlobalStyles.Instance.Get(language, "not_available_for_disable_customers");
                await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast(ava);
            }
        }

        private async void RefreshBillsClicked(object sender, RoutedEventArgs e)
        {
            await vm.GetBillsAsync();
        }

        private async void RefreshWalletClicked(object sender, RoutedEventArgs e)
        {
            await vm.GetCustomerWalletAsync();
        }

        private void TransactionsForInvoice(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var invoice = button.Tag as InvoiceInfo;
            vm.GetTransactionByInvoice(invoice.Id);
        }

        private async void RemindDueClicked(object sender, RoutedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _ = vm.DueRemindAsync();
            });
        }        

        private void ShowCustomerDetailsBtn_Click(object sender, RoutedEventArgs e)
        { 
            parentGrid.RowDefinitions[1].Height = new GridLength(5, GridUnitType.Star);
            parentGrid.RowDefinitions[2].Height = new GridLength(5, GridUnitType.Star);
            ShowCustomerDetailsBtn.Visibility = Visibility.Collapsed;
            HideCustomerDetailsBtn.Visibility = Visibility.Visible;
        }

        private void HideCustomerDetailsBtn_Click(object sender, RoutedEventArgs e)
        {
            parentGrid.RowDefinitions[1].Height = new GridLength(10, GridUnitType.Star);
            parentGrid.RowDefinitions[2].Height = new GridLength(0, GridUnitType.Star);
            ShowCustomerDetailsBtn.Visibility = Visibility.Visible;
            HideCustomerDetailsBtn.Visibility = Visibility.Collapsed;
        }

        private void OpenBillDetails(object sender, RoutedEventArgs e)
        {
            var sen = sender as Button;
            var invoice = sen.Tag as InvoiceInfo;
            if (invoice != null)
            {
                vm.ShowBillDetails(invoice);

            }
            //BillDetailsFlyout = (Flyout)sen.Flyout;
        }

        private void CloseBillDetails(object sender, RoutedEventArgs e)
        {
            BillDetailsFlyout?.Hide();

        }

        private void dataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            var sen = sender as DataGrid;

            if (sen.CurrentColumn != null && sen.CurrentColumn?.Header?.ToString() == "Invoice No.")
            {
                sen.BeginEdit();

            }
        }
        
        private async void ClearDueButton_Click(object sender, RoutedEventArgs e)
        {
            var language = GlobalStyles.Instance.Language;
            var err = GlobalStyles.Instance.Get(language, "error");
            var cus = GlobalStyles.Instance.Get(language, "current_customer_is_already_tagged_in_one_one_of_the_carts");
            var ok = GlobalStyles.Instance.Get(language, "ok");
            if (CheckIfCustomerTagged())
            {
                await ServiceLocator.Current.GetService<IDialogService>().ShowAsync(err,cus,ok);
                return;
            }
            if (vm.CustomerInfo.IsDisabled)
            {
                var dis = GlobalStyles.Instance.Get(language, "not_available_for_disable_customers");
                await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast(dis);
                return;
            }
            vm.ClearDueClicked();
        }

        private async void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            HelpContentDialog dialog = new HelpContentDialog();
            await dialog.LoadVideo(1786,2339);

        }

        private void pivotMain_Loaded(object sender, RoutedEventArgs e)
        {
            pivotMain.SelectedIndex = 0;
        }
    }
}