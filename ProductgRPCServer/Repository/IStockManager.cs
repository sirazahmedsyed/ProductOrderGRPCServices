namespace ProductgRPCServer.Repository
{
    public interface IStockManager
    {
        // Core stock management
        Task<StockUpdateResult> UpdateStockAsync(string productId, int quantityChange, string transactionId);
        Task<int> GetCurrentStockAsync(string productId);

        // Stock reservation
        Task<StockUpdateResult> ReserveStockAsync(string productId, int quantity, string transactionId);
        Task<StockUpdateResult> CommitReservationAsync(string transactionId);
        Task<StockUpdateResult> CancelReservationAsync(string transactionId);

        // Stock monitoring
        IAsyncEnumerable<StockUpdateResult> SubscribeToStockUpdates(IEnumerable<string> productIds);
        Task<bool> IsStockAvailableAsync(string productId, int requiredQuantity);

        // Stock alerts
        Task SetStockAlertThresholdAsync(string productId, int minimumLevel);
        Task<IEnumerable<string>> GetLowStockProductsAsync();
    }
}
