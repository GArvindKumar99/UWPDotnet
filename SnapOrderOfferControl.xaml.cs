using SnapBilling.PushOffers.ViewModels;
using SnapBilling.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SnapBilling.PushOffers
{
    
    public sealed partial class SnapOrderOfferControl : UserControl
    {
        private SnapOrderOfferViewModel _viewModel;
        internal SnapOrderOfferViewModel ViewModel 
        { 
            get
            {
                return _viewModel;
            } 
        }
        public SnapOrderOfferControl()
        {
            _viewModel = new SnapOrderOfferViewModel(ServiceLocator.Current.GetService<ICommonServices>());
            Loaded += SnapOrderOfferControl_Loaded;
            Unloaded += SnapOrderOfferControl_Unloaded;
        }

        private void SnapOrderOfferControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Dispose();
        }

        private void SnapOrderOfferControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.InitializeComponent();
            DataContext = ViewModel;
            ViewModel.Load();
        }
    }
}
