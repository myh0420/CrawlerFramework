// CrawlerCore/Utils/SimpleHttpClientManager.cs
using System;
using System.Collections.Concurrent;
using System.Net.Http;

namespace CrawlerCore.Utils
{
    /// <summary>
    /// 简化的 HttpClient 管理器
    /// </summary>
    /// <remarks>
    /// 构造函数
    /// </remarks>
    /// <param name="maxClients">最大客户端数量</param>
    /// <param name="clientFactory">自定义客户端创建工厂函数</param>
    public class SimpleHttpClientManager(int maxClients = 10, Func<HttpClient>? clientFactory = null) : IDisposable
    {
        private readonly ConcurrentQueue<HttpClient> _availableClients = [];
        private readonly ConcurrentBag<HttpClient> _allClients = [];
        private readonly int _maxClients = maxClients;
        private int _createdCount = 0;
        private bool _disposed = false;
        private readonly SemaphoreSlim _semaphore = new(1);
        private readonly Func<HttpClient> _clientFactory = clientFactory ?? CreateNewClient;
        private readonly SemaphoreSlim _clientAvailableSemaphore = new(0);
        private readonly long _createdTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        private long _totalClientRequests = 0;
        private long _waitTimeMs = 0;

        /// <summary>
        /// 获取一个HttpClient实例，如果没有可用实例且未达到最大数量，则创建新实例；否则等待可用实例。
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>管理的HttpClient实例</returns>
        public async Task<ManagedHttpClient> GetClientAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Interlocked.Increment(ref _totalClientRequests);
            
            // 尝试直接获取可用客户端
            if (_availableClients.TryDequeue(out HttpClient? client))
            {
                return new ManagedHttpClient(client, this);
            }

            var waitStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            
            // 尝试创建新客户端
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // 再次检查是否有可用客户端（可能在等待期间有客户端被释放）
                if (_availableClients.TryDequeue(out client))
                {
                    return new ManagedHttpClient(client, this);
                }

                // 如果未达到最大数量，创建新客户端
                if (_createdCount < _maxClients)
                {
                    client = _clientFactory();
                    _createdCount++;
                    _allClients.Add(client);
                    return new ManagedHttpClient(client, this);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            // 如果达到最大数量，等待可用客户端
            while (!_disposed && !cancellationToken.IsCancellationRequested)
            {
                // 使用SemaphoreSlim进行异步等待
                try
                {
                    if (await _clientAvailableSemaphore.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken))
                    {
                        if (_availableClients.TryDequeue(out client))
                        {
                            var waitEndTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                            Interlocked.Add(ref _waitTimeMs, waitEndTime - waitStartTime);
                            return new ManagedHttpClient(client, this);
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }

            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 理论上不会到达这里，但为了编译安全
            throw new InvalidOperationException("No available HttpClient instances");
        }

        /// <summary>
        /// 同步获取HttpClient实例（已过时，推荐使用异步版本）
        /// </summary>
        /// <returns>管理的HttpClient实例</returns>
        [Obsolete("Use GetClientAsync instead")]
        public ManagedHttpClient GetClient()
        {
            return GetClientAsync().GetAwaiter().GetResult();
        }

        private static HttpClient CreateNewClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
                DefaultRequestHeaders =
                {
                    { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
                    { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                    { "Accept-Language", "en-US,en;q=0.5" }
                }
            };
        }

        private void ReturnClient(HttpClient client)
        {
            if (!_disposed)
            {
                // 重置客户端状态
                client.DefaultRequestHeaders.Clear();
                
                // 重新添加基本头信息
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                
                _availableClients.Enqueue(client);
                
                // 触发信号量，通知等待的线程有客户端可用
                try
                {
                    _clientAvailableSemaphore.Release();
                }
                catch (ObjectDisposedException) { }
            }
            else
            {
                client.Dispose();
            }
        }
        ~SimpleHttpClientManager() { 
            Dispose(false);
        }
        protected virtual void Dispose(bool disposeing) {

            if (!_disposed)
            {
                if (disposeing)
                {
                    // 清理所有客户端
                    foreach (var client in _allClients)
                    {
                        client.Dispose();
                    }

                    _availableClients.Clear();
                    _allClients.Clear();
                    
                    // 释放信号量资源
                    _clientAvailableSemaphore.Dispose();
                    _semaphore.Dispose();
                }
                _disposed = true;
            }
        }
        /// <summary>
        /// 释放资源并调用GC.SuppressFinalize
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // 关键优化：调用GC.SuppressFinalize来避免不必要的终结器执行
            GC.SuppressFinalize(this);
        }

        public int AvailableCount => _availableClients.Count;
        public int TotalCount => _createdCount;

        public class ManagedHttpClient(HttpClient client, SimpleHttpClientManager manager) : IDisposable
        {
            private readonly HttpClient _client = client;
            private readonly SimpleHttpClientManager _manager = manager;
            private bool _disposed = false;

            public HttpClient Client => _client;

            ~ManagedHttpClient() => Dispose(false);
            protected virtual void Dispose(bool disposeing) {
                if (!_disposed)
                {
                    if (disposeing)
                    {
                        _manager.ReturnClient(_client);
                    }
                    _disposed = true;
                }
            }
            /// <summary>
            /// 释放资源并调用GC.SuppressFinalize
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                // 关键优化：调用GC.SuppressFinalize来避免不必要的终结器执行
                GC.SuppressFinalize(this);
            }
        }
    }
}