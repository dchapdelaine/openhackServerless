using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace BFYOC.Functions
{
    public static class Reporting
    {
        [FunctionName("ReportingRatings")]
        public static async Task ReportingRatings([CosmosDBTrigger(
            databaseName: "byoc",
            collectionName: "ratings",
            ConnectionStringSetting = "CosmosDBConnection",
            LeaseCollectionName = "leases",
            CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> documents,
            [EventHub("events", Connection = "EventHubConnection")]IAsyncCollector<Document> outputs,
            TraceWriter log)
        {
            log.Info($"Processing {documents.Count} documents");
            foreach(var document in documents)
            {
                await outputs.AddAsync(document);
            }
        }

        [FunctionName("ReportingSales")]
        public static async Task ReportingSales([CosmosDBTrigger(
            databaseName: "byoc",
            collectionName: "salesevents",
            ConnectionStringSetting = "CosmosDBConnection",
            LeaseCollectionName = "leases",
            CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> documents,
            [EventHub("events", Connection = "EventHubConnection")]
            IAsyncCollector<Document> outputs,
            TraceWriter log)
        {
            log.Info($"Processing {documents.Count} documents");
            foreach (var document in documents)
            {
                await outputs.AddAsync(document);
            }
        }

        [FunctionName("ReportingOrders")]
        public static async Task ReportingOrders([CosmosDBTrigger(
            databaseName: "byoc",
            collectionName: "orders",
            ConnectionStringSetting = "CosmosDBConnection",
            LeaseCollectionName = "leases",
            CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> documents,
            [EventHub("events", Connection = "EventHubConnection")]
            IAsyncCollector<Document> outputs,
            TraceWriter log)
        {
            log.Info($"Processing {documents.Count} documents");
            foreach (var document in documents)
            {
                await outputs.AddAsync(document);
            }
        }
    }
}
