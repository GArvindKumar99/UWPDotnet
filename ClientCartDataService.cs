using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using SnapBilling.Data;
using SnapBilling.Data.Adapters;
using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SnapBilling.CartModule.Services
{
    public class ClientCartDataService
    {
        private IEnumerable<ProductCategoriesInfo> categories = null;
        private DbContextOptionsBuilder<SnapBillingDbContext> optionsBuilder;
        public ClientCartDataService()
        {
            optionsBuilder = new DbContextOptionsBuilder<SnapBillingDbContext>();
            optionsBuilder.UseSqlite($"{SnapBillingDbContext._dbPath}");
        }

        private ObservableCollection<ReceiptItem> receiptItems { get; set; }
        public static ProductSuggestion Convert(Items i)
        {
            var c = new ProductSuggestion();
            c.ProductCode = i.ProductCode;
            c.Batchid = i.batchId;
            c.Name = i.Name;
            if ((i.Quantity >= 1000 || i.Quantity <= -1000) && i.Uom != "PC")
            {
                c.Quantity = i.Quantity / 1000;
            }
            else
            {
                c.Quantity = i.Quantity;
            }
            return c;
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
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
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
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
                return null;
            }
        }

        internal ObservableCollection<SnapProductInfo> GetBestOfferProduct()
        {
            try
            {
                using (var context = new SnapBillingDbContext(optionsBuilder.Options))
                {
                    ObservableCollection<SnapProductInfo> snapProductInfos = new ObservableCollection<SnapProductInfo>();
                    var products = context.Products.Where(u => u.IsOffer == 1 && u.IsDeleted != 1).AsNoTracking().ToList();

                    foreach (var product in products)
                    {
                        var productPacks = context.ProductPacks.Where(x => x.Barcode == product.Barcode).FirstOrDefault();
                        var inventory = context.Inventory.Where(x => x.Barcode == product.Barcode).FirstOrDefault();
                        snapProductInfos.Add(new ProductLocalAdapter().ConvertToSnapProductInfo(productPacks, product, inventory));
                    }
                    return snapProductInfos;
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
                return null;
            }
        }

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
                    ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
                    return null;
                }
            }
            return categories;
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
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
                return null;
            }
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
                            where p.CategoryId == obj && p.IsQuickAdd == 1 && pp.BatchId == i.BatchId && p.IsDeleted == 0
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
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
                return null;
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

                            await AddTransactionsToContext(invoice, context);

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

                            //product pack update
                            var adapter = new ProductLocalAdapter();
                            // var productPacks = invoice.Items?.Select(x => adapter.ConvertToProductPackFromSnapProductInfo(x));
                            //context.ProductPacks.UpdateRange(productPacks.ToArray());

                            //inventory update
                            foreach (var item in invoice.Items)
                            {
                                if ((item.IsLooseItem && item.IsQuickAdd == 0) || item.IsTransient)
                                { continue; }
                                if (item.BatchId != item.ProductCode.ToString())
                                {
                                    var inv = context.Inventory.Where(v => v.BatchId == long.Parse(item.BatchId)).FirstOrDefault();
                                    inv.Quantity -= (long)(item.UomEdited == "KG" || item.UomEdited == "L" ? item.DisplayQuantity * 1000 : item.DisplayQuantity);
                                    inv.UpdatedAt = DateTime.UtcNow.Ticks;
                                    inv.isUpSyncPending = 1;
                                }
                            }
                        }
                        context.SaveChanges();
                        transaction.Commit();
                        ServiceLocator.Current.GetService<ILogService>().Info(nameof(ClientCartDataService), $"Invoice saved: {invoice.Id}");
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
                                    i.Quantity = (long?)-i.DisplayQuantity;
                                    ServiceLocator.Current.GetService<IProductCatalogueService>().SaveProductAsync(i, true);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
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
                                if (invoice.isReturn)
                                {
                                    foreach (var items in invoice.Items)
                                    {
                                        ReceiptItem item = new ReceiptItem();
                                        item.ItemName = items.Name;
                                        item.Quantity = -items.QuantityView;
                                        item.SellingPrice = (double)items.UserDefinedSalePrice / 100;
                                        item.Amount = -items.TotalAmount / 100;
                                        double disPer = items.SalePrice != 0 ? Math.Round((items.DiscountOverride / (double)items.SalePrice) * 100, 2, MidpointRounding.AwayFromZero) : 100;
                                        item.Saving = VersionMode.IsEnterprise() ? disPer : (items.Saving) / 100;
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
                                        double disPer = items.SalePrice != 0 ? Math.Round((items.DiscountOverride / (double)items.SalePrice) * 100, 2, MidpointRounding.AwayFromZero) : 100;
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
                                    prices.Date = invoice.UpdatedAtDateTime.ToLocalTime();
                                    prices.PromotionalAmt = (long)invoice.PromotionalAmount / 100;
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
                                    prices.EzetapPayment = invoice.Payments.Where(x => x is EzetapPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.WalletPayment = invoice.Payments.Where(x => x is WalletPaymentMode).Sum(x => x.Amount) / 100;
                                    prices.CreditAmount = (double)invoice.PendingAmount / 100;
                                    prices.TotalGstAmount = (double?)invoice.TotalGstAmount / 100;
                                    prices.TotalCgstAmount = (double?)invoice.TotalCgstAmount / 100;
                                    prices.TotalSgstAmount = (double?)invoice.TotalSgstAmount / 100;
                                    prices.TotalCessAmount = (double?)invoice.TotalCessAmount / 100;
                                    prices.TotalAdditionalCessAmount = (double?)invoice.TotalAdditionalCessAmount / 100;
                                    var total = (double)invoice.TotalDiscount + (double)invoice.NetAmount;
                                    var per = (double)invoice.TotalDiscount / total;
                                    prices.DiscountPercentage = (Math.Round((per * 100), 2)).ToString() + "%";
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
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
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
                ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);
                return false;
            }
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
        private async Task AddInvoiceToContext(InvoiceInfo invoice, SnapBillingDbContext context)
        {
            invoice.Id = await ServiceLocator.Current.GetService<IInvoiceService>().GetNextInvoiceId(!invoice.IsMemo);
            invoice.updatedAt = invoice.createdAt = DateTime.Now.ToUniversalTime().Ticks;
            invoice.IsSync = 0;
            invoice.IsUpdated = 0;
            invoice.ToReconcile = 1;
            var result = new InvoiceAdapter().ConvertToInvoices(invoice);
            context.Invoices.Add(result);
            List<Items> Items = new List<Items>();
            long count = 0;
            foreach (var x in invoice.Items)
            {
                if (invoice.isReturn)
                {
                    //if ((x.IsLooseItem && x.IsQuickAdd == 0) || x.IsTransient)
                    //{ continue; }

                    x.IgstAmount = -x.IgstAmount;
                    x.CgstAmount = -x.CgstAmount;
                    x.SgstAmount = -x.SgstAmount;
                    x.CessAmount = -x.CessAmount;
                    var item = new ProductLocalAdapter().ConvertToItemsFromSnapProduct(x, invoice.Id);
                    item.Attributes = JsonConvert.SerializeObject(x.Attributes);
                    item.Id = GetMaxId() + 1 + count;
                    item.Quantity = (long)-x.DisplayQuantity;
                    item.TotalAmount = -item.TotalAmount;
                    item.Savings = -item.Savings;
                    count++;
                    Items.Add(item);
                    context.Items.Add(item);
                }
                else
                {
                    //if ((x.IsLooseItem && x.IsQuickAdd == 0) /*|| x.IsTransient*/)
                    //{ continue; }
                    var item = new ProductLocalAdapter().ConvertToItemsFromSnapProduct(x, invoice.Id);
                    item.Attributes = JsonConvert.SerializeObject(x.Attributes);
                    item.Id = GetMaxId() + 1 + count;
                    if (x.BatchId == x.ProductCode.ToString())
                    {
                        item.batchId = DateTime.UtcNow.Ticks;
                    }
                    else
                    {
                        item.batchId = long.Parse(x.BatchId);
                    }
                    count++;
                    Items.Add(item);
                    context.Items.Add(item);
                }
            }
        }

        private async Task AddTransactionsToContext(InvoiceInfo invoice, SnapBillingDbContext context)
        {
            var id = await ServiceLocator.Current.GetService<IInvoiceService>().GetNextTransactionId();
            if (invoice.isReturn)
            {
                var trans = await ReturnTransactionFactory.CreateTransactionsFor(invoice);
                foreach (var t in trans)
                {
                    context.Transactions.Add(t);
                }
            }
            else
            {
                var trans = await TransactionFactory.CreateTransactionsFor(invoice, context);
                foreach (var t in trans)
                {
                    context.Transactions.Add(t);
                }
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

        private CustomerDetails UpdateCustomerDetailsToContext(InvoiceInfo invoice, SnapBillingDbContext context)
        {
            var customerDetails = context.CustomerDetails.FirstOrDefault(x => x.Phone == invoice.Customer.Phone);
            if (invoice.IsCredit)
            {
                customerDetails.AmountDue = customerDetails.AmountDue + invoice.PendingAmount;
            }
            customerDetails.Phone = invoice.Customer.Phone;
            customerDetails.AmountSaved = customerDetails.AmountSaved + invoice.TotalSavings + invoice.TotalDiscount;
            customerDetails.LastPaymentAmount = invoice.NetAmount;
            customerDetails.LastPurchaseDate = invoice.createdAt;
            customerDetails.PurchaseValue = customerDetails.PurchaseValue + invoice.NetAmount;
            customerDetails.TotalVisits++;
            invoice.Customer.AmountDue = customerDetails.AmountDue;
            return customerDetails;
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
            customerMonthlySummary.isUpSyncPending = 1;
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
                transaction.RemainingAmount = info.Change.Value;
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
        }

        public class TransactionFactory
        {
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
                    ServiceLocator.Current.GetService<ILogService>().WriteAsync(LogType.Error, nameof(ClientCartDataService), "", ex.Message, ex.StackTrace);

                    return null;
                }

                return txnList;
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
                transaction.isUpSyncPending = 1;
                transaction.TransactionDesc = cardmode.Description;
                transaction.TransactionRefNo = cardmode.TransactionId;
                transaction.TransactionType = cardmode.CardType;
                transaction.TransactionBankName = "CardPay";
                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
                transaction.CustomerId = info.Customer?.Id;
                transaction.ParentTransactionId = null;
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
                transaction.isUpSyncPending = 1;
                transaction.IsSync = 1;
                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
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
                transaction.isUpSyncPending = 1;

                transaction.CreatedAt = now;
                transaction.UpdatedAt = now;
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
                transaction.UpdatedAt = now;
                transaction.TransactionBankName = null;// digital.PaymentMode;
                transaction.TransactionRefNo = digital.TransactionId;
                transaction.TransactionDesc = digital.Discription;
                transaction.TransactionType = digital.PaymentMode;
                transaction.CustomerId = info.Customer?.Id;
                transaction.ParentTransactionId = null;
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
                transaction.CustomerId = info.Customer?.Id;
                transaction.ParentTransactionId = null;
                transaction.isUpSyncPending = 1;
                return transaction;
            }
        }
    }
}