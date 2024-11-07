namespace ProductgRPCServer.Repository
{
    public interface IProductRepository
    {
        Task<int> GetStockLevelAsync(string productId);
        Task<int> UpdateStockLevelAsync(string productId, int quantityChange, string transactionId);
        Task<Product> GetProductAsync(string productId);
        Task<bool> ProductExistsAsync(string productId);
    }
}
