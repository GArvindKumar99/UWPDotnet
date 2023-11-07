using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using SnapBilling.Core.UI;

using SnapBilling.Data;
using SnapBilling.Data.Adapters;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.StoreConfigurationModule;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SnapBilling.CartModule.Services
{
    public class CartDataService
    {
        private ObservableCollection<ReceiptItem> receiptItems { get; set; }

        private DbContextOptionsBuilder<SnapBillingDbContext> optionsBuilder;

        public CartDataService()
        {
            optionsBuilder = new DbContextOptionsBuilder<SnapBillingDbContext>();
            optionsBuilder.UseSqlite($"{SnapBillingDbContext._dbPath}");
        }

        public async Task<List<ProductSuggestion>> GetMostPurchasedProduct(MostPurchasedProductsCriteria criteria)
        {
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    List<ProductSuggestion> products = new List<ProductSuggestion>();

                    var productList = (from it in context.Items
                                       where it.CategoryId != 0
                                       join inv in context.Invoices
                                       on it.InvoiceId equals inv.Id
                                       where inv.CustomerPhone == criteria.Phone && inv.IsDeleted != 1
                                       select Convert(it)).ToList();
                    var productCode = productList.GroupBy(x => x.ProductCode).Select(x => x.FirstOrDefault()).ToList();
                    foreach (var a in productCode)
                    {
                        var isDeleted = context.Products.FirstOrDefault(x => x.IsDeleted == 1 && x.Barcode == a.ProductCode);
                        if (isDeleted != null) { continue; }
                        a.Quantity = productList.Where(x => x.ProductCode == a.ProductCode).Sum(x => x.Quantity);
                        if (products.Count == 10)
                        {
                            break;
                        }

                        products.Add(a);
                    }
                    return products;
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                return null;
            }
        }

        public static ProductSuggestion Convert(Items i)
        {
            var c = new ProductSuggestion();
            c.ProductCode = i.ProductCode;
            c.Batchid = i.batchId;
            c.Name = i.Name;
            c.Uom=i.Uom;
            if ((i.Quantity >= 1000 || i.Quantity <= -1000) && i.Uom != "PC")
            {
                c.Uom = ResolveUom.Instance.GetMaxMeasure(i.Uom);
                c.Quantity = i.Quantity / 1000;
            }
            else
            {
                c.Quantity = i.Quantity;
            }
            return c;
        }

        internal async Task<IEnumerable<ProductCategoriesInfo>> GetQucikAddCategories()
        {
            List<ProductCategoriesInfo> qucikAddCategories = null;
            ProductCategoriesAdapter adapter = new ProductCategoriesAdapter();
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    var categories = await context.ProductCategories.Where(u => u.IsQuickAdd == 1).AsNoTracking().ToListAsync();
                    foreach (var c in categories)
                    {
                        qucikAddCategories.Add(adapter.ConvertFromProductCategoriesToProductCategoriesInfo(c));
                    }
                    return qucikAddCategories;
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                return null;
            }
        }

        internal async Task<IEnumerable<ProductCategoriesInfo>> GetAllGdbProductCategories()
        {
            List<ProductCategoriesInfo> qucikAddCategories = new List<ProductCategoriesInfo>();
            ProductCategoriesAdapter adapter = new ProductCategoriesAdapter();
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    var categories = await context.ProductCategories.AsNoTracking().ToListAsync();
                    foreach (var c in categories)
                    {
                        qucikAddCategories.Add(adapter.ConvertFromProductCategoriesToProductCategoriesInfo(c));
                    }
                    return qucikAddCategories;
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                return null;
            }
        }

        internal async Task<bool> UpdateQucikAddCategories(IEnumerable<ProductCategoriesInfo> productCategories, bool toAdd, bool toRemove)
        {
            try
            {
                List<ProductCategories> categories = new List<ProductCategories>();
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    if (toAdd)
                    {
                        foreach (var p in productCategories)
                        {
                            var cat = await (from pc in context.ProductCategories
                                             where pc.Id == p.Id
                                             select pc).FirstOrDefaultAsync();
                            cat.IsQuickAdd = 1;
                            categories.Add(cat);
                        }
                    }
                    else if (toRemove)
                    {
                        foreach (var p in productCategories)
                        {
                            var cat = await (from pc in context.ProductCategories
                                             where pc.Id == p.Id
                                             select pc).FirstOrDefaultAsync();
                            cat.IsQuickAdd = 0;
                            categories.Add(cat);
                        }
                    }
                    else
                    { }
                    if (categories.Count > 0)
                    {
                        context.ProductCategories.UpdateRange(categories);
                        context.SaveChanges();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                return false;
            }
        }

        internal async Task<bool> UpdateQucikAddCategories(IEnumerable<ProductCategoriesInfo> productCategories)
        {
            try
            {
                List<ProductCategoriesInfo> oldCategories = new List<ProductCategoriesInfo>();
                List<ProductCategoriesInfo> categories = new List<ProductCategoriesInfo>();

                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    using (IDbContextTransaction transaction = context.Database.BeginTransaction())
                    {
                        context.QuickAddCategories.RemoveRange(context.QuickAddCategories);
                        var qaList = new List<QuickAddCategories>();
                        int i = 0;
                        for (int j = 0; j < 15; j++)
                        {
                            if (productCategories?.Count() > j)
                            {
                                qaList.Add(new QuickAddCategories() { Key = $"qa_slot_{i.ToString("D2")}", Value = productCategories.ElementAt(j).Id, CreatedAt = DateTime.UtcNow.Ticks, isUpSyncPending = 1, UpdatedAt = DateTime.UtcNow.Ticks });

                            }
                            else
                            {
                                qaList.Add(new QuickAddCategories() { Key = $"qa_slot_{i.ToString("D2")}", Value = 0, CreatedAt = DateTime.UtcNow.Ticks, isUpSyncPending = 1, UpdatedAt = DateTime.UtcNow.Ticks });
                            }
                            i++;
                        }
                        context.QuickAddCategories.AddRange(qaList);
                        context.SaveChanges();
                        transaction.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                return false;
            }
        }

        internal async Task<bool> SavePromoAppliedDetails(InvoiceInfo info,List<PromotionDiscountPair> promotion)
        {
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {

                        List<PromotionsAppliedDetails> promotionsAppliedDetails = new List<PromotionsAppliedDetails>();
                    //foreach (var pd in promotion)
                    //{
                    //    promotionsAppliedDetails.Add(new PromotionsAppliedDetailsAdapter().ConvertToPromotionsAppliedDetails(info, pd));
                    //}
                    promotionsAppliedDetails.Add(new PromotionsAppliedDetailsAdapter().ConvertToPromotionsAppliedDetails(info, promotion));
                    context.AddRange(promotionsAppliedDetails);
                    context.SaveChanges();
                        return true;
                }
            }
            catch(Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                return false;
            }
        }
        internal async Task<bool> SaveInvoice(InvoiceInfo invoice)
        {
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    using (IDbContextTransaction transaction = context.Database.BeginTransaction())
                    {
                        List<Transactions> transactions = new List<Transactions>();
                        //save invoice
                        if (invoice.isReturn)
                        {
                            if (invoice.Customer != null)
                            {
                                invoice.CustomerPhone = invoice.Customer.DisplayPhone;
                            }
                            invoice.TotalAmount = -invoice.TotalAmount;
                            invoice.TotalSavings = -invoice.TotalSavings;
                            invoice.TotalDiscount = -invoice.TotalDiscount;
                            invoice.NetAmount = -invoice.NetAmount;
                            invoice.TotalIgstAmount = -invoice.TotalIgstAmount;
                            invoice.TotalCgstAmount = -invoice.TotalCgstAmount;
                            invoice.TotalSgstAmount = -invoice.TotalSgstAmount;
                            invoice.TotalCessAmount = -invoice.TotalCessAmount;
                            await AddInvoiceToContext(invoice, context);

                            var txns=await AddTransactionsToContext(invoice, context);
                            var txnAdapter = new TransactionAdapter();
                            invoice.Payments = new ObservableCollection<IPaymentMode>(txns.Select(x => PaymentFactory.ResolvePaymentMode(txnAdapter.ConvertTransactionToTransactionInfo(x), invoice.NetAmount < 0)));

                            if (invoice.Customer != null)
                            {
                                var customerDetails = UpdateCustomerDetailsToContext(invoice, context);
                                await UpdateCustomerMonthlySummary(invoice, context);
                                customerDetails.UpdatedAt = DateTime.UtcNow.Ticks;
                                customerDetails.isUpSyncPending = 1;
                                context.CustomerDetails.Update(customerDetails);
                            }

                            //product pack update
                            var adapter = new ProductLocalAdapter();
                            // var productPacks = invoice.Items?.Select(x => adapter.ConvertToProductPackFromSnapProductInfo(x));
                            //context.ProductPacks.UpdateRange(productPacks.ToArray());

                            //inventory update
                            foreach (var item in invoice.Items)
                            {
                                if ((item.IsLooseItem && item.IsQuickAdd == 0) || item.IsTransient)
                                { continue; }
                                var inv = context.Inventory.Where(v => v.BatchId == long.Parse(item.BatchId)).FirstOrDefault();
                                var productPacks = adapter.ConvertToProductPackFromSnapProductInfo(item);
                                double userDefinedDiscount = context.ProductPacks.Where(v => v.BatchId == long.Parse(item.BatchId)).AsNoTracking().FirstOrDefault().UserDefinedDiscount;
                                productPacks.UserDefinedDiscount = userDefinedDiscount;
                                //0012881: Return Cart | Product MRP and SP shouldn't be updated from the return cart.

                                //var mrpSetting = (await ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync()).GeneralQuickMRPBilling;
                                //if (mrpSetting)
                                //{
                                //    productPacks.Mrp = item.UserDefinedMrp;
                                //    productPacks.UpdatedAt = DateTime.UtcNow.Ticks;
                                //    productPacks.isUpSyncPending = 1;
                                //    context.ProductPacks.Update(productPacks);
                                //    inv.Mrp = item.UserDefinedMrp;
                                //}
                                //var spSetting = (await ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync()).UpdateSpWithoutChangingMrp;
                                //if (spSetting && (item.SelectedSalePrice == SelectedSalePrice.Custom))
                                //{
                                //    productPacks.SalePrice1 =item.SalePrice;
                                //    productPacks.isUpSyncPending = 1;
                                //    productPacks.UpdatedAt = DateTime.UtcNow.Ticks;
                                //    context.ProductPacks.Update(productPacks);
                                //    inv.SalePrice1 = (long)item.UserDefinedSalePrice;
                                //}
                                inv.Quantity += (long)(item.UomEdited == "KG" || item.UomEdited == "L" ? item.DisplayQuantity * 1000 : item.DisplayQuantity);
                                inv.UpdatedAt = DateTime.UtcNow.Ticks;
                                inv.isUpSyncPending = 1;
                            }
                        }
                        else
                        {
                            if (invoice.Customer != null)
                            {
                                invoice.CustomerPhone = invoice.Customer.DisplayPhone;
                            }

                            await AddInvoiceToContext(invoice, context);

                            await AddTransactionsToContext(invoice, context);

                            if (invoice.Customer != null)
                            {
                                var customerDetails = UpdateCustomerDetailsToContext(invoice, context);
                                await UpdateCustomerMonthlySummary(invoice, context);
                                customerDetails.UpdatedAt = DateTime.UtcNow.Ticks;
                                customerDetails.isUpSyncPending = 1;

                                context.CustomerDetails.Update(customerDetails);
                            }
                            context.SaveChanges();

                            //product pack update
                            var adapter = new ProductLocalAdapter();
                            // var productPacks = invoice.Items?.Select(x => adapter.ConvertToProductPackFromSnapProductInfo(x));
                            //context.ProductPacks.UpdateRange(productPacks.ToArray());

                            //inventory update
                            foreach (SnapProductInfo item in invoice.Items)
                            {
                                if ((item.IsLooseItem && item.IsQuickAdd == 0) || item.IsTransient)
                                { continue; }
                                var inv = context.Inventory.Where(v => v.BatchId == long.Parse(item.BatchId)).FirstOrDefault();
                                var productPacks = adapter.ConvertToProductPackFromSnapProductInfo(item);
                                var ifExist = context.ProductPacks.Where(x => x.BatchId == productPacks.BatchId).FirstOrDefault();
                                context.Entry(ifExist).State = EntityState.Detached;
                                List<ProductPacks> otherBatches = context.ProductPacks.Where(v => v.Barcode == item.Barcode && v.BatchId != long.Parse(item.BatchId)).ToList();
                                foreach (var batch in otherBatches)
                                {
                                    batch.Name = item.Name;
                                }
                                double userDefinedDiscount = context.ProductPacks.Where(v => v.BatchId == long.Parse(item.BatchId)).AsNoTracking().FirstOrDefault().UserDefinedDiscount;
                                productPacks.UserDefinedDiscount = userDefinedDiscount;
                                var settingsInfo = await ServiceLocator.Current.GetService<ISettingService>().GetGeneralSettingAsync();
                                
                                if (item.IsEdited)
                                {
                                    var product = adapter.ConvertToProductFromSnapProductInfo(item);
                                    var ifProductExists = context.Products.Where(x => x.ProductCode == product.ProductCode).FirstOrDefault();
                                    context.Entry(ifProductExists).State = EntityState.Detached;
                                    if (product.IsSnapOrder==1)
                                    {
                                        product.IsSnapOrderSync = 0;
                                    }
                                    product.UpdatedAt = DateTime.UtcNow.Ticks;
                                    context.Products.Update(product);
                                }
                                if (settingsInfo.GeneralQuickMRPBilling)
                                {
                                    productPacks.Mrp = item.UserDefinedMrp;
                                    inv.Mrp = item.UserDefinedMrp;
                                }
                                else
                                {
                                    productPacks.Mrp = inv.Mrp;
                                }
                                if (settingsInfo.UpdateSpWithoutChangingMrp && (item.SelectedSalePrice == SelectedSalePrice.Custom))
                                {
                                    productPacks.SalePrice1 = (long)item.UserDefinedSalePrice;
                                    productPacks.SalePrice2 = Math.Min((long)item.UserDefinedSalePrice, (long)item.SalePrice2);
                                    productPacks.SalePrice3 = Math.Min((long)item.UserDefinedSalePrice, (long)item.SalePrice3);

                                    inv.SalePrice1 = (long)item.UserDefinedSalePrice;
                                    inv.SalePrice2 = Math.Min((long)item.UserDefinedSalePrice, (long)item.SalePrice2);
                                    inv.SalePrice3 = Math.Min((long)item.UserDefinedSalePrice, (long)item.SalePrice3);
                                }
                                productPacks.UpdatedAt = DateTime.UtcNow.Ticks;
                                productPacks.isUpSyncPending = 1;
                                context.ProductPacks.Update(productPacks);

                                inv.Quantity -=(long)(item.UomEdited == "KG" || item.UomEdited == "L" ? item.DisplayQuantity * 1000 : item.DisplayQuantity);
                                inv.UpdatedAt = DateTime.UtcNow.Ticks;
                                inv.isUpSyncPending = 1;
                                context.ProductPacks.UpdateRange(otherBatches);

                            }
                            await context.SaveChangesAsync();
                        }

                        context.SaveChanges();

                        transaction.Commit();
                        ServiceLocator.Current.GetService<ILogService>().Info(nameof(CartDataService), $"Invoice saved: {invoice.Id}");
                        try
                        {
                            var f = invoice.Items.Where(x => x.IsTransient)?.ToList();
                            var isClient = ServiceLocator.Current.GetService<ISettingService>().IsMultiPosEnabled().Result;

                            if (f?.Count != 0 && isClient != null && !isClient.Value)
                            {
                                foreach (var i in f)
                                {
                                    i.Mrp = i.UserDefinedMrp;
                                    i.SalePrice = i.SalePrice;
                                    i.SalePrice1 = i.SalePrice1;
                                    i.SalePrice2 = i.SalePrice2;
                                    i.SalePrice3 = i.SalePrice3;
                                    i.PurchasePrice = i.PurchasePrice;
                                    i.IsSnapOrderProduct = 1;
                                    i.Quantity -= i.Uom=="G"||i.Uom=="ML"? (long)i.DisplayQuantity*1000:(long)i.DisplayQuantity;
                                    ServiceLocator.Current.GetService<IProductCatalogueService>().SaveProductAsync(i, true);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                        }
                        try
                        {
                           if (invoice.ToPrint)
                            {
                                var printerService = ServiceLocator.Current.GetService<IPrinterService>();
                                PrinterConfiguration printerConf = await ServiceLocator.Current.GetService<ISettingService>().GetPrinterConfigrationSettingAsync();
                                var name = printerConf.PrinterName;
                                ReceiptPrint prices = new ReceiptPrint();
                                receiptItems = new ObservableCollection<ReceiptItem>();
                                prices.InvoiceId = invoice.Id;
                                prices.TokenId = invoice.Token;
                                if (invoice.isReturn)
                                {
                                    foreach (var items in invoice.Items)
                                    {
                                        ReceiptItem item = new ReceiptItem();
                                        item.ItemName = items.Name;
                                        item.Attributes = items.Attributes == null ? string.Empty : string.Join("/", items.Attributes.Where(x => x.IsEligibleForPrintAsync(x.Id) && !string.IsNullOrEmpty(x.SetValue)).Select(x => x.SetValue ?? string.Empty));
                                        item.Quantity = /*-*/items.QuantityView;
                                        item.SellingPrice = (double)items.UserDefinedSalePrice / 100;
                                        item.Amount = /*-*/items.TotalAmount / 100;
                                        double disPer = items.SalePrice!=0? Math.Round((items.DiscountOverride / (double)items.SalePrice)*100, 2, MidpointRounding.AwayFromZero) :100;
                                        item.Saving = VersionMode.IsEnterprise()? disPer: (items.Saving) / 100;
                                        item.Mrp = (double)items.UserDefinedMrp / 100;
                                        item.Hsn = items.HsnCode;
                                        item.GstR = items.IgstRate == null ? 0.0 : items.IgstRate;
                                        item.CgstR = items.CgstRate == null ? 0.0 : items.CgstRate;
                                        item.SgstR = items.SgstRate == null ? 0.0 : items.SgstRate;
                                        item.CessR = items.CessRate == null ? 0.0 : items.CessRate;
                                        item.AdditionalCessR = items.AdditionalCessRate == null ? 0.0 : items.AdditionalCessRate;
                                        item.CgstA = items.CgstAmount == null ? 0.0 : -(double)items?.CgstAmount / 100;
                                        item.SgstA = items.SgstAmount == null ? 0.0 : -(double)items?.SgstAmount / 100;
                                        item.CessA = items.CessAmount == null ? 0.0 : -(double)items?.CessAmount / 100;
                                        item.AdditionalCessA = items.AdditionalCessAmount == null ? 0.0 : -(double)items?.AdditionalCessAmount / 100;
                                        item.Uom = /*"-" + */items.QuantityViewWithUom;
                                        receiptItems.Add(item);
                                    }
                                    prices.Date = invoice.UpdatedAtDateTime.ToLocalTime();
                                    prices.IsMemo = invoice.IsMemo;
                                    prices.IsReturn = invoice.isReturn;
                                    prices.GrossAmount = -(double)invoice.TotalAmount / 100;
                                    prices.Savings = -(double)(invoice.TotalSavings) / 100;
                                    prices.Discount = -(double)invoice.TotalDiscount / 100;
                                    prices.NetAmount = -(double)invoice.NetAmount / 100;
                                    prices.TotalSavings = -(double)invoice.TotalSavings / 100;
                                    prices.CashPaid = (double)invoice.Change / 100;
                                    prices.TotalItems = invoice.TotalItems;
                                    prices.TotalIemsQuantity = invoice.TotalQuantity;
                                    prices.CreditAmount = (double)invoice.PendingAmount / 100;
                                    prices.Change = (double)(invoice.Change??0) / 100;
                                    prices.TotalGstAmount = -(double?)invoice.TotalGstAmount / 100;
                                    prices.TotalCgstAmount = -(double?)invoice.TotalCgstAmount / 100;
                                    prices.TotalSgstAmount = -(double?)invoice.TotalSgstAmount / 100;
                                    prices.TotalCessAmount = -(double?)invoice.TotalCessAmount / 100;
                                    prices.TotalAdditionalCessAmount = -(double?)invoice.TotalAdditionalCessAmount / 100;
                                    prices.WalletPayment = invoice.Payments.Where(x => x is WalletPaymentMode).Sum(x => x.Amount) / 100;
                                    if (invoice.RoundOffAmount != 0)
                                    {
                                        prices.RoundOffAmount = string.Concat(invoice.RoundOffAmount > 0 ? "+" : "", invoice.RoundOffAmount.ToString());

                                    }
                                    if (invoice.Customer != null)
                                    {
                                        prices.CustomerName = invoice.Customer.Name;
                                        prices.CustomerPhone = invoice.Customer.DisplayPhone;
                                        prices.CustomerAddress = invoice.Customer.Address;
                                        prices.DueAmount = invoice.PendingAmount;
                                        prices.CustomerDueAmount = invoice.Customer.AmountDue;
                                        prices.CustomerGstIn = invoice.Customer.Gstin;
                                        prices.MembershipId = invoice.Customer.MembershipId;
                                    }
                                   
                                }
                                else
                                {
                                    foreach (var items in invoice.Items)
                                    {
                                        ReceiptItem item = new ReceiptItem();
                                        item.ItemName = items.Name;
                                        item.Attributes = items.Attributes == null ? string.Empty : string.Join("/", items.Attributes.Where(x => x.IsEligibleForPrintAsync(x.Id) && !string.IsNullOrEmpty(x.SetValue)).Select(x => x.SetValue ?? string.Empty));
                                        item.Quantity = items.QuantityView;
                                        item.ActualQuantity = items.DisplayQuantity;
                                        item.SellingPrice = (double)items.UserDefinedSalePrice / 100;
                                        item.Amount = items.TotalAmount / 100;
                                        double disPer = items.SalePrice != 0 ? Math.Round((items.DiscountOverride / (double)items.SalePrice)*100, 2, MidpointRounding.AwayFromZero) : 100;
                                        item.Saving = VersionMode.IsEnterprise() ? disPer : (items.Saving) / 100;
                                        item.Mrp = (double)items.UserDefinedMrp / 100;
                                        item.Hsn = items.HsnCode;
                                        item.GstR = items.IgstRate == null ? 0.0 : items.IgstRate;
                                        item.CgstR = items.CgstRate == null ? 0.0 : items.CgstRate;
                                        item.SgstR = items.SgstRate == null ? 0.0 : items.SgstRate;
                                        item.CessR = items.CessRate == null ? 0.0 : items.CessRate;
                                        item.AdditionalCessR = items.AdditionalCessRate == null ? 0.0 : items.AdditionalCessRate;

                                        item.CgstA = items.CgstAmount == null ? 0.0 : (double)items?.CgstAmount / 100;
                                        item.SgstA = items.SgstAmount == null ? 0.0 : (double)items?.SgstAmount / 100;
                                        item.CessA = items.CessAmount == null ? 0.0 : (double)items?.CessAmount / 100;
                                        item.AdditionalCessA = items.AdditionalCessAmount == null ? 0.0 : (double)items?.AdditionalCessAmount / 100;
                                        item.Uom = items.QuantityViewWithUom;
                                        receiptItems.Add(item);
                                    }
                                    prices.PromotionalAmt = (long)invoice.PromotionalAmount / 100;
                                    prices.Date = invoice.UpdatedAtDateTime.ToLocalTime();
                                    prices.IsMemo = invoice.IsMemo;
                                    prices.GrossAmount = (double)invoice.TotalAmount / 100;
                                    prices.Savings = (double)(invoice.TotalSavings) / 100;
                                    prices.Discount = (double)invoice.TotalDiscount / 100;
                                    prices.NetAmount = (double)invoice.NetAmount / 100;
                                    prices.DeliveryCharges = (double)invoice.DeliveryCharges / 100;
                                    prices.TotalSavings = (double)invoice.TotalSavings / 100;
                                    prices.CashPaid = (double)invoice.DisplayCash / 100;
                                    prices.Change = (double)(invoice.Change??0) / 100;
                                    prices.TotalItems = invoice.TotalItems;
                                    prices.TotalIemsQuantity = invoice.TotalQuantity;
                                    prices.DigitalPayment = invoice.Payments.Where(x => x is DigitalPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.CardPayment = invoice.Payments.Where(x => x is CardPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.WalletPayment = invoice.Payments.Where(x => x is WalletPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.ChequePayment = invoice.Payments.Where(x => x is ChequePaymentMode).Sum(x => x.Amount) / 100;
                                    prices.EzetapPayment = invoice.Payments.Where(x => x is EzetapPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.CreditAmount = (double)invoice.PendingAmount / 100;
                                    prices.TotalGstAmount = (double?)invoice.TotalGstAmount / 100;
                                    prices.TotalCgstAmount = (double?)invoice.TotalCgstAmount / 100;
                                    prices.TotalSgstAmount = (double?)invoice.TotalSgstAmount / 100;
                                    prices.TotalCessAmount = (double?)invoice.TotalCessAmount / 100;
                                    prices.TotalAdditionalCessAmount = (double?)invoice.TotalAdditionalCessAmount / 100;
                                    var total = (double)invoice.TotalDiscount + (double)invoice.NetAmount;
                                    var per = (double)invoice.TotalDiscount / total;
                                    prices.DiscountPercentage = (Math.Round((per * 100), 2)).ToString() + "%";
                                    if (invoice.RoundOffAmount != 0)
                                    {
                                        prices.RoundOffAmount = string.Concat(invoice.RoundOffAmount > 0 ? "+" : "", invoice.RoundOffAmount.ToString());

                                    }
                                    if (invoice.Customer != null)
                                    {
                                        prices.CustomerName = invoice.Customer.Name;
                                        prices.CustomerPhone = invoice.Customer.DisplayPhone;
                                        prices.CustomerAddress = invoice.Customer.Address;
                                        prices.DueAmount = invoice.PendingAmount;
                                        prices.CustomerDueAmount = invoice.Customer.AmountDue;
                                        prices.CustomerGstIn = invoice.Customer.Gstin;
                                        prices.MembershipId = invoice.Customer.MembershipId;
                                    }
                                }
                                printerService.Init(name);
                                printerService.SalesBillPrint(receiptItems, prices);
                                if (printerConf.PrinterMode != "NORMAL")
                                {
                                    printerService.FullPaperCut();
                                    printerService.OpenDrawer();
                                    printerService.PrintDocument();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ServiceLocator.Current.GetService<ILogService>().LogException(this, ex);
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().LogException(this, ex);
                return false;
            }
        }
        internal async Task<bool> DeleteCartEntryFromBackup(long billStartedAt)
        {
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    var cartEntry = await context.CartBackup.Where(x => x.Id == billStartedAt).FirstOrDefaultAsync();
                    if (cartEntry != null)
                    {
                        context.CartBackup.Remove(cartEntry);
                        context.SaveChanges();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(this, ex);
                return false;
            }
        }

        internal async Task<IEnumerable<CartBackupInfo>> RetrieveSavedCarts()
        {
            List<CartBackupInfo> savedCarts = new List<CartBackupInfo>();

            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    var adapter = new CartBackupAdapter();
                    var currentSession = context.UserAccessLog.Where(x => x.LoggedOutAt == null).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
                    if (currentSession == null)
                    { throw new Exception("Null current session in UserAccessLogs"); }                    
                    var currUserId = context.Users.Where(x => x.Name == currentSession.Username).Select(x => x.Id).FirstOrDefault();

                    var temp = await (from cb in context.CartBackup
                                      where cb.UserId == currUserId 
                                      select cb).ToListAsync();//&& cb.CreatedAt >= currentSession.LoggedInAt

                    foreach (var t in temp)
                    {
                        savedCarts.Add(adapter.ConvertFromBackupToInfo(t));
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(this, ex);
            }
            return savedCarts;

        }

        internal async Task QuickSaveCart(CartBackupInfo info)
        {
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    var currentUserlog = context.UserAccessLog.Where(x => x.LoggedOutAt == null).OrderByDescending(x => x.CreatedAt).First();
                    var currentUserid = context.Users.Where(x => x.Name == currentUserlog.Username).Select(x => x.Id).FirstOrDefault();

                    var ifExist = context.CartBackup.Where(x => x.UserId == currentUserid && x.Id == info.Id).FirstOrDefault();
                    if (ifExist == null)
                    {
                        context.CartBackup.Add(new CartBackup()
                        {
                            CartData = info.CartData,
                            CreatedAt = info.createdAt,
                            UpdatedAt = info.updatedAt,
                            UserId = currentUserid,
                            TotalAmount = info.TotalAmount,
                            ItemCount = info.ItemCount,
                            CustomerName = info.CustomerName,
                            Id = info.Id
                        });
                    }
                    else
                    {
                        ifExist.CartData = info.CartData;
                        ifExist.TotalAmount = info.TotalAmount;
                        ifExist.ItemCount = info.ItemCount;
                        ifExist.CustomerName = info.CustomerName;
                        ifExist.UpdatedAt = info.updatedAt;
                        context.Update(ifExist);
                        //context.Entry(ifExist).State = EntityState.Detached;
                    }
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(CartDataService), ex);
            }
        }

        internal async Task<bool> SaveSnapOrderInvoice(InvoiceInfo invoice)
        {
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    //using (IDbContextTransaction transaction = context.Database.BeginTransaction())
                    {
                        invoice.IsGst = true;
                        //save invoice
                        if (invoice.CustomerPhone != null)
                        {
                            //invoice.CustomerPhone = invoice.Customer.DisplayPhone;
                            await CheckIfCustomerExistAndSave(invoice);
                        }

                        await AddInvoiceToContext(invoice, context);

                        await AddTransactionsToContext(invoice, context);

                            if (invoice.CustomerPhone != null)
                            {
                                var customerDetails = UpdateCustomerDetailsToContext(invoice, context);
                                await UpdateCustomerMonthlySummary(invoice, context);
                                customerDetails.UpdatedAt = DateTime.UtcNow.Ticks;
                                customerDetails.isUpSyncPending = 1;
                            context.CustomerDetails.Update(customerDetails);
                            }

                        //product pack update
                        var adapter = new ProductLocalAdapter();
                        // var productPacks = invoice.Items?.Select(x => adapter.ConvertToProductPackFromSnapProductInfo(x));
                        //context.ProductPacks.UpdateRange(productPacks.ToArray());

                            //inventory update
                            foreach (var x in invoice.Items)
                            {

                                var inv = context.Inventory.Where(v => v.BatchId == long.Parse(x.BatchId)).FirstOrDefault();
                                inv.Quantity -= (long)x.DisplayQuantity;
                                inv.UpdatedAt = DateTime.UtcNow.Ticks;
                            inv.isUpSyncPending = 1;
                        }
                        
                        context.SaveChanges();
                        //transaction.Commit();
                        try
                        {
                            if (invoice.ToPrint)
                            {
                                var printerService = ServiceLocator.Current.GetService<IPrinterService>();
                                PrinterConfiguration printerConf = await ServiceLocator.Current.GetService<ISettingService>().GetPrinterConfigrationSettingAsync();
                                var name = printerConf.PrinterName;
                                ReceiptPrint prices = new ReceiptPrint();
                                receiptItems = new ObservableCollection<ReceiptItem>();
                                prices.InvoiceId = invoice.Id;
                                if (invoice.isReturn)
                                {
                                    foreach (var items in invoice.Items)
                                    {
                                        ReceiptItem item = new ReceiptItem();
                                        item.ItemName = items.Name;
                                        item.Quantity = -items.QuantityView;
                                        item.SellingPrice = items.UserDefinedSalePrice / 100;
                                        item.Amount = -items.TotalAmount / 100;
                                        item.Saving = (items.Saving) / 100;
                                        item.Mrp = items.UserDefinedMrp / 100;
                                        item.Hsn = items.HsnCode;
                                        item.GstR = items.IgstRate == null ? 0.0 : items.IgstRate;
                                        item.CgstR = items.CgstRate == null ? 0.0 : items.CgstRate;
                                        item.SgstR = items.SgstRate == null ? 0.0 : items.SgstRate;
                                        item.CessR = items.CessRate == null ? 0.0 : items.CessRate;
                                        item.AdditionalCessR = items.AdditionalCessRate == null ? 0.0 : items.AdditionalCessRate;
                                        item.CgstA = items.CgstAmount == null ? 0.0 : -(double)items?.CgstAmount / 100;
                                        item.SgstA = items.SgstAmount == null ? 0.0 : -(double)items?.SgstAmount / 100;
                                        item.CessA = items.CessAmount == null ? 0.0 : -(double)items?.CessAmount / 100;
                                        item.AdditionalCessA = items.AdditionalCessAmount == null ? 0.0 : -(double)items?.AdditionalCessAmount / 100;
                                        item.Uom = "-" + items.QuantityViewWithUom;
                                        receiptItems.Add(item);
                                    }
                                    prices.Date = invoice.UpdatedAtDateTime.ToLocalTime();
                                    prices.IsReturn = invoice.isReturn;
                                    prices.IsMemo = invoice.IsMemo;
                                    prices.GrossAmount = -(double)invoice.TotalAmount / 100;
                                    prices.Savings = -(double)(invoice.TotalSavings) / 100;
                                    prices.Discount = -(double)invoice.TotalDiscount / 100;
                                    prices.NetAmount = -(double)invoice.NetAmount / 100;
                                    prices.TotalSavings = -(double)invoice.TotalSavings / 100;
                                    prices.CashPaid = (double)invoice.Change / 100;
                                    prices.TotalItems = invoice.TotalItems;
                                    prices.TotalIemsQuantity = invoice.TotalQuantity;
                                    prices.CreditAmount = (double)invoice.PendingAmount / 100;
                                    prices.Change = (double)(invoice.Change ?? 0) / 100;
                                    prices.TotalGstAmount = -(double?)invoice.TotalGstAmount / 100;
                                    prices.TotalCgstAmount = -(double?)invoice.TotalCgstAmount / 100;
                                    prices.TotalSgstAmount = -(double?)invoice.TotalSgstAmount / 100;
                                    prices.TotalCessAmount = -(double?)invoice.TotalCessAmount / 100;
                                    prices.TotalAdditionalCessAmount = -(double?)invoice.TotalAdditionalCessAmount / 100;
                                    if (invoice.RoundOffAmount != 0)
                                    {
                                        prices.RoundOffAmount = string.Concat(invoice.RoundOffAmount > 0 ? "+" : "", invoice.RoundOffAmount.ToString());

                                    }
                                    if (invoice.Customer != null)
                                    {
                                        prices.CustomerName = invoice.Customer.Name;
                                        prices.CustomerPhone = invoice.Customer.DisplayPhone;
                                        prices.CustomerAddress = invoice.Customer.Address;
                                        prices.DueAmount = invoice.PendingAmount;
                                        prices.CustomerDueAmount = invoice.Customer.AmountDue;
                                        prices.CustomerGstIn = invoice.Customer.Gstin;
                                        prices.MembershipId = invoice.Customer.MembershipId;
                                    }
                                }
                                else
                                {
                                    foreach (var items in invoice.Items)
                                    {
                                        ReceiptItem item = new ReceiptItem();
                                        item.ItemName = items.Name;
                                        item.Quantity = items.QuantityView;
                                        item.ActualQuantity = items.DisplayQuantity;
                                        item.SellingPrice = (double)items.UserDefinedSalePrice / 100;
                                        item.Amount = (double)items.TotalAmount / 100;
                                        item.Mrp = (double)items.UserDefinedMrp / 100;
                                        double disPer = items.SalePrice != 0 ? Math.Round(((items.Mrp - (double)items.SalePrice) / items.Mrp)*100,2, MidpointRounding.AwayFromZero) :100;
                                        item.Saving = VersionMode.IsEnterprise() ? disPer : (items.Saving) / 100;
                                        item.Hsn = items.HsnCode;
                                        item.GstR = items.IgstRate == null ? 0.0 : items.IgstRate;
                                        item.CgstR = items.CgstRate == null ? 0.0 : items.CgstRate;
                                        item.SgstR = items.SgstRate == null ? 0.0 : items.SgstRate;
                                        item.CessR = items.CessRate == null ? 0.0 : items.CessRate;
                                        item.AdditionalCessR = items.AdditionalCessRate == null ? 0.0 : items.AdditionalCessRate;

                                        item.CgstA = items.CgstAmount == null ? 0.0 : (double)items?.CgstAmount / 100;
                                        item.SgstA = items.SgstAmount == null ? 0.0 : (double)items?.SgstAmount / 100;
                                        item.CessA = items.CessAmount == null ? 0.0 : (double)items?.CessAmount / 100;
                                        item.AdditionalCessA = items.AdditionalCessAmount == null ? 0.0 : (double)items?.AdditionalCessAmount / 100;
                                        item.Uom = items.QuantityViewWithUom;
                                        receiptItems.Add(item);
                                    }
                                    prices.Date = invoice.UpdatedAtDateTime.ToLocalTime();
                                    prices.IsMemo = invoice.IsMemo;
                                    prices.GrossAmount = (double)invoice.TotalAmount / 100;
                                    prices.Savings = (double)(invoice.TotalSavings) / 100;
                                    prices.Discount = (double)invoice.TotalDiscount / 100;
                                    prices.NetAmount = (double)invoice.NetAmount / 100;
                                    prices.DeliveryCharges = (double)invoice.DeliveryCharges / 100;
                                    prices.TotalSavings = (double)invoice.TotalSavings / 100;
                                    prices.CashPaid = (double)invoice.DisplayCash / 100;
                                    prices.Change = (double)(invoice.Change ?? 0) / 100;
                                    prices.TotalItems = invoice.TotalItems;
                                    prices.TotalIemsQuantity = invoice.TotalQuantity;
                                    prices.DigitalPayment = invoice.Payments.Where(x => x is DigitalPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.CardPayment = invoice.Payments.Where(x => x is CardPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.ChequePayment = invoice.Payments.Where(x => x is ChequePaymentMode).Sum(x => x.Amount) / 100;
                                    prices.WalletPayment = invoice.Payments.Where(x => x is WalletPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.EzetapPayment = invoice.Payments.Where(x => x is EzetapPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.CreditAmount = (double)invoice.PendingAmount / 100;
                                    prices.TotalGstAmount = (double?)invoice.TotalGstAmount / 100;
                                    prices.TotalCgstAmount = (double?)invoice.TotalCgstAmount / 100;
                                    prices.TotalSgstAmount = (double?)invoice.TotalSgstAmount / 100;
                                    prices.TotalCessAmount = (double?)invoice.TotalCessAmount / 100;
                                    prices.TotalAdditionalCessAmount = (double?)invoice.TotalAdditionalCessAmount / 100;
                                    if (invoice.Customer != null)
                                    {
                                        prices.CustomerName = invoice.Customer.Name;
                                        prices.CustomerPhone = invoice.Customer.DisplayPhone;
                                        prices.CustomerAddress = invoice.Customer.Address;
                                        prices.DueAmount = invoice.PendingAmount;
                                        prices.CustomerDueAmount = invoice.Customer.AmountDue;
                                        prices.CustomerGstIn = invoice.Customer.Gstin;
                                        prices.MembershipId = invoice.Customer.MembershipId;
                                    }
                                }
                                printerService.Init(name);
                                printerService.SalesBillPrint(receiptItems, prices);
                                if (printerConf.PrinterMode != "NORMAL")
                                {
                                    printerService.FullPaperCut();
                                    printerService.OpenDrawer();
                                    printerService.PrintDocument();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ServiceLocator.Current.GetService<ILogService>().LogException(this, ex);
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().LogException(this, ex);
                return false;
            }
        }

        private async Task CheckIfCustomerExistAndSave(InvoiceInfo invoice)
        {
            CustomerViewInfo displayCustomer = new CustomerViewInfo
            {
                Phone = (long)invoice.BillToPhone,
                Name = invoice.CustomerName,
                Address = invoice.BillToAddress,
                IsExternal = true,
                ExternalId = invoice.ExternalCustomerId
            };

            await ServiceLocator.Current.GetService<ICustomerService>().SaveCustomerAsync(displayCustomer);
        }

        private IEnumerable<ProductCategoriesInfo> categories = null;

        internal async Task<IEnumerable<ProductCategoriesInfo>> GetProductCategories()
        {
            if (categories == null)
            {
                try
                {
                    using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                    {
                        categories = await (from q in context.QuickAddCategories
                                            where q.Value != 0
                                            join p in context.ProductCategories
                                            on q.Value equals p.Id
                                            select ConvertToProductCategoriesInfo(q, p)).AsNoTracking().ToListAsync();
                        categories = categories.OrderBy(x => x.Key);
                        return categories;
                    }
                }
                catch (Exception ex)
                {
                    ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                    return null;
                }
            }
            return categories;
        }

        private static ProductCategoriesInfo ConvertToProductCategoriesInfo(QuickAddCategories q, ProductCategories product)
        {
            var i = new ProductCategoriesInfo();
            i.Id = product.Id;
            i.Name = product.Name;
            i.ParentId = (long)product.ParentId;
            i.VatId = product.VatId;
            i.IsQuickAdd = product.IsQuickAdd == 1;
            i.Margin = (decimal)product.Margin;
            i.createdAt = product.CreatedAt;
            i.updatedAt = product.UpdatedAt;
            i.Key = q.Key;

            return i;
        }

        internal ObservableCollection<SnapProductInfo> GetBestOfferProduct()
        {
            ObservableCollection<SnapProductInfo> snapProductInfos = new ObservableCollection<SnapProductInfo>();

            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    var products = context.Products.Where(u => u.IsOffer == 1 && u.IsDeleted != 1).AsNoTracking().Take(25).ToList();

                    foreach (var product in products)
                    {
                        var productPacks = context.ProductPacks.Where(x => x.Barcode == product.Barcode && x.IsDisabled == 0).FirstOrDefault();
                        if (productPacks == null) { continue; }
                        var inventory = context.Inventory.Where(x => x.Barcode == product.Barcode || x.ProductCode == product.ProductCode).FirstOrDefault();
                        snapProductInfos.Add(new ProductLocalAdapter().ConvertToSnapProductInfo(productPacks, product, inventory));
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
            }
            return snapProductInfos;
        }

        private async Task UpdateCustomerMonthlySummary(InvoiceInfo invoice, SnapBillingDbContext context)
        {
            var currentDate = DateTime.Now;
            long value = (currentDate.Year * 100) + (currentDate.Month);
            var currentMonthYear = (currentDate.Year * 100) + (currentDate.Month); ;
            var customerMonthlySummary = await context.CustomerMonthlySummary.FirstOrDefaultAsync(x => x.Phone == invoice.Customer.Phone && x.Month == currentMonthYear);
            var newData = false;
            if (customerMonthlySummary == null)
            {
                newData = true;
                customerMonthlySummary = new CustomerMonthlySummary()
                {
                    Id = GetMaxCustomerMonthlySummary() + 1,
                    Phone = invoice.Customer.Phone,
                    CreatedAt = DateTime.Now.ToUniversalTime().Ticks,
                    Month = currentMonthYear,
                };
            }
            customerMonthlySummary.UpdatedAt = currentDate.ToUniversalTime().Ticks;
            customerMonthlySummary.isUpSyncPending =1;
            customerMonthlySummary.AmountDue =
                (customerMonthlySummary.AmountDue ?? 0) + invoice.PendingAmount;
            customerMonthlySummary.AmountPaid =
            (customerMonthlySummary.AmountPaid ?? 0) + (invoice.NetAmount - invoice.PendingAmount);
            customerMonthlySummary.PurchaseValue =
                (customerMonthlySummary.PurchaseValue ?? 0) + invoice.NetAmount;

            if (newData)
            {
                context.CustomerMonthlySummary.Add(customerMonthlySummary);
            }
            else
            {
                context.CustomerMonthlySummary.Update(customerMonthlySummary);
            }
        }

        private CustomerDetails UpdateCustomerDetailsToContext(InvoiceInfo invoice, SnapBillingDbContext context)
        {
            var customerDetails = context.CustomerDetails.FirstOrDefault(x => x.Phone == invoice.Customer.Phone);
            //code needs to be birufcated based on client or master
            if (invoice.IsCredit)
            {
                // Add credit only if there is some credit involved
                customerDetails.AmountDue = customerDetails.AmountDue + invoice.PendingAmount;
            }
            if (!invoice.isReturn && invoice.HasWalletTransaction())
            {
                customerDetails.AmountDue = customerDetails.AmountDue + invoice.WalletAmountUsed();
            }
            if (invoice.AddToCustomerWallet)
            {
                customerDetails.AmountDue = customerDetails.AmountDue - (long)invoice.Customer.DuesCleared - (long)invoice.AddToWalletAmount;
            }
            customerDetails.Phone = invoice.Customer.Phone;
            customerDetails.AmountSaved = customerDetails.AmountSaved + invoice.TotalSavings + invoice.TotalDiscount;
            customerDetails.LastPurchaseAmount = invoice.NetAmount;
            if (!invoice.IsCredit)//full paid
            {
                customerDetails.LastPaymentAmount = Math.Max(invoice.DisplayCash, invoice.NetAmount);
                customerDetails.LastPaymentDate = invoice.createdAt;
            }
            else if (invoice.NetAmount > invoice.PendingAmount)//partially paid
            {
                customerDetails.LastPaymentAmount = invoice.NetAmount - invoice.PendingAmount;
                customerDetails.LastPaymentDate = invoice.createdAt;
            }
            customerDetails.LastPurchaseDate = invoice.createdAt;
            customerDetails.PurchaseValue = customerDetails.PurchaseValue + invoice.NetAmount;
            customerDetails.TotalVisits++;
            invoice.Customer.AmountDue = customerDetails.AmountDue;
            return customerDetails;
        }

        private async Task<IEnumerable<Transactions>> AddTransactionsToContext(InvoiceInfo invoice, SnapBillingDbContext context)
        {
            var id = await ServiceLocator.Current.GetService<IInvoiceService>().GetNextTransactionId();
            if (invoice.isReturn)
            {
                var trans = await ReturnTransactionFactory.CreateTransactionsFor(invoice);
                foreach (var t in trans)
                {
                    context.Transactions.Add(t);
                }
                return trans;
            }
            else
            {
                var trans = await TransactionFactory.CreateTransactionsFor(invoice, context);
                foreach (var t in trans)
                {
                    context.Transactions.Add(t);
                }
            }
            return null;
        }

        private async Task AddInvoiceToContext(InvoiceInfo invoice, SnapBillingDbContext context)
        {
            invoice.Id = await ServiceLocator.Current.GetService<IInvoiceService>().GetNextInvoiceId(!invoice.IsMemo);
            invoice.updatedAt = invoice.createdAt = DateTime.Now.ToUniversalTime().Ticks;
            invoice.IsSync = 0;
            invoice.IsUpdated = 0;
            var result = new InvoiceAdapter().ConvertToInvoices(invoice);
            context.Invoices.Add(result);
            List<Items> Items = new List<Items>();
            long count = 0;
            foreach (var snapProduct in invoice.Items)
            {
                if (invoice.isReturn)
                {
                    //if ((x.IsLooseItem && x.IsQuickAdd == 0) || x.IsTransient)
                    //{ continue; }

                    snapProduct.IgstAmount = -snapProduct.IgstAmount;
                    snapProduct.CgstAmount = -snapProduct.CgstAmount;
                    snapProduct.SgstAmount = -snapProduct.SgstAmount;
                    snapProduct.CessAmount = -snapProduct.CessAmount;
                    var item = new ProductLocalAdapter().ConvertToItemsFromSnapProduct(snapProduct, invoice.Id);
                    item.Attributes = JsonConvert.SerializeObject(snapProduct.Attributes);
                    item.Id = GetMaxId() + 1 + count;
                    item.Quantity = (long)-item.Quantity;
                    item.TotalAmount = -item.TotalAmount;
                    item.Savings = -item.Savings;
                    item.batchId = long.Parse(snapProduct.BatchId);
                    count++;
                    Items.Add(item);
                    context.Items.Add(item);
                }
                else
                {
                    //if ((x.IsLooseItem && x.IsQuickAdd == 0) /*|| x.IsTransient*/)
                    //{ continue; }
                    var item = new ProductLocalAdapter().ConvertToItemsFromSnapProduct(snapProduct, invoice.Id);
                    item.Attributes = JsonConvert.SerializeObject(snapProduct.Attributes);
                    item.Id = GetMaxId() + 1 + count;
                    item.batchId = long.Parse(snapProduct.BatchId);
                    count++;
                    Items.Add(item);
                    context.Items.Add(item);
                    //var t = From(x,item.Id);
                    //if (t.Count() > 0)
                    // context.MetaData.AddRange(t);
                }
            }
        }

        public IEnumerable<MetaData> From(SnapProductInfo info, long id)
        {
            List<MetaData> list = new List<MetaData>();

            foreach (var m in info.MetaData)
            {
                list.Add(new MetaData() { ItemId = id, ItemType = 2, Key = m.Key, Value = m.Value });
            }
            return list;
        }

        internal async Task<ObservableCollection<QuickAddProductInfo>> GetQuickAddProductsByCategory(long obj)
        {
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    ObservableCollection<QuickAddProductInfo> list = new ObservableCollection<QuickAddProductInfo>();

                    var c = from pp in context.ProductPacks
                            join p in context.Products
                            on pp.ProductCode equals p.ProductCode
                            join i in context.Inventory
                            on pp.ProductCode equals i.ProductCode
                            where p.CategoryId == obj && p.IsQuickAdd == 1 && pp.BatchId == i.BatchId && p.IsDeleted == 0 && pp.IsDisabled == 0
                            select new ProductPackAdapter().ConvertToQuickAddProductInfo(pp, p, i);

                    var product = await c.ToListAsync();

                    if (product?.Count() > 0)
                    {
                        int count = 0;
                        foreach (var i in product)
                        {
                            count++;
                            i.SalePrice = i.SalePrice1;
                            i.DisplayId = count;
                            list.Add(i);
                        }
                        return list;
                    }
                    return list;
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);
                return null;
            }
        }

        private long GetMaxCustomerMonthlySummary()
        {
            using (var context = new SnapBillingDbContext(optionsBuilder.Options))
            {
                long max = 0;
                try
                {
                    max = context.CustomerMonthlySummary.Max(x => x.Id);
                }
                catch
                {
                }
                return max;
            }
        }

        public class TransactionFactory
        {
            internal static async Task<List<TransactionInfo>> CreateTransactionsForDuesClearance(InvoiceInfo info, SnapBillingDbContext context)
            {
                long amountPaid = 0, totalAmountPaid = 0;
                List<TransactionInfo> txnList = new List<TransactionInfo>();
                var cust = info.Customer;
                long adjustment = info.AddToWalletAmount;
                try
                {
                    {
                        var invoiceAdapter = new InvoiceAdapter();
                        var creditInvoices = ServiceLocator.Current.GetService<ICustomerService>().GetCreditInvoicesAsync(info.Customer.Phone).Result;
                        if (creditInvoices.Count > 0)
                        {
                            foreach (var creditInvoice in creditInvoices)
                            {
                                if (creditInvoice.PendingAmount < adjustment)
                                {
                                    adjustment -= creditInvoice.PendingAmount;
                                    amountPaid = creditInvoice.PendingAmount;
                                    creditInvoice.PendingAmount = 0;
                                }
                                else
                                {
                                    creditInvoice.PendingAmount -= adjustment;
                                    amountPaid = adjustment;
                                    adjustment = 0;
                                }
                                creditInvoice.updatedAt = DateTime.UtcNow.Ticks;
                                var invoice = invoiceAdapter.ConvertToInvoices(creditInvoice);
                                context.Invoices.Update(invoice);
                                int saved = context.SaveChanges();
                                if (saved > 0)
                                {
                                    var transaction = new TransactionInfo() { };
                                    transaction.TransactionId = 0;
                                    transaction.InvoiceId = invoice.Id;
                                    transaction.TenderedAmount = amountPaid;
                                    transaction.PaidAmount = amountPaid;
                                    transaction.PaymentMode = StringConstants.TransactionsStringConstants.PaymentModeStringConstants.Cash;
                                    transaction.TransactionBankName = StringConstants.TransactionsStringConstants.PaymentModeStringConstants.Cash;
                                    transaction.PaymentType = StringConstants.TransactionsStringConstants.PaymentTypeStringConstants.Credit;
                                    transaction.CustomerPhone = cust.Phone;
                                    transaction.createdAt = DateTime.UtcNow.Ticks;
                                    transaction.updatedAt = DateTime.UtcNow.Ticks;
                                    transaction.TransactionDate = DateTime.UtcNow.Ticks;
                                    transaction.RemainingAmount = 0;//customerInfo.AmountDue- (totalAmountPaid==0?amountPaid:totalAmountPaid);
                                    transaction.POS_ID = System.Convert.ToInt64(invoice.PosName??"0");
                                    transaction.BillerName = invoice.BillerName; 
                                    txnList.Add(transaction);

                                    totalAmountPaid += amountPaid;
                                }
                                else
                                {
                                    throw new Exception("Failed to update invoices pending amount.");
                                }
                                info.Customer.AmountDue -= amountPaid;
                                amountPaid = 0;
                                if (adjustment == 0)
                                    break;
                            }
                        }
                        info.Customer.DuesCleared = totalAmountPaid;
                        info.AddToWalletAmount -= totalAmountPaid;
                    }
                }
                catch (Exception ex)
                {
                    ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(CartDataService), "", ex.Message, ex.StackTrace);

                    return null;
                }

                return txnList;
            }

            public static async Task<IEnumerable<Transactions>> CreateTransactionsFor(InvoiceInfo info, SnapBillingDbContext context)
            {
                List<Transactions> list = new List<Transactions>();
                TransactionAdapter adapter = new TransactionAdapter();
                var id = await ServiceLocator.Current.GetService<IInvoiceService>().GetNextTransactionId() - 1;

                if (info.HasWalletTransaction())
                {
                    foreach (var x in info.GetWalletPayments())
                    {
                        list.Add(CreateWalletTransaction(x, info, ++id));
                    }
                }

                if (info.AddToCustomerWallet)
                {
                    if (info.WasCashUsed())
                    {
                        list.Add(CreateCashTransactionWithCustomerWalletAdd(info, ++id));
                    }
                    var txnList = CreateTransactionsForDuesClearance(info, context).Result;
                    foreach (var t in txnList)
                    {
                        var temp = adapter.ConvertTransactionInfoToTransactions(t);
                        temp.isUpSyncPending = 1;
                        temp.Id = ++id;
                        temp.IsSync = 1;
                        list.Add(temp);
                    }

                    list.Add(CreateCustomerWalletTransaction(info, ++id));
                }
                else
                {
                    if (info.WasCashUsed())

                    {
                        list.Add(info.IsCredit ?
                            CreateCashTransactionForCreditInvoice(info, ++id) :
                            CreatePlainCashTransaction(info, ++id));
                    }
                }

                if (info.HasDigitalTransaction())
                {
                    foreach (var x in info.GetDigitalPayments())
                    {
                        list.Add(CreateDigitalTransaction(x, info, ++id));
                    }
                }

                if (info.WasChequeUsed())
                {
                    foreach (var x in info.GetChequePayments())
                    {
                        list.Add(CreateChequeTransaction(x, info, ++id));
                    }
                }      
                if (info.WasEzetapUsed())
                {
                    foreach (var x in info.GetEzetapPayments())
                    {
                        list.Add(CreateEzetapTransaction(x, info, ++id));
                    }
                }

                if (info.WasCardUsed())
                {
                    foreach (var x in info.GetCardPayments())
                    {
                        list.Add(CreateCardTransaction(x, info, ++id));
                    }
                }

                return list;
            }

            private static Transactions CreateCustomerWalletTransaction(InvoiceInfo info, long id)
            {
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var cashTransaction = info.Payments.First(t => t is CashPaymentMode);
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "ADVANCE";
                transaction.PaymentMode = "CASH";
                transaction.Amount = info.AddToWalletAmount;
                transaction.TenderedAmount = info.DisplayCash > info.AddToWalletAmount ? info.DisplayCash : info.NetAmount + info.AddToWalletAmount;
                transaction.RemainingAmount = info.ImpliedWalletAmount();//info.Change.Value;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.CustomerId = info.Customer?.Id;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;
                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }

            private static Transactions CreatePlainCashTransaction(InvoiceInfo info, long id)
            {
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var cashTransaction = info.Payments?.First(t => t is CashPaymentMode);
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "CURRENT";
                transaction.PaymentMode = "CASH";
                transaction.Amount = info.ImpliedCash();
                transaction.TenderedAmount = info.DisplayCash;
                transaction.RemainingAmount = 0;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.CustomerId = info.Customer?.Id;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;
                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }

            private static Transactions CreateCashTransactionWithCustomerWalletAdd(InvoiceInfo info, long id)
            {
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var cashTransaction = info.Payments.First(t => t is CashPaymentMode);
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "CURRENT";
                transaction.PaymentMode = "CASH";
                transaction.Amount = info.ImpliedCash();// info.DisplayCash;
                transaction.TenderedAmount = info.DisplayCash;
                transaction.RemainingAmount = 0;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.CustomerId = info.Customer?.Id;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;
                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }

            private static Transactions CreateCashTransactionForCreditInvoice(InvoiceInfo info, long id)
            {
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var cashTransaction = info.Payments.First(t => t is CashPaymentMode);
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "CURRENT";
                transaction.PaymentMode = "CASH";
                transaction.Amount = info.DisplayCash;
                transaction.TenderedAmount = info.DisplayCash;
                transaction.RemainingAmount = 0;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.CustomerId = info.Customer?.Id;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;
                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }

            private static Transactions CreateDigitalTransaction(IPaymentMode mode, InvoiceInfo info, long id)
            {
                var digital = mode as DigitalPaymentMode;
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "CURRENT";
                transaction.PaymentMode = "DIGITAL";
                transaction.Amount = (long)digital.Amount;
                transaction.TenderedAmount = (long)digital.Amount;
                transaction.RemainingAmount = 0;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;
                transaction.CreatedAt = now;
                transaction.UpdatedAt = now; transaction.isUpSyncPending = 1;
                transaction.TransactionType = digital.PaymentMode;
                transaction.TransactionDesc = digital.Discription;
                transaction.TransactionRefNo = digital.TransactionId;
                transaction.TransactionBankName = null;// digital.PaymentMode;
                transaction.CustomerId = info.Customer?.Id;
                transaction.ParentTransactionId = null;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }

            private static Transactions CreateWalletTransaction(IPaymentMode mode, InvoiceInfo info, long id)
            {
                var digital = mode as WalletPaymentMode;
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "CURRENT";
                transaction.PaymentMode = "WALLET";
                transaction.Amount = (long)digital.Amount;
                transaction.TenderedAmount = (long)digital.Amount;
                transaction.RemainingAmount = 0;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;
                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.CustomerId = info.Customer?.Id;
                transaction.ParentTransactionId = null;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }

            private static Transactions CreateChequeTransaction(IPaymentMode mode, InvoiceInfo info, long id)
            {
                var cheque = mode as ChequePaymentMode;
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "CURRENT";
                transaction.PaymentMode = "CHEQUE";
                transaction.Amount = (long)cheque.Amount;
                transaction.TenderedAmount = (long)cheque.Amount;
                transaction.RemainingAmount = 0;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;

                transaction.TransactionBankName = cheque.BankName;
                transaction.TransactionDesc = cheque.ChequeDiscription;
                transaction.TransactionRefNo = cheque.ChequeNo;

                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.CustomerId = info.Customer?.Id;
                transaction.ParentTransactionId = null;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }       
            private static Transactions CreateEzetapTransaction(IPaymentMode mode, InvoiceInfo info, long id)
            {
                var ezetap = mode as EzetapPaymentMode;
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "CURRENT";
                transaction.PaymentMode = "Ezetap";
                transaction.Amount = (long)ezetap.Amount;
                transaction.TenderedAmount = (long)ezetap.Amount;
                transaction.RemainingAmount = 0;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;
                //transaction.TransactionType = ezetap.transactipnType;
                //transaction.TransactionBankName = ezetap.BankName;
                transaction.TransactionDesc = ezetap.Description;
                transaction.TransactionRefNo = ezetap.TransactionId;

                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.CustomerId = info.Customer?.Id;
                transaction.ParentTransactionId = null;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }

            private static Transactions CreateCardTransaction(IPaymentMode mode, InvoiceInfo info, long id)
            {
                var cardmode = mode as CardPaymentMode;
                var now = DateTime.Now.ToUniversalTime().Ticks;
                var transaction = new Transactions();
                transaction.Id = id;
                transaction.InvoiceId = info.Id;
                transaction.PaymentType = "CURRENT";
                transaction.PaymentMode = "CARD";
                transaction.Amount = (long)cardmode.Amount;
                transaction.TenderedAmount = (long)cardmode.Amount;
                transaction.RemainingAmount = 0;
                transaction.CustomerPhone = info.Customer?.Phone;
                transaction.TransactionDate = now;
                transaction.IsUpdated = 0;
                transaction.IsSync = 1;
                transaction.TransactionDesc = cardmode.Description;
                transaction.TransactionRefNo = cardmode.TransactionId;
                transaction.TransactionType = cardmode.CardType;
                transaction.TransactionBankName = "CardPay";
                transaction.CreatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.UpdatedAt = now;
                transaction.CustomerId = info.Customer?.Id;
                transaction.ParentTransactionId = null;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
            }
        }

        public long GetMaxId()
        {
            using (var context = new SnapBillingDbContext(optionsBuilder.Options))
            {
                long max = 0;
                try
                {
                    max = context.Items.Max(x => x.Id);
                }
                catch
                {
                }
                return max;
            }
        }

        public class ReturnTransactionFactory
        {
            public static async Task<IEnumerable<Transactions>> CreateTransactionsFor(InvoiceInfo info)
            {
                List<Transactions> list = new List<Transactions>();

                var id = await ServiceLocator.Current.GetService<IInvoiceService>().GetNextTransactionId();

                if (info.AddToCustomerWallet)
                {
                    list.Add(CreateCustomerWalletTransaction(info, id));
                }
                else
                {
                    if (info.WasCashUsed())

                    {
                        list.Add(CreatePlainCashTransaction(info, id));
                    }
                }

                    return list;
                }
                private static Transactions CreatePlainCashTransaction(InvoiceInfo info, long id)
                {
                    var now = DateTime.Now.ToUniversalTime().Ticks;
                    var cashTransaction = info.Payments?.First(t => t is CashPaymentMode);
                    var transaction = new Transactions();
                    transaction.Id = id;
                    transaction.InvoiceId = info.Id;
                    transaction.PaymentType = "CURRENT";
                    transaction.PaymentMode = "CASH";
                    transaction.Amount = info.ImpliedCash();
                    transaction.TenderedAmount = info.DisplayCash;
                    transaction.RemainingAmount = 0;
                    transaction.CustomerPhone = info.Customer?.Phone;
                    transaction.CustomerId = info.Customer?.Id;
                    transaction.TransactionDate = now;
                    transaction.IsUpdated = 0;
                    transaction.IsSync = 1;
                    transaction.CreatedAt = now;
                    transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.PosName = info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
                }

                private static Transactions CreateCustomerWalletTransaction(InvoiceInfo info, long id)
                {
                    var now = DateTime.Now.ToUniversalTime().Ticks;
                    var cashTransaction = info.Payments.First(t => t is CashPaymentMode);
                    var transaction = new Transactions();
                    transaction.Id = id;
                    transaction.InvoiceId = info.Id;
                    transaction.PaymentType = "ADVANCE";
                    transaction.PaymentMode = "CASH";
                    transaction.Amount = info.AddToWalletAmount;
                    transaction.TenderedAmount = info.NetAmount + info.AddToWalletAmount;
                    transaction.RemainingAmount = info.ImpliedWalletAmount(); //info.Change.Value;
                    transaction.CustomerPhone = info.Customer?.Phone;
                    transaction.CustomerId = info.Customer?.Id;
                    transaction.TransactionDate = now;
                    transaction.IsUpdated = 0;
                    transaction.IsSync = 1;
                    transaction.CreatedAt = now;
                    transaction.UpdatedAt = now;
                transaction.isUpSyncPending = 1;
                transaction.PosName= info.PosName;
                transaction.BillerName = info.BillerName;
                return transaction;
                }

            }

        


    }
}