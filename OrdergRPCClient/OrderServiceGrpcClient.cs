using Grpc.Core;
using Microsoft.AspNetCore.Http;
using ProductgRPCServer;
using ProductgRPCServer.Repository;
using System.Runtime.CompilerServices;

namespace OrdergRPCClient
{
    public class OrderServiceGrpcClient
    {
        private readonly ProductGrpc.ProductGrpcClient _productClient;
        private readonly ILogger<OrderServiceGrpcClient> _logger;

        public OrderServiceGrpcClient(ProductGrpc.ProductGrpcClient productClient,ILogger<OrderServiceGrpcClient> logger)
        {
            _productClient = productClient;
            _logger = logger;
        }

        public async Task<Product> GetProductDetailsAsync(string productId)
        {
            try
            {
                var response = await _productClient.GetProductAsync(
                    new ProductRequest { ProductId = productId });

                return new Product
                {
                    Product_Id = response.ProductId,
                    Name = response.Name,
                    Price = response.Price,
                    Stock = response.CurrentStock
                };
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                throw new InvalidOperationException($"Product with ID '{productId}' was not found.");
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error getting product details");
                throw new InvalidOperationException("Error getting product details", ex);
            }
        }

        public async Task<bool> UpdateStockAsync(string productId, int quantity, string transactionId)
        {
            try
            {
                var response = await _productClient.UpdateStockAsync(
                    new StockUpdateRequest
                    {
                        ProductId = productId,
                        QuantityChange = quantity,
                        TransactionId = transactionId
                    });

                return response.Success;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error updating stock");
                throw new InvalidOperationException("Error in stock updates subscription", ex);
            }
        }

        public IAsyncEnumerable<StockUpdate> SubscribeToStockUpdatesAsync(
            IEnumerable<string> productIds,
            CancellationToken cancellationToken = default)
        {
            var request = new StockSubscriptionRequest();
            request.ProductIds.AddRange(productIds);

            var call = _productClient.SubscribeToStockChanges(request);

            return ProcessStockUpdates(call.ResponseStream, cancellationToken);
        }

        private async IAsyncEnumerable<StockUpdate> ProcessStockUpdates(
            IAsyncStreamReader<StockUpdateResponse> stream,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
                await foreach (var update in stream.ReadAllAsync(cancellationToken))
                {
                    yield return new StockUpdate
                    {
                        Success = update.Success,
                        Message = update.Message,
                        NewStockLevel = update.NewStockLevel
                    };
                }
        }
    }
}
