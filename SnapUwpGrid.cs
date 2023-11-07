using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace SnapBilling.Customer.Views
{
    public class SnapUwpGrid : Grid
    {
        public SnapUwpGrid()
        {
            Margin = new Windows.UI.Xaml.Thickness(20);
            CornerRadius = new Windows.UI.Xaml.CornerRadius(20d);
            Background = new SolidColorBrush(Windows.UI.Colors.White);
        }
    }
}