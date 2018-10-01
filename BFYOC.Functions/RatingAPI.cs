using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace BFYOC.Functions
{
  public static class RatingAPI
  {
    [FunctionName("CreateRating")]
    public static async Task<HttpResponseMessage> CreateRating(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ratings")] HttpRequestMessage req,
      [DocumentDB(databaseName: "byoc", collectionName: "ratings", ConnectionStringSetting = "CosmosDBConnection")] IAsyncCollector<Rating> ratingsOut,
      TraceWriter log)
    {
      log.Info("C# HTTP trigger function processed a request.");

      var client = new HttpClient();

      // parse query parameter
      string name = req.GetQueryNameValuePairs()
          .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
          .Value;



      string requestBody = await req.Content.ReadAsStringAsync();
      var rating = JsonConvert.DeserializeObject<Rating>(requestBody);

      var validProduct = ValidProduct(client, rating.productId);
      if (!validProduct)
      {
        return req.CreateResponse(HttpStatusCode.BadRequest, "Bad product");
      }

      var validUser = ValidUser(client, rating.userId);
      if (!validUser)
      {
        return req.CreateResponse(HttpStatusCode.BadRequest, "Bad user");
      }


      rating.id = Guid.NewGuid().ToString();
      rating.timestamp = DateTime.UtcNow.ToString();
      await ratingsOut.AddAsync(rating);

      return req.CreateResponse(HttpStatusCode.OK, "Rating successfully submitted");
    }

    [FunctionName("GetRating")]
    public static HttpResponseMessage GetRating(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ratings/{id}")]HttpRequestMessage req,
      [DocumentDB(databaseName: "byoc", collectionName: "ratings", ConnectionStringSetting = "CosmosDBConnection", Id = "{id}")] Rating rating,
      TraceWriter log)
    {
      log.Info("C# HTTP trigger function processed a request.");

      return rating == null
          ? req.CreateResponse(HttpStatusCode.NotFound, "Rating not found", "application/json")
          : req.CreateResponse(HttpStatusCode.OK, rating, "application/json");
    }
    [FunctionName("GetRatings")]
    public static HttpResponseMessage GetRatings(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ratings")]HttpRequestMessage req,
      [DocumentDB(databaseName: "byoc", collectionName: "ratings", ConnectionStringSetting = "CosmosDBConnection", SqlQuery = "SELECT * FROM c")] IEnumerable<Rating> ratings,
      TraceWriter log)
    {
      log.Info("C# HTTP trigger function processed a request.");

      return req.CreateResponse(HttpStatusCode.OK, ratings, "application/json");
    }

    private static bool ValidUser(HttpClient client, string userId)
    {
      client.DefaultRequestHeaders.Accept.Clear();
      client.DefaultRequestHeaders
            .Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));

      var result = client.GetAsync($"{Settings.GetUserUrl}?userId={userId}").Result;
      return result.StatusCode == HttpStatusCode.OK;
    }

    private static bool ValidProduct(HttpClient client, string productId)
    {
      client.DefaultRequestHeaders.Accept.Clear();
      client.DefaultRequestHeaders
            .Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));

      var result = client.GetAsync($"{Settings.GetProductUrl}?productId={productId}").Result;
      return result.StatusCode == HttpStatusCode.OK;
    }
  }
}
