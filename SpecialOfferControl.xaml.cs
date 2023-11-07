using SnapBilling.Core.UI;
using SnapBilling.PushOffers;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
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
    public sealed partial class SpecialOfferControl : UserControl
    {
        private SpecialOffersViewModel VM = new SpecialOffersViewModel(ServiceLocator.Current.GetService<ICommonServices>());
        public event Action<ProductPackCompact> ProductAdded;
        public SpecialOfferControl()
        {            
            Loaded += SpecialOfferControl_Loaded;
            Unloaded += SpecialOfferControl_Unloaded;
        }

        private void SpecialOfferControl_Unloaded(object sender, RoutedEventArgs e)
        {
            VM.Dispose();
        }

        private void SpecialOfferControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.InitializeComponent();
            DataContext = VM;
            AutoSuggestBox.CallbackOnThrottle(ProductSearched, 400);

            VM.Load();
        }
        private void ProductSearched(AutoSuggestBox box, string searchQuery)
        {
            if (searchQuery.Contains("\t"))
            {
                box.Text = "";
            }
            if (string.IsNullOrEmpty(searchQuery))
            {
                VM.Suggestions = null;
                VM.NotifyPropertyChanged(nameof(VM.Suggestions));
            }
            else
            {
                VM.Search(searchQuery);
            }
        }
        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is ProductPackCompact)
            {
                var productPack = args.ChosenSuggestion as ProductPackCompact;

                if (productPack != null && productPack.Name != "No Result")
                {
                    VM.UpdateMasterProductList(productPack);
                }
                sender.Text = "";
                AutoSuggestBox.ItemsSource = null;
                VM.Suggestions = null;
                AutoSuggestBox.Focus(FocusState.Programmatic);

            }
            else
            {
                if (sender.Text?.Length > 2)
                {
                    ProductSearched(sender, sender.Text);
                }
            }
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            VM.UpdateSlaveProductsList();
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
        private string GetValidValue(string text)
        {
            foreach (var c in text)
            {
                if (!char.IsDigit(c))
                    text = text.Remove(text.IndexOf(c), 1);
            }
            return text;
        }

        private void PriceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var text = (sender as ComboBox).Text;
            text = GetValidValue(text);
            (sender as ComboBox).Text = text;
            VM.Discountoffer = text == null ? "" : string.Format("Rs.{0}", text.ToString());
            VM.DiscountLong = text == null ? 0 : Convert.ToInt64(text);
        }               
        
    }
}