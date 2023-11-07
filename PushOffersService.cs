using SnapBilling.Services.AppServices;
using SnapBilling.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SnapBilling.Services.AppServices.AppSettingClasses;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using System.Net.Http;
using System.ServiceModel.Channels;
using WeihanLi.Common.Http;
using System.Security.Policy;
using SnapBilling.Data;
using SnapBilling.Services.AppServices.Services;
using System.Net.Http.Headers;

namespace SnapBilling.PushOffers
{
    public class ResponseData
    {
        public int store_id { get; set; }
        public DateTime expires_at { get; set; }
        public int live_sms_count { get; set; }
        public int sms_sent { get; set; }
    }

    public class Response
    {
        public List<ResponseData> data { get; set; }
        public int subCode { get; set; }
        public string message { get; set; }
        public string status { get; set; }
    }
    public class PushOffersService : IPushOffersService
    {
        private readonly PushOffersDataService dataService = new PushOffersDataService();

        public async Task<bool> SendPushOffer(IEnumerable<long> selectedCustomers, PushOfferInfo pushOffer)
        {
            try
            {               
                #region PostPushOffer
                List<PostPushOffer> pushOffers = new List<PostPushOffer>
                {
                    new PostPushOffer()
                    {
                        phone = selectedCustomers.ToList(),
                        message = pushOffer.Message
                    }
                };
                var messageBody = JsonConvert.SerializeObject(pushOffers);
                var uri = string.Format(CommonSettings.NewPushOfferPostUrl, pushOffer.TemplateId);
                ServiceLocator.Current.GetService<ILogService>().Info(nameof(PushOffersService), uri);
                var response=await ServiceLocator.Current.GetService<IGenericHttpService>().PostJsonWithHeaders(uri, messageBody);
                #endregion
                ServiceLocator.Current.GetService<ILogService>().Info(nameof(PushOffersService), response);

                var pushOfferResponse = JsonConvert.DeserializeObject<PushOfferResponse>(response);
                if (pushOfferResponse.Status == 400)
                {
                    await ServiceLocator.Current.GetService<IToastMessageHelper>().ShowToast(pushOfferResponse.Message, AlertType.Error);
                }
                return pushOfferResponse.Status == 201;
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(PushOffersDataService), ex);
                return false;
            }
        }

        

        public async Task<List<CustomerInfoSelector>> FetchTopCustomersByPurchaseValue(int take)
        {
            return await dataService.FetchTopCustomersByPurchaseValue(take);
        }

        public async Task<bool> SavePushOfferAsync(PushOfferInfo pushOffer)
        {
            return await dataService.SavePushOfferAsync(pushOffer);
        }

        public async Task<IEnumerable<PushOfferInfo>> FetchPreviousPushOffers(long fromDate, long toDate, int pageSize, int skip)
        {
            return await dataService.FetchPreviousPushOffers(fromDate, toDate, pageSize, skip);
        }

        public async Task<List<long>> GetAllCustomersPhones()
        {
            return await dataService.GetAllCustomersPhones();
        }

        public Task<List<ChartSummaryInfo>> GetCustomersVisitWise(int take)
        {
            throw new NotImplementedException();
        }

        public Task<List<ChartSummaryInfo>> GetRegularCustomers()
        {
            throw new NotImplementedException();
        }

        public Task<List<ChartSummaryInfo>> GetIrregularCustomers()
        {
            throw new NotImplementedException();
        }

        public Task<List<ChartSummaryInfo>> GetCustomersAmountSpentWise(int take)
        {
            throw new NotImplementedException();
        }

        public Task<List<CustomerInfo>> GetCustomersWalletWise(int take)
        {
            throw new NotImplementedException();
        }

        public Task<List<ProductInfo>> GetMostSoldProductsByQuantity(int take)
        {
            throw new NotImplementedException();
        }

        public Task<List<ProductInfo>> GetMostSoldProductsByValue(int take)
        {
            throw new NotImplementedException();
        }

        public Task<List<ProductInfo>> GetSlowMovingStockProducts(int take)
        {
            throw new NotImplementedException();
        }

        public Task<List<ProductInfo>> GetSimilarCategoryProducts(ProductInfo product)
        {
            throw new NotImplementedException();
        }

        public async Task<PushOfferSmsCounter> GetPushOfferSmsCounter()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var cache = PushOfferHeadersCache.Instance.GetCache();
                    foreach (var item in cache)
                    {
                        client.DefaultRequestHeaders.Add(item.Key, item.Value);
                    }
                    var mt = new MediaTypeWithQualityHeaderValue("application/json");
                    client.DefaultRequestHeaders.Accept.Add(mt);
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(CommonSettings.GetLiveSmsCountUrl)
                    };
                    ServiceLocator.Current.GetService<ILogService>().Info(nameof(PushOffersDataService), request.RequestUri.ToString());

                    var response = await client.SendAsync(request);
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            using (var streamReader = new StreamReader(stream))
                            {
                                using (var jsonTextReader = new JsonTextReader(streamReader))
                                {
                                    var res = new JsonSerializer().Deserialize<Response>(jsonTextReader);
                                    throw new Exception($"Unauthorized request: {res.message} with sub code: {res.subCode}.");
                                }
                            }
                        }
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            using (var streamReader = new StreamReader(stream))
                            {
                                using (var jsonTextReader = new JsonTextReader(streamReader))
                                {
                                    var res = new JsonSerializer().Deserialize<Response>(jsonTextReader);
                                    var data = res.data.FirstOrDefault();
                                    return new PushOfferSmsCounter
                                    {

                                        AlreadySentCount = data.sms_sent,
                                        RemainingCount = data.live_sms_count,
                                        ExpiryDate = data.expires_at.ToLongDateString()
                                    };
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<ILogService>().Error(nameof(PushOffersDataService), ex);
            }
            return new PushOfferSmsCounter();
        }
    }
}