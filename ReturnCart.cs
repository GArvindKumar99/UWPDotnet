using SnapBilling.CartModule.ContentDialogs;
using SnapBilling.Core.UI;
using SnapBilling.Core.UI.ContentDialogs;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.Infrastructure;
using SnapBilling.Services.AppServices.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls;

namespace SnapBilling.CartModule.Services
{
    [Serializable]
    public class ReturnCart : IReturnCartService, INotificationVisualProvider,ISalesManHelper
    {
        private LineItemManager manager;
        public LineItemManager LineItemManager { get { return manager; } set { manager = value; } }
        public DiscountHelper discountHelper { get; set; }

        public InvoiceInfo CurrentInvoice { get; set; }
        public ObservableCollection<SnapProductInfo> LineItems { get; set; }
        public CustomerViewInfo TaggedCustomer { get; set; }
        public long TotalValue => (long)LineItems.Sum(x => x.TotalAmount);
        public int ItemCount => LineItems.Count;
        private IMessageService messageService;

        private CartDataService CartDataService;
        public double TotalDiscount { get; set; }
        public double TotalApplicableTax { get; set; }
        public SnapProductInfo CurrentItem { get; set; }

        public IDiscountApplicationStrategy discountApplicationStrategy;

        public ObservableCollection<IPaymentMode> PaymentModes { get; set; }

        public CashPaymentMode cash = new CashPaymentMode();
        private CurrencyManager currencyManager;

        public ReturnCart(IMessageService service)
        {
            messageService = service;
            Init();
        }

        private void Init()
        {
            PaymentModes = new ObservableCollection<IPaymentMode>();
            manager = new LineItemManager();
            CurrentInvoice = InvoiceFactory.CreateDefault();
            CurrentInvoice.isReturn = true;
            LineItems = new ObservableCollection<SnapProductInfo>();
            PaymentModes.Add(cash);
            discountHelper = new DiscountHelper();
            currencyManager = new CurrencyManager(this);
            SalesmanHelper = new SalesmanHelper();
        }

        public bool IsCustomerTagged
        {
            get
            {
                return CurrentInvoice.Customer != null;
            }
        }
        public CurrencyManager CurrencyManager => currencyManager;

        public long GetCustomerWalletDetails()
        {
            long wallet = 0;
            if (IsCustomerTagged)
            {
                wallet = CurrentInvoice.Customer.AmountDue < 0 ? Math.Abs(CurrentInvoice.Customer.AmountDue) : 0;
            }
            return wallet;
        }

        public void Calculate()
        {
            CurrentInvoice.TotalAmount = (long)LineItems.Sum(x => x.UserDefinedMrp * ((x.UomEdited == "G" || x.UomEdited == "ML") ? x.DisplayQuantity / 1000 : x.DisplayQuantity));
            CurrentInvoice.TotalItems = ItemCount;
            CurrentInvoice.NetAmount = TotalValue;
            CurrentInvoice.TotalSavings = (long)(LineItems?.Sum(x => x.Saving) ?? 0);
            CurrentInvoice.TotalQuantity = ResolveTotalQty();
            CurrentInvoice.TotalVatAmount = LineItems?.Sum(x => x.VatAmount) ?? 0;
          
            CurrentInvoice.TotalIgstAmount = (long?)(LineItems?.Sum(x => x.IgstAmount) ?? 0);
            CurrentInvoice.TotalCgstAmount = (long?)(LineItems?.Sum(x => x.CgstAmount) ?? 0);
            CurrentInvoice.TotalSgstAmount = (long?)(LineItems?.Sum(x => x.SgstAmount) ?? 0);
            CurrentInvoice.TotalCessAmount = (long?)(LineItems?.Sum(x => x.CessAmount) ?? 0);
            CurrentInvoice.TotalAdditionalCessAmount = (long?)(LineItems?.Sum(x => x.AdditionalCessAmount) ?? 0);


            CurrentInvoice.Change = TotalValue;
            CurrentInvoice.DisplayCash = (long)(cash.Amount = (long)-CurrentInvoice.Change); 
            CalculateRoundOff();
            
            //if (CurrentInvoice.ShouldUseCustomerWallet)
            //{
            //    var paymentmode = PaymentModes.SingleOrDefault(u => u is WalletPaymentMode);
            //    if (paymentmode != null)
            //    {
            //        paymentmode.Amount = DebitFromCustomerWallet();
            //    }
            //    else
            //    {
            //        PaymentModes.Add(new WalletPaymentMode() { Amount = DebitFromCustomerWallet() });
            //    }
            //}
            //var valuePaid = PaymentModes.Where(x => x.Name != "Cash").Sum(x => x.Amount);
            //if (TotalValue - valuePaid > 0)
            //{
            //    CurrentInvoice.DisplayCash = (long)(cash.Amount = (TotalValue - valuePaid));
            //}
            //else
            //{
            //    CurrentInvoice.DisplayCash = (long)(cash.Amount = 0);
            //}

            //CalculationEngine.Calculate(this);
            //ApplyDiscount(CurrentInvoice.TotalDiscount);
        }

        private long ResolveTotalQty()
        {
            var gLItems = LineItems?.Where(x => x.Uom == "G" || x.Uom == "ML");
            var pcItems = LineItems?.Except(gLItems);
            return (long)((pcItems?.Sum(x => x.DisplayQuantity) ?? 0) + (gLItems?.Count()));
        }

        //private long DebitFromCustomerWallet()
        //{
        //    long debidedAmount = 0;
        //    long amountToPaid = (long)(CurrentInvoice.NetAmount - PaymentModes.Where(x => x.Name != "Cash" && x.Name != "Wallet").Sum(x => x.Amount));
        //    if (amountToPaid > GetCustomerWalletDetails())
        //    {
        //        debidedAmount = GetCustomerWalletDetails();
        //    }
        //    else
        //    {
        //        debidedAmount = amountToPaid < 0 ? 0 : amountToPaid;
        //    }
        //    return debidedAmount;
        //}

        //public void CashChanged(long displayCash)
        //{
        //    cash.Amount = displayCash;
        //    CurrentInvoice.DisplayCash = displayCash;
        //    // CalculationEngine.Calculate(this);
        //}

        private void CalculateRoundOff()
        {
            if (ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync().Result.RoundOffCashAndTotalField)
            {
                var netAmount = (double)CurrentInvoice.NetAmount / 100;
                var roundedNetAmount = Math.Round(netAmount, MidpointRounding.AwayFromZero);
                CurrentInvoice.NetAmount = (long)(roundedNetAmount * 100);
                CurrentInvoice.RoundOffAmount = Math.Round(roundedNetAmount - netAmount, 2, MidpointRounding.AwayFromZero);
                CurrentInvoice.Change = CurrentInvoice.NetAmount;
            }
            else
            {
                CurrentInvoice.RoundOffAmount = 0;
            }
        }
        public async Task AddToCart(ObservableCollection<SnapProductInfo> productpacks)
        {

            double quantity = 1;
            SnapProductInfo result = null;
            if (productpacks.Count() == 1)
            {
                result = productpacks.ElementAt(0);


            }
            else
            {
                var c = await ShowMultipleMrpDialog(productpacks);
                if (c != null)
                {
                    result = c;
                }
                else
                {
                    return;
                }
            }
            CurrentItem = result;

            if (result.IsTransient)
            {
                var transient_qty = manager.HowManyInCartFor(result.BatchId);
                manager.Add(result.BatchId, ++transient_qty);
                LineItems.Add(result);
                result.DisplayQuantity = manager.HowManyInCartFor(result.BatchId);
                //ApplyDiscount(CurrentInvoice.TotalDiscount);
                return;
            }
            if (result.SalePrice == null || result.UserDefinedMrp == 0)
            {
                result.SalePrice = result.SalePrice1.ConvertSafeToInt64();
                result.UserDefinedMrp = result.Mrp;
            }

            result.DiscountOverride = System.Math.Round(((double)result.SalePrice / 100) * result.UserDefinedDiscount,3, MidpointRounding.AwayFromZero);
            //if (result.Uom == "G" || result.Uom == "ML")
            //{
            //    quantity = 1000;
            //}
            result.UomEdited = ResolveUom.Instance.GetMaxMeasure(result.Uom);
            if (ServiceLocator.Current.GetService<IWeighingMachineService>().IsMachineConnected)
            {
                result.Locked = result.UomEdited != "KG";
            }
            else
            {
                result.Locked = true;
            }
            result.DisplayQuantity = 1;
            if (!BarcodeAwareContentControlContext.Instance.IsActive)
            {
                QuantityUomPreSelectionDialog dialog = new QuantityUomPreSelectionDialog(result);
                await dialog.ShowAsync();
                if (result.QuantityView == 0 || dialog.Result != ContentDialogResult.Primary)
                {
                    if (result.QuantityView == 0)
                    {
                        await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Product not added due to invalid quantity.", AlertType.Error);
                    }
                    if (!BarcodeAwareContentControlContext.Instance.IsActive)
                    {
                        messageService.Send(this, "FocusOnProductSearchBar", null);
                    }
                    return;
                }
            }
            if (result.QuantityView == 0)
            {
                result.DisplayQuantity = 1;
            }
            var howmany = manager.HowManyInCartFor(result.BatchId);

            if (howmany == 0)
            {
                // var stockAvailable = await InventoryStockTracker.Current.GetRealTimeStockAvailabilityFor(result.BatchId, result.ProductCode.ToString());
                //if (!await InventoryStockTracker.Current.IsEnabled())
                //{
                //    if (discountHelper.Discount > 0)
                //    {
                //        result.Discount = discountHelper.Discount;
                //    }
                //    result.DisplayQuantity = quantity;

                //    if (quantity == 1)
                //    {
                //        manager.Add(result.BatchId);
                //    }
                //    else
                //    {
                //        manager.Add(result.BatchId, quantity);
                //    }
                //    LineItems.Add(result);

                //if (stockAvailable - quantity >= 0)

                //if (discountHelper.Discount > 0)
                //{
                //    result.Discount = discountHelper.Discount;
                //}
                //result.DisplayQuantity = quantity;


                //if (quantity == 1)
                //{
                //    manager.Add(result.BatchId);
                //}
                //else

                manager.Add(result.BatchId, quantity);

                LineItems.Add(result);
                //else
                //{
                //    await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Not Enough Stock", AlertType.Warning);
                //}
            }
            else
            {
                var exitingLineItem = LineItems.Where(x => x.BatchId == result.BatchId).First();

                //if (await InventoryStockTracker.Current.IsEnabled())
                //{
                //    var stockAvailable = await InventoryStockTracker.Current.GetRealTimeStockAvailabilityFor(result.BatchId, result.ProductCode.ToString());

                //    if (stockAvailable - quantity <= 0)
                //    {
                //        await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Not Enough Stock", AlertType.Warning);
                //        return;
                //    }
                //}
                exitingLineItem.DisplayQuantity += quantity;

                if (result.UomEdited == "KG" || result.UomEdited == "L")
                {
                    if ((exitingLineItem.UomEdited == "ML" || exitingLineItem.UomEdited == "G") & exitingLineItem.DisplayQuantity >= 1000)
                    {
                        exitingLineItem.DisplayQuantity = exitingLineItem.DisplayQuantity / 1000;
                    }
                }
                exitingLineItem.UomEdited = result.UomEdited;

                manager.Add(result.BatchId, quantity);
            }
            ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", result);

            Calculate();
            //ApplyDiscount(CurrentInvoice.TotalDiscount);
            if (!BarcodeAwareContentControlContext.Instance.IsActive)
            {
                messageService.Send(this, "FocusOnProductSearchBar", null);
            }
        }

        public void UpdateItemInCart(SnapProductInfo product)
        {
            if (product != null)
            {
                CurrentItem = product;

                var howmany = manager.HowManyInCartFor(product.BatchId);
                if (product.IsTransient)
                {
                    var transient_qty = manager.HowManyInCartFor(product.BatchId);
                    manager.Add(product.BatchId, ++transient_qty);
                    var existingItem = LineItems.Where(x => x.BatchId == product.BatchId).FirstOrDefault();
                    if (existingItem != null)
                    {
                        existingItem.DisplayQuantity = manager.HowManyInCartFor(product.BatchId);
                    }
                    else
                    {
                        LineItems.Add(product);
                    }
                    product.DisplayQuantity = manager.HowManyInCartFor(product.BatchId);
                    Calculate();
                    return;
                }
                if (howmany == 0)
                {
                    if (product.IsLooseItem && product.IsQuickAdd != 1)
                    {
                        var ticks = DateTime.Now.Ticks;
                        product.Barcode = ticks;
                        product.ProductCode = ticks;
                        product.BatchId = ticks.ToString();
                        //var c = LineItems.Where(x => x.BatchId == product.BatchId).FirstOrDefault();
                        //if (c != null)
                        //{
                        //    c = product;
                        //}
                        //else
                        {
                            LineItems.Add(product);
                        }
                        manager.Add(product.BatchId, product.DisplayQuantity);


                    }
                    else if (product.IsLooseItem && product.IsQuickAdd == 1)
                    {
                        if ((product.DisplayQuantity >= 1000 || product.DisplayQuantity <= -1000) && (product.Uom == "G" || product.Uom == "ML"))
                        {
                            product.UomEdited = ResolveUom.Instance.GetMaxMeasure(product.Uom);
                        }
                        LineItems.Add(product);
                        manager.Add(product.BatchId, product.DisplayQuantity);
                    }
                    else
                    {
                        product.DisplayQuantity = 1;
                        LineItems.Add(product);
                        manager.Add(product.BatchId);
                    }
                    product.DiscountOverride = System.Math.Round(((double)product.SalePrice / 100) * product.UserDefinedDiscount, 3, MidpointRounding.AwayFromZero);

                }
                else
                {
                    //check to see if we have enough in inventory

                    var exitingLineItem = LineItems.Where(x => x.BatchId == product.BatchId).FirstOrDefault();
                    exitingLineItem.DisplayQuantity = product.DisplayQuantity;
                    if ((exitingLineItem.DisplayQuantity >= 1000 || product.DisplayQuantity <= -1000) && (product.Uom == "G" || product.Uom == "ML"))
                    {
                        exitingLineItem.UomEdited = ResolveUom.Instance.GetMaxMeasure(product.Uom);
                    }
                    if (howmany > product.ActualDisplayQuantity)
                    {
                        manager.Remove(exitingLineItem.BatchId, howmany - product.ActualDisplayQuantity);
                    }
                    else
                    {
                        manager.Add(exitingLineItem.BatchId, product.ActualDisplayQuantity - howmany);
                    }
                    exitingLineItem.DiscountOverride = ((double)product.SalePrice / 100) * exitingLineItem.UserDefinedDiscount;

                }
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", product);

            }
            Calculate();
        }
   

        public void DeleteFromCart(SnapProductInfo item)
        {
            if (item != null)
            {
                CurrentItem = null;
                var exitingLineItem = LineItems.Where(x => x.BatchId == item.BatchId).First();
                exitingLineItem.DisplayQuantity = 0;
                LineItems.Remove(exitingLineItem);
                //if (/*!exitingLineItem.IsLooseItem||*/exitingLineItem.IsQuickAdd==1)
                {
                    manager.Delete(exitingLineItem.BatchId);
                }
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", item);
                Calculate();
            }
            
        }

        protected async Task<SnapProductInfo> ShowMultipleMrpDialog(ObservableCollection<SnapProductInfo> info)
        {
            if (info.Count > 1)
            {
                ProductBatchSelectDialog dialog = new ProductBatchSelectDialog(info);
                var result = await dialog.ShowAsync();
                if (dialog.Result == "Primary")
                {
                    var text = dialog.SelectedItem;
                    return text;
                }
                return null;
            }
            else
            {
                return info.First();
            }
        }

        public Task<IEnumerable<CustomerInfo>> SearchCustomer(string query)
        {
            throw new NotImplementedException();
        }

        public Task<ObservableCollection<SnapProductInfo>> SearchProduct(long productCode)
        {
            throw new NotImplementedException();
        }

        public Task<List<ProductPackCompact>> SearchProductCompact(string query)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<SnapProductInfo> GetTopProductsForCustomer()
        {
            throw new NotImplementedException();
        }

        //public void ApplyDiscount(double discount)
        //{
        //    if (discount <= CurrentInvoice.NetAmount)
        //    {
        //        var discountApplicationStrategy = DiscountStrategyResolver.ResolveDiscountStrategy();
        //        new SplitDiscountApplicationStateguy().ApplyDiscount(this, DiscountType.Amount, discount);
        //        CurrentInvoice.TotalDiscount = (long)discount;
        //        Calculate();
        //    }
        //    else
        //    {
        //        ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Discount cannot be more then net amount", AlertType.Error);
        //        new SplitDiscountApplicationStateguy().ApplyDiscount(this, DiscountType.Amount, 0);

        //        CurrentInvoice.TotalDiscount = 0;
        //        Calculate();
        //    }
        //    // discountHelper.Discount = discount;
        //    //Calculate();
        //}

        public async Task<bool> SaveInvoice()
        {
            CurrentInvoice.BillStartedAt = CurrentInvoice.createdAt = DateTime.UtcNow.Ticks;
            UpdateInvoice(CurrentInvoice);
            CurrentInvoice.Items = LineItems;
            CartDataService = new CartDataService();
            CurrentInvoice.Payments = PaymentModes;
            //CurrentInvoice.Customer = TaggedCustomer;
            var result = await CartDataService.SaveInvoice(CurrentInvoice);
            if (result)
            {
               // ServiceLocator.Current.GetService<IMessageService>().Send(this, "SaveSalesmanInfo", CurrentInvoice);
                CurrentItem = null;
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "InvoiceSaved", CurrentInvoice);

                RefreshItems(CurrentInvoice);

            }
            return result;
        }
        private void RefreshItems(InvoiceInfo currentInvoice)
        {
            foreach (var i in currentInvoice?.Items)
            {
                if (i.IsTransient && i.IsLooseItem && i.IsQuickAdd != 1)
                {
                    continue;
                }
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", i);

            }
        }

        private void UpdateInvoice(InvoiceInfo info)
        {
            if (info.Customer != null)
            {
                info.CustomerPhone = info.Customer?.Phone;
                info.ShipToPhone = info.Customer?.Phone;
                info.BillToPhone = info.Customer?.Phone;
                info.BillToAddress = info.Customer?.Address;
            }
            info.IsDeleted = false;
            info.IsSync = 1;
            var isClient = ServiceLocator.Current.GetService<ISettingService>().IsMultiPosEnabled().Result.Value;
            if (isClient)
            {
                info.ToReconcile = 1;
            }
            info.IsUpdated = 0;
        }

        public void ClearCart()
        {
            Reset();
            Init();
        }

        private void Reset()
        {
            //clear PaymentModes
            PaymentModes.Clear();
            cash.Amount = 0;
            PaymentModes = null;
            CurrentItem = null;

            //clear invoice
            var ci = CurrentInvoice;

            //reset payments
            ci.Customer = null;
            ci.Clear();
            ci = InvoiceFactory.CreateDefault();
            //ci.Payments = null;

            this.LineItems.Clear();
            this.LineItemManager.Reset();

            messageService.Send(this, "CustomerReset", null);
            messageService.Send(this, "LineItemStatus", null);
            //reset all properties
            ResetProperties(ci);
            ResetProperties(this);
        }

        private void ResetProperties(object ci)
        {
            var props = ci.GetType().GetProperties();

            foreach (var prop in props)
            {
                prop.PropertyType.ToDefault();
            }
        }

        public void AddPaymentMode(IPaymentMode paymentMode)
        {
            if (paymentMode != null)
            {
                if (paymentMode is DigitalPaymentMode)
                {
                    PaymentModes.Add(paymentMode as DigitalPaymentMode);
                }
                else if (paymentMode is CardPaymentMode)
                {
                    PaymentModes.Add(paymentMode as CardPaymentMode);
                }
                else if (paymentMode is ChequePaymentMode)
                {
                    PaymentModes.Add(paymentMode as ChequePaymentMode);
                }
                Calculate();
            }
        }

        private void PaymentModeChanged()
        {
            var valuePaid = PaymentModes.Where(x => x.Name != "Cash").Sum(x => x.Amount);
            if (TotalValue - valuePaid > 0)
            {
                CurrentInvoice.DisplayCash = (long)(cash.Amount = (TotalValue - valuePaid));
            }
            else
            {
                CurrentInvoice.DisplayCash = (long)(cash.Amount = 0);
            }
            //CalculationEngine.Calculate(this);
        }

        public void RemovePaymentMode(IPaymentMode paymentMode)
        {
            if (paymentMode != null)
            {
                PaymentModes.Remove(paymentMode);
                Calculate();
            }
        }
        public void Notify(object sender, object parameter)
        {
            if (CurrencyManager.SelectedItem != null && ((CurrencyManager.SelectedItem.UomEdited != "PC") && !CurrencyManager.SelectedItem.Locked))
            {
                Task.Run(() =>
                {
                    ServiceLocator.Current.GetService<IMessageService>().Send(this, "WeighingScaleNotificationRecd", parameter);
                });
            }
        }
        public INotificationVisual Visual { get; private set; }
        public SalesmanHelper SalesmanHelper { get; set; }

        public void SetVisual(INotificationVisual visual)
        {
            Visual = visual;
            ServiceLocator.Get<IMessageService>().Send(this, "NotificationChanged", visual);
        }


        public INotificationVisual GetCurrentVisual()
        {
            return Visual;
        }
    }
}
