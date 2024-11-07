using System.Data;
using Dapper;
using Npgsql;

namespace ProductgRPCServer.Repository
{

    public class ProductRepository : IProductRepository
    {
        private readonly NpgsqlConnection _connection; 
        public ProductRepository(NpgsqlConnection connection) 
        {
            _connection = connection; 
        }
        public async Task<int> GetStockLevelAsync(string productId)
        {
            return await _connection.QuerySingleAsync<int>($"SELECT stock FROM products WHERE product_id ='{productId}'");
        }

        public async Task<int> UpdateStockLevelAsync(string productId, int quantityChange, string transactionId)
        {
            var query = $"UPDATE products SET stock = stock + {quantityChange} WHERE product_id ='{productId}' RETURNING stock"; 
            Console.WriteLine($"Executing query: {query}");

            await _connection.OpenAsync(); 

            using var transaction = await _connection.BeginTransactionAsync();
            try
            {
                var newStockLevel = await _connection.QuerySingleAsync<int>(
                    query,
                    new { ProductId = productId, QuantityChange = quantityChange },
                    transaction);

                // Log the transaction
                //await LogStockTransactionAsync(productId, quantityChange, transactionId, transaction);

                await transaction.CommitAsync();
                return newStockLevel;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                await _connection.CloseAsync(); 
            }
        }

        public async Task<Product> GetProductAsync(string productId)
        {
            return await _connection.QuerySingleOrDefaultAsync<Product>($"SELECT product_id,name,price,stock FROM products WHERE product_id ='{productId}'");
        }

        public async Task<bool> ProductExistsAsync(string productId)
        {
            var count = await _connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM products WHERE product_id = '{productId}'");
            return count > 0;
        }

        private async Task LogStockTransactionAsync(string productId, int quantityChange, string transactionId, IDbTransaction transaction)
        {
            var query = $"INSERT INTO StockTransactions (ProductId, QuantityChange, TransactionId) VALUES ('{productId}', {quantityChange}, '{transactionId}')";


            await _connection.ExecuteAsync(
                query,
                new
                {
                    ProductId = productId,
                    QuantityChange = quantityChange,
                    TransactionId = transactionId,
                    TransactionTimestamp = DateTime.UtcNow
                },
                transaction);
        }
    }

    public class Product
    {
        public string Product_Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public int Stock { get; set; }
    }
}
