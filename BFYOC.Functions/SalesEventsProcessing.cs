using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;
using System.Linq;
using System.Threading.Tasks;

namespace BFYOC.Functions
{
    public static class SalesEventsProcessing
    {
        [FunctionName("SalesProcessing")]
        public static async Task Run(
            [EventHubTrigger("salesevents", Connection = "EventHubConnection")]string[] salesEvents,
            [DocumentDB(databaseName: "byoc", collectionName: "salesevents", ConnectionStringSetting = "CosmosDBConnection")] IAsyncCollector<string> salesEventsOut,
            TraceWriter log)
        {
            log.Info($"Processing {salesEvents.Length} events");
            var tasks = salesEvents.Select(e => salesEventsOut.AddAsync(e)).ToArray();
            await Task.WhenAll(tasks);
            log.Info($"Processing done");
        }
    }
}