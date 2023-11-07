using SnapBilling.CartModule.ContentDialogs;
using SnapBilling.Core.UI.ContentDialogs;
using SnapBilling.Data;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.Services;
using SnapBilling.Services.AppServices.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using static SnapBilling.Services.AppServices.PaymentModeCalculation;
using static SnapBilling.Services.AppServices.CoreCalculationComponent;
using System.ComponentModel;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using Windows.UI.Xaml.Controls;
using SnapBilling.Core.UI;

namespace SnapBilling.CartModule.Services
{
    public static class DiscountStrategyResolver
    {
        public static IDiscountApplicationStrategy ResolveDiscountStrategy()
        {
            //read settings to check
            return null;
        }
    }

    public class IVisualNotification
    {
        public void ShowNotification()
        {

        }
    }
    
    [Serializable]
    public class Cart : ICartService, INotificationVisualProvider,INotifyPropertyChanged,ISalesManHelper
    {
        public SalesmanHelper SalesmanHelper { get; set; }
        protected LineItemManager manager;
        public LineItemManager LineItemManager { get { return manager; } set { manager = value; } }
        public DiscountHelper discountHelper { get; set; }
        public SnapProductInfo CurrentItem { get; set; }
        public InvoiceInfo CurrentInvoice { get; set; }
        public ObservableCollection<SnapProductInfo> LineItems { get; set; }
        public CustomerViewInfo TaggedCustomer { get; set; }
        public long TotalValue => (long)LineItems.Sum(x => x.TotalAmount);
        public long AdditionalDiscountValue { get; set; }
        public int ItemCount => LineItems.Count;
        private IMessageService messageService;
        public bool IsClient { get; set; }

        protected CartDataService CartDataService;
        public double TotalDiscount { get; set; }
        public double TotalApplicableTax { get; set; }
        public IDiscountApplicationStrategy discountApplicationStrategy;

        public ObservableCollection<IPaymentMode> PaymentModes { get; set; }

        public INotificationVisual Visual { get; private set; }
        public void SetVisual(INotificationVisual visual)
        {
            Visual = visual;
            ServiceLocator.Get<IMessageService>().Send(this, "NotificationChanged", visual);
        }

        public int Token { get; set; }

        public INotificationVisual GetCurrentVisual()
        {
            return Visual;
        }

        public CashPaymentMode CashPayment { get; set; }
        public Cart(IMessageService service)
        {
            messageService = service;
            Init();
            IsScanningEnabled = true;
            
            //SetCalculator(CalculationEngineFactory.Get(this));
        }

        protected async virtual void Init()
        {
            PaymentModes = new ObservableCollection<IPaymentMode>();
            manager = new LineItemManager();
            CurrentInvoice = InvoiceFactory.CreateDefault();
            CurrentInvoice.AddTrigger(new InvoiceMoreThan90PercentTrigger());
            LineItems = new ObservableCollection<SnapProductInfo>();
            CashPayment = new CashPaymentMode();
            PaymentModes.Add(CashPayment); 
            discountHelper = new DiscountHelper();
            IsClient = (bool)await ServiceLocator.Current.GetService<ISettingService>().IsMultiPosEnabled();
            eventListener = new BillingContext.EventListener();
            eventListener.AddListener(new MockWeighingScaleListener());
            currencyManager = new CurrencyManager(this);
            SalesmanHelper = new SalesmanHelper();            
            CurrentInvoice.Token = Token;
        }
        public bool IsCustomerTagged
        {
            get
            {
                return CurrentInvoice.Customer != null;
            }
        }

        public bool IsScanningEnabled { get; set; }
        BillingContext.EventListener eventListener = new BillingContext.EventListener();
        private CurrencyManager currencyManager;

        public event PropertyChangedEventHandler PropertyChanged;

        public BillingContext.EventListener EventListener => eventListener;

        public CurrencyManager CurrencyManager { get => currencyManager; }
        public CartSpType SelectedSpType { get; set; } = CartSpType.SP1;

        public long GetCustomerWalletDetails()
        {
            long wallet = 0;
            if (IsCustomerTagged)
            {
                wallet = CurrentInvoice.Customer.AmountDue < 0 ? Math.Abs(CurrentInvoice.Customer.AmountDue) : 0;
            }
            return wallet;
        }
        private long ResolveTotalQty()
        {
            var gLItems = LineItems?.Where(x => x.Uom == "G" || x.Uom == "ML");
            var pcItems = LineItems?.Except(gLItems);
            return (long)((pcItems?.Sum(x => x.DisplayQuantity) ?? 0) + (gLItems?.Count()));
        }
        public virtual void Calculate()
        {

            //if (this.CurrentInvoice.TotalAmount > 0)
            //{
            //    this.CurrentInvoice.FireTrigger();
            //}
            var cc = new CoreCalculationComponent(this);
            cc.Calculate();
            InvoicePromotionComponent.Instance.Calculate();
            cc.Calculate();
            ProductQuantityTieredPromotionComponent.Instance.Calculate();
            cc.Calculate();
            this.CurrentInvoice.FireTrigger();
           /* CurrentInvoice.TotalAmount = (long)LineItems.Sum(x => x.UserDefinedMrp * x.DisplayQuantity);
            CurrentInvoice.TotalItems = ItemCount;
            CurrentInvoice.NetAmount = TotalValue;
            CurrentInvoice.RunBehaviors();
            CurrentInvoice.TotalSavings = (long)(LineItems?.Sum(x => x.Saving) ?? 0);
            CurrentInvoice.TotalQuantity = ResolveTotalQty();
            if (Convert.ToBoolean(CurrentInvoice.Customer != null)&& Convert.ToBoolean(CurrentInvoice.Customer.IsTaxExclusive))
            {
                CurrentInvoice.NetAmount += (long)(CurrentInvoice.TotalCgstAmount + CurrentInvoice.TotalSgstAmount + CurrentInvoice.TotalCessAmount + CurrentInvoice.TotalAdditionalCessAmount);
            }
            CurrentInvoice.NetAmount = CurrentInvoice.NetAmount - CurrentInvoice.TotalDiscount;
            CalculateRoundOff();
            CalculateVat();
            CalculateGst();
            ProcessPayment();
            CalculationEngine.Calculate(this);
            //ApplyDiscount(CurrentInvoice.TotalDiscount);
            */
        }
        private void CalculateRoundOff()
        {
            if (ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync().Result.RoundOffCashAndTotalField)
            {
                var netAmount = (double)CurrentInvoice.NetAmount / 100;
                var roundedNetAmount = Math.Round(netAmount,MidpointRounding.AwayFromZero);
                CurrentInvoice.NetAmount = (long)(roundedNetAmount * 100);
                CurrentInvoice.RoundOffAmount = Math.Round(roundedNetAmount - netAmount, 2,MidpointRounding.AwayFromZero);
            }
            else
            {
                CurrentInvoice.RoundOffAmount = 0;
            }
        }

        protected virtual void ProcessPayment()
        {
            if (CurrentInvoice.ShouldUseCustomerWallet)
            {
                var paymentmode = PaymentModes.SingleOrDefault(u => u is WalletPaymentMode);
                if (paymentmode != null)
                {
                    paymentmode.Amount = DebitFromCustomerWallet();
                }
                else
                {
                    PaymentModes.Add(new WalletPaymentMode() { Amount = DebitFromCustomerWallet() });
                }
            }

            var valuePaid = PaymentModes.Where(x => x.Name != "Cash").Sum(x => x.Amount);
            if (CurrentInvoice.NetAmount - valuePaid > 0)
            {
                if (!CurrentInvoice.IsCredit)               
                {
                    CurrentInvoice.DisplayCash = (long)(CashPayment.Amount = (CurrentInvoice.NetAmount - valuePaid));
                }
            }
            else
            {
                CurrentInvoice.DisplayCash = (long)(CashPayment.Amount = 0);
            }
        }
        protected virtual void CalculateVat()
        {
            CurrentInvoice.TotalVatAmount = LineItems?.Sum(x => x.VatAmount) ?? 0;
        }
        protected virtual void CalculateGst()
        {
            CurrentInvoice.TotalIgstAmount = (LineItems?.Sum(x => (long?)x.IgstAmount) ?? 0);
            CurrentInvoice.TotalCgstAmount = (LineItems?.Sum(x => (long?)x.CgstAmount) ?? 0);
            CurrentInvoice.TotalSgstAmount = (LineItems?.Sum(x => (long?)x.SgstAmount) ?? 0);
            CurrentInvoice.TotalCessAmount = (LineItems?.Sum(x => (long?)x.CessAmount) ?? 0);
            CurrentInvoice.TotalAdditionalCessAmount = (LineItems?.Sum(x => (long?)x.AdditionalCessAmount) ?? 0);
        }

        private long DebitFromCustomerWallet()
        {
            long debidedAmount = 0;
            long amountToPaid = (long)(CurrentInvoice.NetAmount - PaymentModes.Where(x => x.Name != "Cash" && x.Name != "Wallet").Sum(x => x.Amount));
            if (amountToPaid > GetCustomerWalletDetails())
            {
                debidedAmount = GetCustomerWalletDetails();
            }
            else
            {
                debidedAmount = amountToPaid < 0 ? 0 : amountToPaid;
            }
            return debidedAmount;
        }
        public virtual async Task<List<ProductSuggestion>> GetMostPurchasedProducts(MostPurchasedProductsCriteria criteria, int n = 0)
        {
            return (await new CartDataService().GetMostPurchasedProduct(criteria))?.OrderByDescending(x => x.Quantity).Take(n).ToList();
        }
        public void CashChanged(long displayCash)
        {
            CashPayment.Amount = displayCash;
            CurrentInvoice.DisplayCash = displayCash;
            new CashCalculationComponent(this).Calculate();
        }
        public virtual async Task<SnapProductInfo> CheckForBatchInfo(ObservableCollection<SnapProductInfo> productpacks)
        {
            if (productpacks.Count() == 1)
            {
                return productpacks.ElementAt(0);
            }
            else
            {
                return await ShowMultipleMrpDialog(productpacks);
            }
        }
        
        public virtual async Task AddToCart(ObservableCollection<SnapProductInfo> productpacks)
        {

            //double quantity = 1;
            SnapProductInfo result = await CheckForBatchInfo(productpacks);

            if (result == null) { return; }
            result.Attributes = await ServiceLocator.Current.GetService<IProductCatalogueService>().GetAttributeMapForProduct(result.Barcode);
            result.Attributes = await ServiceLocator.Current.GetService<IAttributeServices>().GetAllAttributesForProductAsync(result);
            CurrentItem = result;
            // result.AddMetaData("IMEI", "TestIMEI");
            if (result.IsTransient)
            {
                var transient_qty = manager.HowManyInCartFor(result.BatchId);
                manager.Add(result.BatchId);//, ++transient_qty);
                var p = LineItems.FirstOrDefault(x => x.BarcodeView == result.BarcodeView);
                if (p == null)
                {

                    LineItems.Add(result);
                }
                else
                {
                    p.DisplayQuantity += manager.HowManyInCartFor(result.BatchId);
                }

                result.DisplayQuantity = manager.HowManyInCartFor(result.BatchId);
                ApplyDiscount(CurrentInvoice.TotalDiscount);
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", result);

                return;
            }
            if (result.SalePrice == null || result.UserDefinedMrp == 0)
            {
                SetSelectedSp(result);
                result.UserDefinedMrp = result.Mrp;
            }
            if (result.UserDefinedDiscount != 0)
            {
                //result.DiscountOverride = result.UserDefinedDiscount;
                ResolvePreDefinedDiscount(result);
            }
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
                if (result.QuantityView==0|| dialog.Result != ContentDialogResult.Primary)
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
                var stockAvailable = await InventoryStockTracker.Current.GetRealTimeStockAvailabilityFor(result.BatchId, result.ProductCode.ToString());
                if (!await InventoryStockTracker.Current.IsEnabled())
                {
                    if (discountHelper.Discount > 0)
                    {
                        result.Discount = discountHelper.Discount;
                    }
                    //result.DisplayQuantity = quantity;

                    if (result.DisplayQuantity == 1)
                    {
                        manager.Add(result.BatchId);
                    }
                    else
                    {
                        manager.Add(result.BatchId, result.DisplayQuantity);
                    }
                    LineItems.Add(result);
                    var quickQty = (await ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync()).GeneralQuickQtyBilling;
                    if (quickQty)
                    {
                        messageService.Send(this, "OpenDialogBox", result);
                    }
                }
                else
                {
                    if (stockAvailable - result.DisplayQuantity >= 0)
                    {
                        if (discountHelper.Discount > 0)
                        {
                            result.Discount = discountHelper.Discount;
                        }
                        //result.DisplayQuantity = quantity;

                        if (result.DisplayQuantity == 1)
                        {
                            manager.Add(result.BatchId);
                        }
                        else
                        {
                            manager.Add(result.BatchId, result.DisplayQuantity);
                        }
                        LineItems.Add(result);
                        var quickQty = (await ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync()).GeneralQuickQtyBilling;
                        if (quickQty)
                        {
                            messageService.Send(this, "OpenDialogBox", result);
                        }
                    }
                    else
                    {
                        await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Not Enough Stock", AlertType.Warning);
                    }
                }
            }
            else
            {
                var exitingLineItem = LineItems.Where(x => x.BatchId == result.BatchId).First();

                if (await InventoryStockTracker.Current.IsEnabled())
                {
                    var stockAvailable = await InventoryStockTracker.Current.GetRealTimeStockAvailabilityFor(result.BatchId, result.ProductCode.ToString());

                    if (stockAvailable - result.DisplayQuantity <= 0)
                    {
                        await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Not Enough Stock", AlertType.Warning);
                        return;
                    }
                }
                var resolvedPair = QuantityUomResolverFactory.UnitResolution(exitingLineItem.DisplayQuantity, exitingLineItem.UomEdited, result.QuantityView, result.UomEdited);
                exitingLineItem.DisplayQuantity = resolvedPair.Quantity;
                exitingLineItem.UomEdited = resolvedPair.Uom;
                //exitingLineItem.DisplayQuantity += quantity;
                if (result.UomEdited == "KG" || result.UomEdited == "L")
                {
                    if ((exitingLineItem.UomEdited == "ML" || exitingLineItem.UomEdited == "G") & exitingLineItem.DisplayQuantity >= 1000)
                    {
                        exitingLineItem.DisplayQuantity = exitingLineItem.DisplayQuantity / 1000;
                    }
                }
                //exitingLineItem.UomEdited = result.UomEdited;

                manager.Add(result.BatchId, result.DisplayQuantity);
                var quickQty = (await ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync()).GeneralQuickQtyBilling;
                if (quickQty)
                {
                    messageService.Send(this, "OpenDialogBox", exitingLineItem);
                }
            }
            ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", result);
            ApplyDiscount(CurrentInvoice.TotalDiscount);
            if (!BarcodeAwareContentControlContext.Instance.IsActive)
            {
                messageService.Send(this, "FocusOnProductSearchBar", null);
            }
        }



        protected async Task ResolvePreDefinedDiscount(SnapProductInfo product)
        {
            if (product.UserDefinedDiscount != 0)
            {
                product.DiscountOverride = System.Math.Round(((double)product.SalePrice / 100) * product.UserDefinedDiscount, MidpointRounding.AwayFromZero);
            }
            else if (product.UserDefinedDiscount == 0 || product.SelectedSalePrice == SelectedSalePrice.Custom)
            {
                product.DiscountOverride = 0;
            }
        }
        public virtual async void UpdateItemInCart(SnapProductInfo product)
        {
            if (product != null)
            {
                CurrentItem = product;
                var howmany = manager.HowManyInCartFor(product.BatchId);
                if (product.IsTransient)
                {
                    var transient_qty = manager.HowManyInCartFor(product.BatchId);

                    var existingItem = LineItems.Where(x => x.BatchId == product.BatchId).FirstOrDefault();
                    existingItem.Mrp = existingItem.UserDefinedMrp;
                    existingItem.SalePrice1 = existingItem.SalePrice2 = existingItem.SalePrice3 = Convert.ToInt64(existingItem.UserDefinedSalePrice);
                    if (existingItem != null)
                    {
                        if (transient_qty > product.DisplayQuantity)
                        {
                            manager.Remove(existingItem.BatchId, transient_qty - product.DisplayQuantity);
                        }
                        else
                        {
                            manager.Add(existingItem.BatchId, product.DisplayQuantity - transient_qty);
                        }
                        existingItem.DisplayQuantity = manager.HowManyInCartFor(product.BatchId);

                    }
                    else
                    {
                        LineItems.Add(product);
                    }
                    product.DisplayQuantity = manager.HowManyInCartFor(product.BatchId);
                    ApplyDiscount(CurrentInvoice.TotalDiscount);
                    ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", product);
                    ResolvePreDefinedDiscount(product);
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
                        manager.Add(product.BatchId, product.ActualDisplayQuantity);
                    }
                    else
                    {
                        product.DisplayQuantity = 1;
                        LineItems.Add(product);
                        manager.Add(product.BatchId);
                    }
                    ResolvePreDefinedDiscount(product);
                }
                else
                {
                    //check to see if we have enough in inventory

                    var exitingLineItem = LineItems.Where(x => x.BatchId == product.BatchId).FirstOrDefault();
                    if (await InventoryStockTracker.Current.IsEnabled())
                    {
                        var stockAvailable = await InventoryStockTracker.Current.GetRealTimeStockAvailabilityFor(product.BatchId, product.ProductCode.ToString());

                        if (stockAvailable - (product.DisplayQuantity - howmany) < 0)
                        {
                            await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Not Enough Stock", AlertType.Warning);
                            exitingLineItem.DisplayQuantity = howmany;
                            return;
                        }
                    }
                    exitingLineItem.DisplayQuantity = product.DisplayQuantity;
                    exitingLineItem.UOMEditedByQA = product.UOMEditedByQA;
                    exitingLineItem.UomEdited = product.UomEdited;
                    ResolvePreDefinedDiscount(exitingLineItem);
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
                }
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", product);
            }
            ApplyDiscount(CurrentInvoice.TotalDiscount);
        }

        public void DeleteFromCart(SnapProductInfo item)
        {
            try
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
                    ApplyDiscount(CurrentInvoice.TotalDiscount);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(this, ex);
            }
        }
        public void UpdateSelectedSp(CartSpType cartSpType=CartSpType.SP1)
        {
            try
            {
                SelectedSpType = cartSpType;
                foreach (var item in LineItems)
                {
                    SetSelectedSp(item);
                }
                ApplyDiscount(CurrentInvoice.TotalDiscount);
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(Cart),ex);
            }
        }

        public void SetSelectedSp(SnapProductInfo item)
        {
            switch (SelectedSpType)
            {
                case CartSpType.SP1:
                    item.SalePrice = Convert.ToInt64(item.SalePrice1);
                    item.SelectedSalePrice = SelectedSalePrice.SP1;
                    break;
                case CartSpType.SP2:
                    item.SalePrice = Convert.ToInt64(item.SalePrice2);
                    item.SelectedSalePrice = SelectedSalePrice.SP2;
                    break;
                case CartSpType.SP3:
                    item.SalePrice = Convert.ToInt64(item.SalePrice3);
                    item.SelectedSalePrice = SelectedSalePrice.SP3;
                    break;
                default:
                    item.SalePrice = Convert.ToInt64(item.Mrp);
                    item.SelectedSalePrice = SelectedSalePrice.Custom;
                    break;
            } 
        }

        protected virtual async Task<SnapProductInfo> ShowMultipleMrpDialog(ObservableCollection<SnapProductInfo> info)
        {
            if (info.Count > 1)
            {
                ProductBatchSelectDialog dialog = new ProductBatchSelectDialog(new ObservableCollection<SnapProductInfo>(info.OrderByDescending(x=>x.IsDefault).ToList()));
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

        public virtual void ApplyDiscount(double discount)
        {
            if (discount <= CurrentInvoice.NetAmount)
            {
                //if (!VersionMode.IsEnterprise())
                //{
                //    var discountApplicationStrategy = DiscountStrategyResolver.ResolveDiscountStrategy();
                //    new SplitDiscountApplicationStateguy().ApplyDiscount(this, DiscountType.Amount, discount);
                //}
                if (discount == 0&&!CurrentInvoice.IsCustomDiscount)
                {
                    CurrentInvoice.IsCustomDiscount = false;
                }

                CurrentInvoice.TotalDiscount = (long)discount;
                Calculate();
            }
            else
            {
                if (ItemCount != 0)
                {
                    ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Discount cannot be more then net amount", AlertType.Error);
                    //if (!VersionMode.IsEnterprise())
                    //{
                    //    new SplitDiscountApplicationStateguy().ApplyDiscount(this, DiscountType.Amount, 0);
                    //}
                    CurrentInvoice.TotalDiscount = 0;
                    Calculate();
                }
                else
                {
                    CurrentInvoice.AdditionalDiscountValue = 0;
                    CurrentInvoice.TotalDiscount = 0;
                    Calculate();
                }
            }
            // discountHelper.Discount = discount;
            //Calculate();
        }

        public virtual async Task<bool> SaveInvoice()
        {
            UpdateInvoice(CurrentInvoice);
            CurrentInvoice.Items = LineItems;
            CartDataService = new CartDataService();
            CurrentInvoice.Payments = PaymentModes;
            bool result;
            bool result2;
            if (IsClient)
            {
                result = await new ClientCartDataService().SaveInvoice(CurrentInvoice);
                if (CurrentInvoice.PromotionalAmount != 0)
                {
                    result2 = await new CartDataService().SavePromoAppliedDetails(CurrentInvoice, CurrentInvoice.AppliedPromos);
                }

            }
            else
            {
                result = await new CartDataService().SaveInvoice(CurrentInvoice);
                
                if (CurrentInvoice.PromotionalAmount != 0)
                {
                    result2 = await new CartDataService().SavePromoAppliedDetails(CurrentInvoice, CurrentInvoice.AppliedPromos);
                }
            }
            if (result)
            {
                if((BillingContext.Current.CurrentCart as Cart).SalesmanHelper.MetadataValuesInfosList.Count>0||!String.IsNullOrEmpty((BillingContext.Current.CurrentCart as Cart).SalesmanHelper.SelectedInvoiceSalesman)) ServiceLocator.Current.GetService<IMessageService>().Send(this, "SaveSalesmanInfo", CurrentInvoice);
                CurrentItem = null;
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "InvoiceSaved", CurrentInvoice);
                Token = RestaurantTokenContext.Instance.GenerateToken();
            }
            return result;
        }

        

        private void UpdateInvoice(InvoiceInfo info)
        {
            if (info.Customer != null)
            {
                info.CustomerPhone = info.Customer?.DisplayPhone;
                info.ShipToPhone = info.Customer?.DisplayPhone;
                info.BillToAddress = info.Customer?.Address;
                info.BillToGstin = info.Customer?.Gstin ?? "";
                info.BillToPhone = info.Customer?.DisplayPhone;
            }
            info.IsDeleted = false;
            info.IsSync = 1;
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
            CashPayment.Amount = 0;
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
            if (BillingContext.Current.CurrentCart is Cart)
            {
                (BillingContext.Current.CurrentCart as ICartService).SetVisual(new CartNotificationVisual("", false));
            }
            else
            {
                (BillingContext.Current.CurrentCart as IReturnCartService).SetVisual(new CartNotificationVisual("", false));
            }
            //reset all properties
            messageService.Send(this, "ResetDiscountTextBox", null);
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
                if (paymentMode is EzetapPaymentMode)
                {
                    PaymentModes.Add(paymentMode as EzetapPaymentMode);
                }
                else if (paymentMode is CardPaymentMode)
                {
                    PaymentModes.Add(paymentMode as CardPaymentMode);
                }
                else if (paymentMode is ChequePaymentMode)
                {
                    PaymentModes.Add(paymentMode as ChequePaymentMode);
                }
                else if (paymentMode is DeliveryChargesPaymentMode)
                {
                    PaymentModes.Add(paymentMode as DeliveryChargesPaymentMode);
                }
                Calculate();
            }
        }

        private void PaymentModeChanged()
        {
            var valuePaid = PaymentModes.Where(x => x.Name != "Cash").Sum(x => x.Amount);
            if (TotalValue - valuePaid > 0)
            {
                CurrentInvoice.DisplayCash = (long)(CashPayment.Amount = (TotalValue - valuePaid));
            }
            else
            {
                CurrentInvoice.DisplayCash = (long)(CashPayment.Amount = 0);
            }
            new CashCalculationComponent(this).Calculate();
        }

        public void RemovePaymentMode(IPaymentMode paymentMode)
        {
            if (paymentMode != null)
            {
                PaymentModes.Remove(paymentMode);
                Calculate();
            }
        }

        public async Task<IEnumerable<ProductCategoriesInfo>> GetProductCategories()
        {
            return await new CartDataService().GetProductCategories();
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

        public async Task QuickSaveCart(CartBackupInfo info)
        {
            try
            {
                await new CartDataService().QuickSaveCart(info);
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(this, ex);
            }
        }

        public async Task<bool> DeleteCartEntryFromBackup(CartBackupInfo cartBackup = null, long? BillStartedAt=null)
        {
            try
            {
                if (cartBackup == null)
                {
                    var result = await new CartDataService().DeleteCartEntryFromBackup(BillStartedAt??0);
                    return result;
                }
                else
                {
                    var result = await new CartDataService().DeleteCartEntryFromBackup(cartBackup.Id);
                    return result;
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(this, ex);
                return false;
            }
        }
        public async Task<IEnumerable<CartBackupInfo>> RetrieveSavedCarts()
        {
            return await new CartDataService().RetrieveSavedCarts();
        }
                
    }


    public class SnapOrderCart : Cart, ISnapCartService
    {
        private CurrencyManager currencyManager;

        public SnapOrderCart(IMessageService service) : base(service)
        {
        }
        public void Initialize()
        {
            IsScanningEnabled = false;
        }


        public override async Task AddToCart(ObservableCollection<SnapProductInfo> productpacks)
        {
            try
            {
                foreach (var p in productpacks)
                {
                    if (p == null)
                    {
                        return;
                    }
                    var result = p;
                    if (result.IsDeleted == 1)
                    {
                        return;
                    }
                    CurrentItem = result;
                    //result.UomEdited = ResolveUom.Instance.GetMaxMeasure(result.Uom);
                    var resolvedUomQtypair = QuantityUomResolverFactory.Resolve(p.DisplayQuantity, p.Uom);
                    result.UomEdited = resolvedUomQtypair.Uom;
                    result.DisplayQuantity = resolvedUomQtypair.Quantity;

                    var howmany = manager.HowManyInCartFor(result.BatchId);
                    if (howmany == 0)
                    {
                        var stockAvailable = await InventoryStockTracker.Current.GetRealTimeStockAvailabilityFor(result.BatchId, result.ProductCode.ToString());
                        if (!await InventoryStockTracker.Current.IsEnabled())
                        {


                            manager.Add(result.BatchId, result.DisplayQuantity);
                            LineItems.Add(result);

                        }
                        else
                        {
                            if (stockAvailable - result.DisplayQuantity >= 0)
                            {

                                manager.Add(result.BatchId, result.DisplayQuantity);
                                LineItems.Add(result);

                            }
                            else
                            {
                                await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast("Not Enough Stock", AlertType.Warning);
                            }
                        }
                    }

                    ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", result);
                    Calculate();
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(this, ex);
            }
        }

        public override async void UpdateItemInCart(SnapProductInfo product)
        {

            if (product != null)
            {
                CurrentItem = product;
                var howmany = manager.HowManyInCartFor(product.BatchId);
                var exitingLineItem = LineItems.Where(x => x.BatchId == product.BatchId).FirstOrDefault();
                exitingLineItem.DisplayQuantity = product.DisplayQuantity;
                if (howmany > product.DisplayQuantity)
                {
                    manager.Remove(exitingLineItem.BatchId, howmany - product.DisplayQuantity);
                }
                else
                {
                    manager.Add(exitingLineItem.BatchId, product.DisplayQuantity - howmany);
                }
            }
            ServiceLocator.Current.GetService<IMessageService>().Send(this, "CurrentItemUpdated", product);

            Calculate();
        }

        public override void Calculate()
        {
            base.Calculate();
        }



        public override async Task<bool> SaveInvoice()
        {
            UpdateInvoice(CurrentInvoice);
            CurrentInvoice.Items = LineItems;
            base.CartDataService = new CartDataService();
            CurrentInvoice.Payments = PaymentModes;
            //CurrentInvoice.Customer = TaggedCustomer;
            var result = await CartDataService.SaveSnapOrderInvoice(CurrentInvoice);
            if (result)
            {
                CurrentItem = null;
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "InvoiceSaved", CurrentInvoice);
            }
            return true;
        }

        private void UpdateInvoice(InvoiceInfo info)
        {
            if (info.Customer != null)
            {
                info.CustomerPhone = info.Customer?.DisplayPhone;
                info.ShipToPhone = info.Customer?.DisplayPhone;
                info.BillToAddress = info.Customer?.Address;
                info.BillToGstin = info.Customer?.Gstin ?? "";
                info.BillToPhone = info.Customer?.DisplayPhone;
            }
            info.IsDeleted = false;
            info.IsSync = 1;
            info.IsUpdated = 0;
            info.BillStartedAt = DateTime.UtcNow.Ticks;
            //info.BillerName= ServiceLocator.Current.GetService<ILoginService>().GetUserInfo()?.Username;
            info.BillerName = SessionInfo.Current.LoggedInUser.Username;
            info.PosName= ServiceLocator.Current.GetService<ISettingService>().PosId?.ToString();
        }

    }


    public class CartSerializer
    {
        public const string _diskdbPath = "local-SnapBillDataBase.sqlite";

        public string DiskDbPath { get { return _diskdbPath; } }

        public static object From(byte[] arrBytes)
        {
            MemoryStream memStream = new MemoryStream();
            BinaryFormatter binForm = new BinaryFormatter();
            memStream.Write(arrBytes, 0, arrBytes.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            var obj = (ObservableCollection<SnapProductInfo>)binForm.Deserialize(memStream);

            return obj;
        }

        public static byte[] To()
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, BillingContext.Current.CurrentCart.LineItems);
            return ms.ToArray();
        }
    }

    public class MockWeighingScaleListener : IListener
    {
        public event ListenerNotificationHandler NotificationReceived;

        public MockWeighingScaleListener()
        {
            //StartListening();
        }


        public void PauseListening()
        {

        }

        public void ResumeListening()
        {

        }



        public async Task StartListening()
        {
            //OnWeightDetectedCallback(MockWeighingMachine.StartEmittingResults().ToString());

        }
        public void OnWeightDetectedCallback(string weight)
        {
            BillingContext.Current.Notify(this, weight);
        }

        static class MockWeighingMachine
        {
            public static double StartEmittingResults()
            {
                var r = new Random();
                double m = 0;
                Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            await Task.Delay(2000);
                            var x = GetRandomNumber(700, 2500);
                            m = x;
                            //BillingContext.Current.Notify(typeof(MockWeighingMachine), x);
                        }
                    }
                    catch
                    {

                    }
                });
                return m;
            }

            private static double GetRandomNumber(double minimum, double maximum)
            {
                Random random = new Random();
                return random.NextDouble() * (maximum - minimum) + minimum;
            }
        }
    }

    public class InvoiceMoreThan90PercentTrigger : TriggerBase<InvoiceInfo>
    {
        public InvoiceMoreThan90PercentTrigger()
        {
            InvoiceInfo invoiceInfo = BillingContext.Current.CurrentCart?.CurrentInvoice;
            PromoResolver p2 = new PromoResolver();
            Predicate = invoice => p2.Resolve(invoiceInfo).Result&& invoice.NetAmount >= p2.Amt * 0.9 &&invoice.TotalItems!=0;
            Action = i => ShowToast(p2.PromoName,p2.Amt,p2.CurrentNetAmount);
            ResetAction = i => HideNotification(i);
        }
        private async void ShowToast(string a,double b,long f)
        {
            var c = BillingContext.Current.CurrentCart as ICartService;
            if(c!=null && (b-f)!=0)
            {
                c.SetVisual(new CartNotificationVisual("Add " + "\x20b9"+ $"{(b-f)/100} more to avail {a}", true));
            }
            else
            {
                c.SetVisual(new CartNotificationVisual("", false));

            }

        }
        private async void HideNotification(InvoiceInfo i)
        {
            var d = BillingContext.Current.CurrentCart as ICartService;
            if(d!=null)
            d.SetVisual(new CartNotificationVisual("", false));
        }

        public override Predicate<InvoiceInfo> Predicate { get; set; }
        public override Action<InvoiceInfo> Action { get; set; }
        public override Action<InvoiceInfo> ResetAction { get; set; }
    }
        public class PromoResolver
        {
        PromotionsCache Promos = new PromotionsCache();
        private static ObservableCollection<PromotionInfo> ActivePromos { get; set; }
        public string PromoName { get; set; }
        public double Amt { get; set; }        
        public long CurrentNetAmount { get; set; }
        public async Task<bool>Resolve(InvoiceInfo info)
        {
            ActivePromos = await Promos.GetActivePromotions(DiscountInfoType.AssignedToOrderTotal); //Get all active promos
            CurrentNetAmount = BillingContext.Current.CurrentCart.CurrentInvoice.NetAmount; //Get current invoice net amt
            foreach(var item in ActivePromos.Select((value, index) => new { value, index }))
            {
                var Minslabamt = (JsonConvert.DeserializeObject<OrderTotalInfo>(item.value.RulesString)).PromotionSlabs.Where(x => x.Condition.From >= CurrentNetAmount).FirstOrDefault();
                if (Minslabamt!=null)
                {
                    PromoName = ActivePromos.ElementAt(item.index).Name;
                    Amt = Minslabamt.Condition.From;
                    return true;
                }
            }
            return false;
        }
    } 
}