// This is the default URL for triggering event grid function in the local environment.
// http://localhost:7071/admin/extensions/EventGridExtensionConfig?functionName={functionname} 

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BFYOC.Functions
{
  public static class OrderProcessing
  {
    [FunctionName("OnNewFile")]
    public static void OnNewFile([EventGridTrigger]JObject eventGridEvent, TraceWriter log)
    {
      log.Info("Woohoo!" + eventGridEvent.ToString(Formatting.Indented));
    }

    public static void OrderProcessor()
    {

    }

    public static void SaveOrder()
    {
    }
  }
}
