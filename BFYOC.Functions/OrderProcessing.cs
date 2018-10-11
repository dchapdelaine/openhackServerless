// This is the default URL for triggering event grid function in the local environment.
// http://localhost:7071/admin/extensions/EventGridExtensionConfig?functionName={functionname}

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using System;
using System.IO;
using Microsoft.WindowsAzure.Storage.Blob;
using CsvHelper;

namespace BFYOC.Functions
{
    public static class OrderProcessing
    {
        private static readonly CloudStorageAccount _functionStorageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        private static readonly CloudStorageAccount _orderStorageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("OrdersStorageConnection"));

        public static async Task<CsvReader> GetCsvReader(this ICloudBlob blob)
        {
            using (Stream stream = await blob.OpenReadAsync())
            using (StreamReader sr = new StreamReader(stream))
            {
                var csvReader = new CsvReader(sr);
                csvReader.Read();
                return csvReader;
            }
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
                var result = await blobRef.AcquireLeaseAsync(TimeSpan.FromMinutes(60));
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
            var blobName = blobUrl.Substring(blobUrl.LastIndexOf('/') + 1);

            var orderId = blobName.Substring(0, 14);
            var fileType = blobName.Substring(blobName.IndexOf('-') + 1, blobName.Length - blobName.IndexOf('-') - 4);

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

            log.Info($"Raising event {fileType} with value {blobUrl}");
            await starter.RaiseEventAsync(orderId, fileType, blobUrl);
        }

        [FunctionName("OrderOrchestrator")]
        public static async Task OrderOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context, TraceWriter log)
        {
            log.Info($"Starting to wait Parent:{context.ParentInstanceId}, InstanceId:{context.InstanceId}, Input:{context.GetInput<string>()}");

            var orderHeaderDetailsEvent = context.WaitForExternalEvent<string>("OrderHeaderDetails");
            var orderLineItemsEvent = context.WaitForExternalEvent<string>("OrderLineItems");
            var productInformationEvent = context.WaitForExternalEvent<string>("ProductInformation");

            await Task.WhenAny(orderHeaderDetailsEvent, orderLineItemsEvent, productInformationEvent);

            var orderFiles = new OrderFiles
            {
                OrderHeaderDetails = orderHeaderDetailsEvent.Result,
                OrderLineItems = orderLineItemsEvent.Result,
                ProductInformation = productInformationEvent.Result
            };

            log.Info($"Received header:{orderFiles.OrderHeaderDetails}, line:{orderFiles.OrderLineItems}, product:{orderFiles.ProductInformation}");

            var order = await context.CallActivityAsync<Order>("OrderProcessor", orderFiles);
        }

        [FunctionName("OrderProcessor")]
        public static async Task<Order> OrderProcessor([ActivityTrigger]OrderFiles orderFiles, TraceWriter log)
        {
            var client = _orderStorageAccount.CreateCloudBlobClient();
            var OrderHeaderReader = (await client.GetBlobReferenceFromServerAsync(new Uri(orderFiles.OrderHeaderDetails))).GetCsvReader();
            var OrderLineItemsReader = (await client.GetBlobReferenceFromServerAsync(new Uri(orderFiles.OrderLineItems))).GetCsvReader();
            var ProductInformationReader = (await client.GetBlobReferenceFromServerAsync(new Uri(orderFiles.ProductInformation))).GetCsvReader();

            return new Order();
        }

        [NoAutomaticTrigger]
        public static void ProcessingExistingBlogs(TraceWriter log)
        {
            log.Info($"This is a manually triggered C# function with input");
        }

        public static void SaveOrder()
        {
        }
    }
}