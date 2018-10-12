// This is the default URL for triggering event grid function in the local environment.
// http://localhost:7071/admin/extensions/EventGridExtensionConfig?functionName={functionname}

using CsvHelper;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BFYOC.Functions
{
    public static class OrderProcessing
    {
        private static readonly CloudStorageAccount _functionStorageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        private static readonly CloudStorageAccount _orderStorageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("OrdersStorageConnection"));

        public static async Task<CsvReader> GetCsvReader(this Task<ICloudBlob> blob)
        {
            await blob;
            Stream stream = await blob.Result.OpenReadAsync();
            StreamReader sr = new StreamReader(stream);
            return new CsvReader(sr);
        }

        public static async Task<bool> IsLeader(string instanceId)
        {
            var client = _functionStorageAccount.CreateCloudBlobClient();
            var containerRef = client.GetContainerReference("lease");
            await containerRef.CreateIfNotExistsAsync();
            var blobRef = containerRef.GetBlockBlobReference(instanceId);
            if (!(await blobRef.ExistsAsync()))
            {
                blobRef.UploadText("Empty");
            }

            try
            {
                var result = await blobRef.AcquireLeaseAsync(TimeSpan.FromSeconds(30));
                return true;
            }
            catch
            {
                return false;
            }
        }

        [FunctionName("OnNewFile")]
        public static async Task OnNewFile([EventGridTrigger]JObject eventGridEvent,
            [OrchestrationClient] DurableOrchestrationClient starter,
            TraceWriter log)
        {
            log.Info(eventGridEvent.ToString());
            var blobUrl = (string)eventGridEvent["data"]["url"];
            await ProcessBlob(blobUrl, starter, log);
        }

        [FunctionName("OrderOrchestrator")]
        public static async Task OrderOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context, TraceWriter log)
        {
            log.Info($"Starting to wait InstanceId:{context.InstanceId}, Input:{context.GetInput<string>()}");

            var orderHeaderDetailsEvent = context.WaitForExternalEvent<string>("OrderHeaderDetails");
            var orderLineItemsEvent = context.WaitForExternalEvent<string>("OrderLineItems");
            var productInformationEvent = context.WaitForExternalEvent<string>("ProductInformation");

            await Task.WhenAll(orderHeaderDetailsEvent, orderLineItemsEvent, productInformationEvent);

            var orderFiles = new OrderFiles
            {
                OrderHeaderDetails = orderHeaderDetailsEvent.Result,
                OrderLineItems = orderLineItemsEvent.Result,
                ProductInformation = productInformationEvent.Result
            };

            log.Info($"Received header:{orderFiles.OrderHeaderDetails}, line:{orderFiles.OrderLineItems}, product:{orderFiles.ProductInformation}");

            var orders = await context.CallActivityAsync<IEnumerable<Order>>("OrderProcessor", orderFiles);

            await context.CallActivityAsync("SaveOrders", orders);

            log.Info("Work done!");
        }

        [FunctionName("OrderProcessor")]
        public static async Task<IEnumerable<Order>> OrderProcessor([ActivityTrigger]OrderFiles orderFiles, TraceWriter log)
        {
            var client = _orderStorageAccount.CreateCloudBlobClient();
            using (var orderHeaderReader = await client.GetBlobReferenceFromServerAsync(new Uri(orderFiles.OrderHeaderDetails)).GetCsvReader())
            using (var orderLineItemsReader = await client.GetBlobReferenceFromServerAsync(new Uri(orderFiles.OrderLineItems)).GetCsvReader())
            using (var productInformationReader = await client.GetBlobReferenceFromServerAsync(new Uri(orderFiles.ProductInformation)).GetCsvReader())
            {
                var orders = orderHeaderReader.GetRecords<dynamic>().ToDictionary(r => (string)r.ponumber, r => new Order
                {
                    dateTime = r.datetime,
                    locationId = r.locationid,
                    locationName = r.locationname,
                    locationPostCode = r.locationpostcode,
                    poNumber = r.ponumber,
                    totalCost = double.Parse(r.totalcost),
                    totalTax = double.Parse(r.totaltax),
                    lineItems = new List<OrderLineItem>(),
                    id = r.ponumber
                });

                var products = productInformationReader.GetRecords<dynamic>().ToDictionary(r => (string)r.productid, r => r);

                foreach (var lineItem in orderLineItemsReader.GetRecords<dynamic>())
                {
                    var product = products[lineItem.productid];
                    orders[(string)lineItem.ponumber].lineItems.Add(new OrderLineItem
                    {
                        productId = lineItem.productid,
                        quantity = int.Parse(lineItem.quantity),
                        unitCost = double.Parse(lineItem.unitcost),
                        totalCost = double.Parse(lineItem.totalcost),
                        totalTax = double.Parse(lineItem.totaltax),
                        productDescription = product.productdescription,
                        productName = product.productname
                    });
                }

                return orders.Values;
            }
        }

        [FunctionName("ProcessingExistingBlobs")]
        public static async Task<HttpResponseMessage> ProcessingExistingBlobs(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage req,
            [OrchestrationClient] DurableOrchestrationClient starter,
            TraceWriter log)
        {
            var client = _orderStorageAccount.CreateCloudBlobClient();
            var containerRef = client.GetContainerReference("orders");
            foreach (var blob in containerRef.ListBlobs())
            {
                log.Info($"Processing {blob.Uri}");
                await ProcessBlob(blob.Uri.ToString(), starter, log);
            }

            return req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        }

        [FunctionName("SaveOrders")]
        public static async Task SaveOrders(
            [ActivityTrigger]IEnumerable<Order> orders,
            [DocumentDB(databaseName: "byoc", collectionName: "orders", ConnectionStringSetting = "CosmosDBConnection")] IAsyncCollector<Order> ordersOut,
            TraceWriter log)
        {
            log.Info("Saving documents");
            await Task.WhenAll(orders.Select(o => ordersOut.AddAsync(o)));
        }

        private static async Task ProcessBlob(string blobUrl, DurableOrchestrationClient starter, TraceWriter log)
        {
            var blobName = blobUrl.Substring(blobUrl.LastIndexOf('/') + 1);
            var orderId = blobName.Substring(0, 14);
            var fileType = blobName.Substring(blobName.IndexOf('-') + 1, blobName.Length - blobName.IndexOf('-') - 5);

            var status = await starter.GetStatusAsync(orderId);
            if (status == null)
            {
                if (await IsLeader(orderId))
                {
                    log.Info($"Starting orchestrator for {orderId}");
                    await starter.StartNewAsync("OrderOrchestrator", instanceId: orderId, input: orderId);
                }
                else
                {
                    log.Info("I am not the leader");
                }
            }
            else
            {
                log.Info("I am not the leader");
            }

            status = await starter.GetStatusAsync(orderId);
            log.Info($"status is {status.RuntimeStatus}");
            log.Info($"Raising event {fileType} with value {blobUrl} to instance {orderId}");
            await starter.RaiseEventAsync(orderId, fileType, blobUrl);
        }
    }
}