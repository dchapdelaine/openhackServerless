// This is the default URL for triggering event grid function in the local environment.
// http://localhost:7071/admin/extensions/EventGridExtensionConfig?functionName={functionname}

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace BFYOC.Functions
{

    public class OrderFiles
    {
        public string OrderHeaderDetails { get; set; }
        public string OrderLineItems { get; set; }
        public string ProductInformation { get; set; }
    }
}