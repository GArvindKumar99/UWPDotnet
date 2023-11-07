using SnapBilling.PushOffers;
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
    public sealed partial class StoreWideOfferControl : UserControl
    {
        private StoreWideOfferViewModel VM = new StoreWideOfferViewModel(ServiceLocator.Current.GetService<ICommonServices>());

        public StoreWideOfferControl()
        {
            Loaded += StoreWideOfferControl_Loaded;
            Unloaded += StoreWideOfferControl_Unloaded;
            
        }

        private void StoreWideOfferControl_Unloaded(object sender, RoutedEventArgs e)
        {
            VM.Dispose();
        }

        private void StoreWideOfferControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.InitializeComponent();
            DataContext = VM;
            VM.Load();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cmb = sender as ComboBox;
            VM.Discountoffer = string.Empty;

            switch (cmb.SelectedIndex)
            {
                case 0:
                    VM.DisableDiscountOptions();
                    break;

                case 1:
                    VM.EnablePerc();
                    break;

                case 2:
                    VM.EnablePrice();
                    break;
            }
        }        

        private void PercentageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var text = (sender as ComboBox).Text;
            text = GetValidValue(text);
            (sender as ComboBox).Text = text;
            VM.Discountoffer = text == null ? "" : string.Format("{0}%", text.ToString());
            VM.DiscountLong = text == null ? 0 : Convert.ToInt64(text);
        }

        private void PriceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var text = (sender as ComboBox).Text;
            text = GetValidValue(text);
            (sender as ComboBox).Text = text;
            VM.Discountoffer = text == null ? "" : string.Format("Rs.{0}", text.ToString());
            VM.DiscountLong = text == null ? 0 : Convert.ToInt64(text);
        }        

        private string GetValidValue(string text)
        {
            foreach (var c in text)
            {
                if (!char.IsDigit(c))
                    text = text.Remove(text.IndexOf(c), 1);
            }
            return text;
        }
        
    }
}