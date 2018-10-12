using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFYOC.Functions
{
    public class Order
    {
        public string dateTime { get; set; }
        public string id { get; set; }
        public List<OrderLineItem> lineItems { get; set; }
        public string locationId { get; set; }
        public string locationName { get; set; }
        public string locationPostCode { get; set; }
        public string poNumber { get; set; }
        public double totalCost { get; set; }
        public double totalTax { get; set; }
    }

    public class OrderLineItem
    {
        public string productDescription { get; set; }
        public string productId { get; set; }
        public string productName { get; set; }
        public int quantity { get; set; }
        public double totalCost { get; set; }
        public double totalTax { get; set; }
        public double unitCost { get; set; }
    }
}