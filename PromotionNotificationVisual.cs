using SnapBilling.Services.AppServices;

namespace SnapBilling.CartModule.Services
{
    internal class CartNotificationVisual : INotificationVisual
    {
        public CartNotificationVisual(string description,bool isEnabled)
        {
            Description = description;
            IsEnabled = isEnabled;
        }
        public string Description { get; private set; }
        public bool IsEnabled { get; private set; } 
    }
}