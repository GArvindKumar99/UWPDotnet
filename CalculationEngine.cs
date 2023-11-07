using SnapBilling.Services.AppServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapBilling.CartModule.Services
{
    //public class CalculationEngine : ICalculationEngine
    //{
    //    List<CalculationBase> list = new List<CalculationBase>();
    //    ICartService cartService;
    //    public CalculationEngine(ICartService cart)
    //    {
    //        cartService = cart;
    //    }
    //    public static void Calculate(ICartService cart)
    //    {
    //        list = new List<CalculationBase>();
    //        if (list.Count() == 0)
    //        {
    //            list.Add(new DefaultCalculation(cart));

    //            list.Add(new UserSpecifiedNonCreditCashCalculation(cart));
    //            list.Add(new UserSpecified_NonCreditCashCalculation_WithOtherPaymentModesCalculation(cart));
    //            list.Add(new CreditModeCalculation(cart));
    //        }

    //        foreach (var x in list)
    //        {
    //            if (x.CanHandle)
    //            {
    //                x.Calculate();
    //            }
    //        }

    //    }
    //}

    //abstract class CalculationBase
    //{
    //    public CalculationBase(ICartService c)
    //    {
    //        this.Cart = c;
    //    }

    //    public ICartService Cart { get; set; }
    //    public abstract bool CanHandle { get; }

    //    public abstract void Calculate();

    //    protected bool IsOnlyCashMode
    //    {
    //        get
    //        {
    //            return Cart.PaymentModes.Count() == 1 && Cart.PaymentModes.First() is CashPaymentMode;
    //        }
    //    }

    //}
    //class DefaultCalculation : CalculationBase
    //{
    //    public DefaultCalculation(ICartService c) : base(c)
    //    {

    //    }
    //    public override bool CanHandle
    //    {
    //        get
    //        {
    //            return IsOnlyCashMode && Cart.CurrentInvoice.NetAmount == Cart.CurrentInvoice.DisplayCash;
    //        }
    //    }

    //    public override void Calculate()
    //    {
    //        Cart.CurrentInvoice.Change = 0;
    //        Cart.CurrentInvoice.PendingAmount = 0;

    //        Cart.CurrentInvoice.IsCredit = false;
    //    }

    //}

    //class UserSpecifiedNonCreditCashCalculation : CalculationBase
    //{
    //    public UserSpecifiedNonCreditCashCalculation(ICartService c) : base(c)
    //    {

    //    }
    //    public override bool CanHandle
    //    {
    //        get
    //        {
    //            return IsOnlyCashMode && Cart.CurrentInvoice.NetAmount < Cart.CurrentInvoice.DisplayCash;
    //        }
    //    }

    //    public override void Calculate()
    //    {
    //        Cart.CurrentInvoice.Change = Cart.CurrentInvoice.DisplayCash-Cart.CurrentInvoice.NetAmount;
    //        Cart.CurrentInvoice.PendingAmount = 0;
    //        Cart.CurrentInvoice.IsCredit = false;
    //    }

    //}

    //class UserSpecified_NonCreditCashCalculation_WithOtherPaymentModesCalculation : CalculationBase
    //{
    //    public UserSpecified_NonCreditCashCalculation_WithOtherPaymentModesCalculation(ICartService c) : base(c)
    //    {

    //    }
    //    public override bool CanHandle
    //    {
    //        get
    //        {
    //            return !IsOnlyCashMode && Cart.CurrentInvoice.NetAmount <= Cart.PaymentModes.Sum(p => p.Amount);
    //        }
    //    }

    //    public override void Calculate()
    //    {
    //        Cart.CurrentInvoice.Change = (long?)Cart.PaymentModes.Sum(p => p.Amount) - Cart.CurrentInvoice.NetAmount;
    //        Cart.CurrentInvoice.PendingAmount = 0;
    //        Cart.CurrentInvoice.IsCredit = false;
    //    }

    //}

    //class CreditModeCalculation : CalculationBase
    //{
    //    public CreditModeCalculation(ICartService c) : base(c)
    //    {

    //    }
    //    public override bool CanHandle
    //    {
    //        get
    //        {
    //            return Cart.CurrentInvoice.NetAmount > Cart.PaymentModes.Sum(p => p.Amount);
    //        }
    //    }

    //    public override void Calculate()
    //    {
    //        Cart.CurrentInvoice.Change = null;
    //        Cart.CurrentInvoice.PendingAmount = (long)(Cart.CurrentInvoice.NetAmount - Cart.PaymentModes.Sum(p => p.Amount));
    //        Cart.CurrentInvoice.IsCredit = true;
    //    }

    //}
}
