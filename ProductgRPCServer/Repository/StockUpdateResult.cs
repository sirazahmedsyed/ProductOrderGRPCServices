namespace ProductgRPCServer.Repository
{
    public class StockUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int NewStockLevel { get; set; }
        public string ProductId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class StockUpdate
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int NewStockLevel { get; set; }
    }

    public class StockReservation
    {
        public string ReservationId { get; set; }
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public DateTime ExpiryTime { get; set; }
        public string TransactionId { get; set; }
    }
}
