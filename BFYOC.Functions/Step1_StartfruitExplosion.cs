using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace BFYOC.Functions
{
  public static class Step1_StartfruitExplosion
  {
    [FunctionName("Step1_StartfruitExplosion")]
    public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequestMessage req, TraceWriter log)
    {
      log.Info("C# HTTP trigger function processed a request.");

      string productId = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "productId", true) == 0)
        .Value;

      string productName = "Unknown product";
      if (productId == "75542e38-563f-436f-adeb-f426f1dabb5c")
      {
        productName = "Startfruit Explosion";
      }

      string requestBody = await req.Content.ReadAsStringAsync();
      dynamic data = JsonConvert.DeserializeObject(requestBody);

      return req.CreateResponse(HttpStatusCode.OK, $"Your product is {productName}", "application/json");
    }
  }
}
