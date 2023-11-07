using SnapBilling.Services;
using SnapBilling.Services.AppServices;
using SnapBilling.Services.AppServices.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapBilling.CartModule.Services
{
    public class ProductCatalogService : IProductCatalogService
    {
        public async Task<List<BrandsInfo>> SearchBrands(string searchQuery)
        {
            return await ServiceLocator.Current.GetService<ISearchService>().SearchBrands(searchQuery);
        }

        public async Task<ObservableCollection<SearchResult>> SearchProduct(string query)
        {
            return null;
        // return await ServiceLocator.Current.GetService<ISearchService>().SearchProduct(query);
        }
        public async Task<List<ProductPackCompact>> SearchProductCompact(string query)
        {
            return await ServiceLocator.Current.GetService<ISearchService>().SearchProductCompact(query);
        }
    }
}
