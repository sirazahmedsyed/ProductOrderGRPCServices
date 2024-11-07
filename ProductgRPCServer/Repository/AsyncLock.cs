using System.Collections.Concurrent;

namespace ProductgRPCServer.Repository
{
    public class AsyncLock
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
            new ConcurrentDictionary<string, SemaphoreSlim>();

        public static async Task<IDisposable> AcquireLockAsync(
            string key,
            TimeSpan timeout)
        {
            var semaphore = _locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));

            if (await semaphore.WaitAsync(timeout))
            {
                return new AsyncLockReleaser(semaphore);
            }

            return null;
        }

        private class AsyncLockReleaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _disposed;

            public AsyncLockReleaser(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _semaphore.Release();
                    _disposed = true;
                }
            }
        }
    }
}
