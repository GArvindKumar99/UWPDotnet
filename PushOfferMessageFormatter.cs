using System.Text;

namespace SnapBilling.PushOffers.Services
{
    public static class PushOfferMessageFormatter
    {
        public static string TrimTo30(string message)
        {           
            if (message.Length > 27)
            {
                StringBuilder finalmessage = new StringBuilder(message.Substring(0, 27)).Append("...");
                return finalmessage.ToString();
            }
            else
            {
                return message.Substring(0, message.Length);
            }
        }
        public static string TrimTo60(string message) 
        {
            if (message.Length > 57)
            {
                StringBuilder finalmessage = new StringBuilder(message.Substring(0, 57)).Append("...");
                return finalmessage.ToString();
            }
            else
            {
                return message.Substring(0, message.Length);
            }
        }
    }
}
