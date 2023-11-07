using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Controls;
using SnapBilling.Core.UI;
using SnapBilling.Customer.ViewModel;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.AppSettingClasses;
using System;
using Telerik.Data.Core;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SnapBilling.Customer.Views
{
    public sealed partial class CustomersListView : UserControl
    {
        private CustomersListViewModel vm { get; set; }
        private static bool _toSnapOrder;
        public CustomersListView()
        {
            Loaded += CustomersListViewLoaded;
            Unloaded += CustomersListViewUnloaded;
        }

        internal CustomersListViewModel ViewModel
        {
            get
            {
                return vm;
            }
        }

        private async void CustomersListViewLoaded(object sender, RoutedEventArgs e)
        {
            if (vm == null)
            {
                this.InitializeComponent();
                vm = new CustomersListViewModel(ServiceLocator.Current.GetService<ICommonServices>());
            }
            DataContext = vm;
            vm.DataGrid = DataGrid;
            vm.Loaded();
            _toSnapOrder = (await ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync()).ToSnapOrder;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var size= base.ArrangeOverride(finalSize);
            DataGrid.ResizeColumns();
            return size;
        }


        private void CustomersListViewUnloaded(object sender, RoutedEventArgs e)
        {
            vm.Dispose();
        }

        private void OnFocus(object sender, RoutedEventArgs e)
        {
            DataGrid.BorderBrush = new SolidColorBrush(Colors.DarkGray);
            
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            DataGrid.BorderBrush = new SolidColorBrush(Colors.Transparent);
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var CustomerInfo = (sender as DataGrid).SelectedItem;

            vm?.CustomerSelectionChange(CustomerInfo);
        }
        public void FocusDataGrid(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            DataGrid.Focus(FocusState.Programmatic);
            args.Handled = true;
        }
        private void DataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            try
            {
                if (e.Column.Tag.ToString() == "Name" || e.Column.Tag.ToString() == "Phone" || e.Column.Tag.ToString() == "Alternatephone" || e.Column.Tag.ToString() == "DisplayAmountDue" || e.Column.Tag.ToString() == "CreatedAtDateTime")
                {
                    if (e.Column.Tag.ToString() == "Name" || e.Column.Tag.ToString() == "Phone" || e.Column.Tag.ToString() == "Alternatephone" || e.Column.Tag.ToString() == "DisplayAmountDue" || e.Column.Tag.ToString() == "CreatedAtDateTime")
                    {
                        if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
                        {
                            vm.CustomerInfoList.SortDescriptions.Clear();
                            vm.CustomerInfoList.SortDescriptions.Add(new SortDescription(e.Column.Tag.ToString(), SortDirection.Ascending));
                            e.Column.SortDirection = DataGridSortDirection.Ascending;
                        }
                        else
                        {
                            vm.CustomerInfoList.SortDescriptions.Clear();
                            vm.CustomerInfoList.SortDescriptions.Add(new SortDescription(e.Column.Tag.ToString(), SortDirection.Descending));
                            e.Column.SortDirection = DataGridSortDirection.Descending;
                        }
                        vm.NotifyPropertyChanged(nameof(vm.CustomerInfoList));
                    }
                    foreach (var dgColumn in DataGrid.Columns)
                    {
                        if (dgColumn.Tag.ToString() != e.Column.Tag.ToString())
                        {
                            dgColumn.SortDirection = null;
                        }
                    }
                }
            }catch(Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CustomersListView), "DataGrid_Sorting", ex.Message, ex.StackTrace);

            }
        }

        private void ForceSnapOrderClick(object sender, RoutedEventArgs e)
        {
            var cus = ((Button)sender).Tag as CustomerViewInfo;
            if (cus != null)
            {
                vm.ForceSnapOrderSync(cus);
            }
        }

        private void ForceSnapOrderTapped(object sender, TappedRoutedEventArgs e)
        {
            var cus = ((Grid)sender).Tag as CustomerViewInfo;
            if (cus != null)
            {
                vm.ForceSnapOrderSync(cus);
            }
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            var sen = sender as Grid;
            sen.Visibility = _toSnapOrder ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public class CustomIKeyLookup : IKeyLookup
    {
        public object GetKey(object instance)
        {
            var d = instance as CustomerViewInfo;
            return d.Name == null || d.Name.Length == 0 ? " "[0] : d.Name[0];
        }
    }
}