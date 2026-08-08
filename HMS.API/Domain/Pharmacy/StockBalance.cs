using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    // keyless view/entity representing computed stock balances
    public class StockBalance
    {
        public Guid ItemId { get; set; }
        public Guid? StoreId { get; set; }
        public Guid? BatchId { get; set; }
        public int AvailableQty { get; set; }
    }
}
