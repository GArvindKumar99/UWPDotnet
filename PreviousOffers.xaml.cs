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
    public sealed partial class PreviousOffers : UserControl
    {
        private PreviousOffersViewModel VM = new PreviousOffersViewModel(ServiceLocator.Current.GetService<ICommonServices>());

        public PreviousOffers()
        {
            Loaded += PreviousOffers_Loaded;
            Unloaded += PreviousOffers_Unloaded;
        }

        private void PreviousOffers_Unloaded(object sender, RoutedEventArgs e)
        {
            VM.Dispose();
        }

        private void PreviousOffers_Loaded(object sender, RoutedEventArgs e)
        {
            this.InitializeComponent();
            DataContext = VM;
            VM.Loaded();
        }
    }
}