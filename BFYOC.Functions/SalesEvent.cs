using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFYOC.Functions
{
    public class SalesEvent
    {
        public DateTime dateTime { get; set; }
        public List<SaleEventDetail> details { get; set; }
        public string locationAddress { get; set; }
        public string locationId { get; set; }
        public string locationName { get; set; }
        public string locationPostcode { get; set; }
        public Guid salesNumber { get; set; }
        public double totalCost { get; set; }
        public double totalTax { get; set; }

        public class SaleEventDetail
        {
            public string productDescription { get; set; }
            public Guid productId { get; set; }
            public string productName { get; set; }
            public int quantity { get; set; }
            public double totalCost { get; set; }
            public double totalTax { get; set; }
            public double unitCost { get; set; }
        }
    }
}