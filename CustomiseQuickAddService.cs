using SnapBilling.Services;
using SnapBilling.Services.AppServices.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapBilling.CartModule.Services
{
    public class CustomisedQucikAddService : ICustomisedQuickAdd
    {
        public CartDataService dataService = new CartDataService();

        public async Task<IEnumerable<ProductCategoriesInfo>> GetAllGdbProductCategories()
        {
            return await dataService.GetAllGdbProductCategories();
        }

        public async Task<IEnumerable<ProductCategoriesInfo>> GetQucikAddCategories()
        {
            return await dataService.GetQucikAddCategories();
        }

        public async Task<bool> UpdateQucikAddCategories(IEnumerable<ProductCategoriesInfo> productCategories)
        {
            var result = await dataService.UpdateQucikAddCategories(productCategories);
            if (result)
            {
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "customizecategories", null);
            }
            return result;
        }
        public async Task<bool> UpdateQucikAddCategories(IEnumerable<ProductCategoriesInfo> productCategories, bool toAdd, bool toRemove)
        {
            var result = await dataService.UpdateQucikAddCategories(productCategories, toAdd, toRemove);
            if (result)
            {
                ServiceLocator.Current.GetService<IMessageService>().Send(this, "customizecategories", null);
            }
            return result;
        }
    }
}
