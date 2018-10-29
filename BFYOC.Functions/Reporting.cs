using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.WindowsAzure.Storage;

namespace BFYOC.Functions
{
    public static class Reporting
    {
        private static readonly Dictionary<string, string> mapping;
        private static readonly CloudStorageAccount ReportStorageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("ReportsStorageConnection"));

        static Reporting()
        {
            mapping = new Dictionary<string, string>
            {
                {"75542e38-563f-436f-adeb-f426f1dabb5c","Starfruit Explosion" },
                {"e94d85bc-7bd0-44f3-854e-d8cd70348b63","Just Peachy" },
                {"288fd748-ad2b-4417-83b9-7aa5be9cff22","Tropical Mango" },
                {"76065ecd-8a14-426d-a4cd-abbde2acbb10","Gone Bananas" },
                {"551a9be9-7f1c-447d-83ee-b18f5a6fb018","Matcha Green Tea" },
                {"80bab959-ef8b-4ae3-8bf2-e876d77277b6","French Vanilla" },
                {"4c25613a-a3c2-4ef3-8e02-9c335eb23204","Truly Orange-inal" },
                {"65ab124a-9b2c-4294-a52d-18839364ef15","Durian Durian" },
                {"e4e7068e-500e-4a00-8be4-630d4594735b","It's Grape!" },
                {"0f5a0fe8-4506-4332-969e-699a693334a8","Beer" },
            };
        }

        [FunctionName("ReportGenerator")]
        public static async Task ReportGenerator([CosmosDBTrigger(
            databaseName: "byoc",
            collectionName: "salesevents",
            ConnectionStringSetting = "CosmosDBConnection",
            LeaseCollectionName = "leases",
            CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> documents,
            [EventHub("changeeventsales", Connection = "EventHubConnection")]
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
            [EventHub("changeeventorders", Connection = "EventHubConnection")]
            IAsyncCollector<Document> outputs,
            TraceWriter log)
        {
            log.Info($"Processing {documents.Count} documents");
            foreach (var document in documents)
            {
                await outputs.AddAsync(document);
            }
        }

        [FunctionName("ReportingRatings")]
        public static async Task ReportingRatings([CosmosDBTrigger(
            databaseName: "byoc",
            collectionName: "ratings",
            ConnectionStringSetting = "CosmosDBConnection",
            LeaseCollectionName = "leases",
            CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> documents,
            [EventHub("changeeventratings", Connection = "EventHubConnection")]IAsyncCollector<Document> outputs,
            TraceWriter log)
        {
            log.Info($"Processing {documents.Count} documents");
            foreach (var document in documents)
            {
                await outputs.AddAsync(document);
            }
        }

        [FunctionName("ReportingSales")]
        public static async Task ReportingSales(
            [EventHubTrigger("reports", Connection = "EventHubConnection", ConsumerGroup = "salesreport")]string reportDataEvent,
            TraceWriter log)
        {
            var client = ReportStorageAccount.CreateCloudBlobClient();
            var containerRef = client.GetContainerReference("reports");
            using (var csvReader = new CsvReader(new StringReader(reportDataEvent)))
            {
                var reportDataByTime = csvReader.GetRecords<dynamic>()
                    .GroupBy(r => (string)r.time, r => new
                    {
                        Time = r.time,
                        Score = double.Parse(r.score),
                        TotalSales = double.Parse(r.totalcostsales),
                        TotalOrders = double.Parse(r.totalcostorders),
                        ProductId = r.productid
                    });

                foreach (var reportData in reportDataByTime)
                {
                    var reportSS = new StringWriter();
                    reportSS.WriteLine($"Sales report for {reportData.Key}");

                    reportSS.WriteLine();
                    reportSS.WriteLine($"Sentiment score by ice cream");
                    foreach (var orderedReportData in reportData.Where(r => r.Score >= 0).OrderByDescending(r => r.Score))
                    {
                        reportSS.WriteLine($"{orderedReportData.Score:F3} - {ProductIdToProductName(orderedReportData.ProductId)}");
                    }

                    reportSS.WriteLine();
                    reportSS.WriteLine($"Total orders in $ by ice cream");
                    foreach (var orderedReportData in reportData.OrderByDescending(r => r.TotalOrders))
                    {
                        reportSS.WriteLine($"{orderedReportData.TotalOrders:C} - {ProductIdToProductName(orderedReportData.ProductId)}");
                    }

                    reportSS.WriteLine();
                    reportSS.WriteLine($"Total sales events in $ by ice cream");
                    foreach (var orderedReportData in reportData.OrderByDescending(r => r.TotalSales))
                    {
                        reportSS.WriteLine($"{orderedReportData.TotalSales:C} - {ProductIdToProductName(orderedReportData.ProductId)}");
                    }

                    var blob = containerRef.GetBlockBlobReference(DateTime.Parse(reportData.Key).ToString("yyyyMMdd_HHmmss") + ".txt");
                    await blob.UploadTextAsync(reportSS.ToString());
                }
            }
        }

        private static string ProductIdToProductName(string productId)
        {
            return mapping[productId];
        }
    }
}