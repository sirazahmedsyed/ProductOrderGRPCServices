using Grpc.Core;
using ProductgRPCServer;
using ProductgRPCServer.Repository;

namespace ProductgRPCServer.Services
{
    public class ProductGrpcService : ProductGrpc.ProductGrpcBase
    {
        private readonly IProductRepository _repository;
        private readonly ILogger<ProductGrpcService> _logger;
        private readonly IStockManager _stockManager;

        public ProductGrpcService(IProductRepository repository, ILogger<ProductGrpcService> logger, IStockManager stockManager)
        {
            _repository = repository;
            _logger = logger;
            _stockManager = stockManager;
        }

        public override async Task<ProductResponse> GetProduct(ProductRequest request, ServerCallContext context)
        {
            var product = await _repository.GetProductAsync(request.ProductId);

            if (product == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Product not found"));

            return new ProductResponse
            {
                ProductId = product.Product_Id,
                Name = product.Name,
                Price = product.Price,
                CurrentStock = product.Stock
            };
        }

        public override async Task<StockUpdateResponse> UpdateStock(StockUpdateRequest request, ServerCallContext context)
        {
            try
            {
                var result = await _stockManager.UpdateStockAsync(
                    request.ProductId,
                    request.QuantityChange,
                    request.TransactionId);

                return new StockUpdateResponse
                {
                    Success = result.Success,
                    Message = result.Message,
                    NewStockLevel = result.NewStockLevel
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock");
                throw new RpcException(new Status(StatusCode.Internal, "Internal error updating stock"));
            }
        }

        public override async Task SubscribeToStockChanges(StockSubscriptionRequest request,
            IServerStreamWriter<StockUpdateResponse> responseStream, ServerCallContext context)
        {
            var subscription = _stockManager.SubscribeToStockUpdates(request.ProductIds);

            try
            {
                await foreach (var update in subscription.WithCancellation(context.CancellationToken))
                {
                    await responseStream.WriteAsync(new StockUpdateResponse
                    {
                        Success = true,
                        Message = "Stock updated",
                        NewStockLevel = update.NewStockLevel
                    });
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Client disconnected from stock updates stream");
            }
        }
    }
}
