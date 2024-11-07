using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Linq;

namespace ProductgRPCServer.Repository
{
    public class StockManager : IStockManager
    {
        private readonly IProductRepository _productRepository;
        private readonly ILogger<StockManager> _logger;
        private readonly IMemoryCache _cache;
        private readonly Channel<StockUpdateResult> _stockUpdatesChannel;
        private readonly ConcurrentDictionary<string, int> _stockAlertThresholds;
        private readonly ConcurrentDictionary<string, List<StockReservation>> _stockReservations;

        public StockManager(IProductRepository productRepository, ILogger<StockManager> logger, IMemoryCache cache)
        {
            _productRepository = productRepository;
            _logger = logger;
            _cache = cache;
            _stockUpdatesChannel = Channel.CreateUnbounded<StockUpdateResult>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = true
            });
            _stockAlertThresholds = new ConcurrentDictionary<string, int>();
            _stockReservations = new ConcurrentDictionary<string, List<StockReservation>>();
        }

        public async Task<StockUpdateResult> UpdateStockAsync(string productId, int quantityChange, string transactionId)
        {
            try
            {
                // Acquire a lock for the specific product
                using var lockToken = await AcquireStockLockAsync(productId);

                // Get current stock level
                var currentStock = await GetCurrentStockAsync(productId);

                if (currentStock + quantityChange < 0)
                {
                    return new StockUpdateResult
                    {
                        Success = false,
                        Message = "Insufficient stock available",
                        NewStockLevel = currentStock,
                        ProductId = productId,
                        Timestamp = DateTime.UtcNow
                    };
                }

                var newStockLevel = await _productRepository.UpdateStockLevelAsync(productId, quantityChange, transactionId);

                var result = new StockUpdateResult
                {
                    Success = true,
                    Message = "Stock updated successfully",
                    NewStockLevel = newStockLevel,
                    ProductId = productId,
                    Timestamp = DateTime.UtcNow
                };

                await PublishStockUpdateAsync(result);

                // Check for low stock alert
                await CheckLowStockAlertAsync(productId, newStockLevel);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for product {ProductId}", productId);
                throw new StockManagementException("Failed to update stock", ex);
            }
        }

        public async Task<int> GetCurrentStockAsync(string productId)
        {
            return await _cache.GetOrCreateAsync(
                $"stock_{productId}",
                async entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return await _productRepository.GetStockLevelAsync(productId);
                });
        }

        public async Task<StockUpdateResult> ReserveStockAsync(
            string productId,
            int quantity,
            string transactionId)
        {
            using var lockToken = await AcquireStockLockAsync(productId);

            var currentStock = await GetCurrentStockAsync(productId);

            // Check if enough stock is available
            if (currentStock < quantity)
            {
                return new StockUpdateResult
                {
                    Success = false,
                    Message = "Insufficient stock for reservation",
                    NewStockLevel = currentStock,
                    ProductId = productId,
                    Timestamp = DateTime.UtcNow
                };
            }

            // Create reservation
            var reservation = new StockReservation
            {
                ReservationId = Guid.NewGuid().ToString(),
                ProductId = productId,
                Quantity = quantity,
                ExpiryTime = DateTime.UtcNow.AddMinutes(15),
                TransactionId = transactionId
            };

            // Add to reservations
            _stockReservations.AddOrUpdate(
                productId,
                new List<StockReservation> { reservation },
                (key, existing) =>
                {
                    existing.Add(reservation);
                    return existing;
                });

            // Update available stock
            var result = await UpdateStockAsync(productId, -quantity, transactionId);

            return result;
        }

        public async Task<StockUpdateResult> CommitReservationAsync(string transactionId)
        {
            var reservations = _stockReservations.Values
                .SelectMany(x => x)
                .Where(r => r.TransactionId == transactionId)
                .ToList();

            if (!reservations.Any())
            {
                throw new StockManagementException("No reservation found for transaction");
            }

            foreach (var reservation in reservations)
            {
                _stockReservations.AddOrUpdate(
                    reservation.ProductId,
                    new List<StockReservation>(),
                    (key, existing) =>
                    {
                        existing.RemoveAll(r => r.TransactionId == transactionId);
                        return existing;
                    });
            }

            return new StockUpdateResult
            {
                Success = true,
                Message = "Reservation committed successfully",
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<StockUpdateResult> CancelReservationAsync(string transactionId)
        {
            var reservations = _stockReservations.Values
                .SelectMany(x => x)
                .Where(r => r.TransactionId == transactionId)
                .ToList();

            foreach (var reservation in reservations)
            {
                // Return stock
                await UpdateStockAsync(
                    reservation.ProductId,
                    reservation.Quantity,
                    $"cancel_{transactionId}");

                // Remove reservation
                _stockReservations.AddOrUpdate(
                    reservation.ProductId,
                    new List<StockReservation>(),
                    (key, existing) =>
                    {
                        existing.RemoveAll(r => r.TransactionId == transactionId);
                        return existing;
                    });
            }

            return new StockUpdateResult
            {
                Success = true,
                Message = "Reservation cancelled successfully",
                Timestamp = DateTime.UtcNow
            };
        }

        public IAsyncEnumerable<StockUpdateResult> SubscribeToStockUpdates(IEnumerable<string> productIds)
        {
            return _stockUpdatesChannel.Reader.ReadAllAsync()
                .Where(update => productIds.Contains(update.ProductId));
        }

        public async Task<bool> IsStockAvailableAsync(string productId, int requiredQuantity)
        {
            var currentStock = await GetCurrentStockAsync(productId);
            var reservedStock = GetReservedStock(productId);
            return (currentStock - reservedStock) >= requiredQuantity;
        }

        public Task SetStockAlertThresholdAsync(string productId, int minimumLevel)
        {
            _stockAlertThresholds.AddOrUpdate(productId, minimumLevel, (key, old) => minimumLevel);
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<string>> GetLowStockProductsAsync()
        {
            var lowStockProducts = new List<string>();

            foreach (var threshold in _stockAlertThresholds)
            {
                var currentStock = await GetCurrentStockAsync(threshold.Key);
                if (currentStock <= threshold.Value)
                {
                    lowStockProducts.Add(threshold.Key);
                }
            }

            return lowStockProducts;
        }

        private async Task<IDisposable> AcquireStockLockAsync(string productId)
        {
            var lockKey = $"stock_lock_{productId}";
            var lockTimeout = TimeSpan.FromSeconds(30);
            var lockToken = await AsyncLock.AcquireLockAsync(lockKey, lockTimeout);

            if (lockToken == null)
            {
                throw new StockManagementException("Failed to acquire stock lock");
            }

            return lockToken;
        }

        private async Task PublishStockUpdateAsync(StockUpdateResult update)
        {
            try
            {
                await _stockUpdatesChannel.Writer.WriteAsync(update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing stock update");
            }
        }

        private async Task CheckLowStockAlertAsync(string productId, int newStockLevel)
        {
            if (_stockAlertThresholds.TryGetValue(productId, out var threshold))
            {
                if (newStockLevel <= threshold)
                {
                    _logger.LogWarning(
                        "Low stock alert for product {ProductId}. Current level: {StockLevel}, Threshold: {Threshold}",
                        productId, newStockLevel, threshold);
                }
            }
        }

        private int GetReservedStock(string productId)
        {
            if (_stockReservations.TryGetValue(productId, out var reservations))
            {
                // Clean expired reservations
                reservations.RemoveAll(r => r.ExpiryTime < DateTime.UtcNow);

                return reservations.Sum(r => r.Quantity);
            }
            return 0;
        }
    }
}
