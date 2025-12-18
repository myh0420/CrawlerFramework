// <copyright file="SimpleHttpClientManager.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Utils
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// 简化的 HttpClient 管理器，用于管理 HttpClient 实例池，实现高效的资源复用。
    /// </summary>
    public class SimpleHttpClientManager : IDisposable
    {
        /// <summary>
        /// 可用的 HttpClient 实例队列。
        /// </summary>
        private readonly ConcurrentQueue<HttpClient> _availableClients = [];

        /// <summary>
        /// 所有创建的 HttpClient 实例集合。
        /// </summary>
        private readonly ConcurrentBag<HttpClient> _allClients = [];

        /// <summary>
        /// 最大客户端数量限制。
        /// </summary>
        private readonly int _maxClients;

        /// <summary>
        /// 已创建的客户端数量。
        /// </summary>
        private int _createdCount = 0;

        /// <summary>
        /// 表示管理器是否已被释放。
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// 用于线程同步的信号量。
        /// </summary>
        private readonly SemaphoreSlim _semaphore = new(1);

        /// <summary>
        /// 客户端创建工厂函数。
        /// </summary>
        private readonly Func<HttpClient> _clientFactory;

        /// <summary>
        /// 用于通知客户端可用的信号量。
        /// </summary>
        private readonly SemaphoreSlim _clientAvailableSemaphore = new(0);

        /// <summary>
        /// 管理器创建时间（Unix时间戳，毫秒）。
        /// </summary>
        private readonly long _createdTime;

        /// <summary>
        /// 客户端请求总数。
        /// </summary>
        private long _totalClientRequests = 0;

        /// <summary>
        /// 客户端等待总时间（毫秒）。
        /// </summary>
        private long _waitTimeMs = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleHttpClientManager"/> class.
        /// 初始化 <see cref="SimpleHttpClientManager"/> 类的新实例。
        /// </summary>
        /// <param name="maxClients">最大客户端数量限制，默认值为10。</param>
        /// <param name="clientFactory">客户端创建工厂函数，用于自定义HttpClient的创建逻辑。</param>
        public SimpleHttpClientManager(int maxClients = 10, Func<HttpClient>? clientFactory = null)
        {
            _maxClients = maxClients;
            _clientFactory = clientFactory ?? CreateNewClient;
            _createdTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

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

        /// <summary>
        /// 创建一个新的 HttpClient 实例。
        /// </summary>
        /// <returns>新创建的 HttpClient 实例。</returns>
        private static HttpClient CreateNewClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
                DefaultRequestHeaders =
                {
                    { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
                    { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                    { "Accept-Language", "en-US,en;q=0.5" },
                },
            };
        }

        /// <summary>
        /// 将 HttpClient 实例返回到管理器的可用队列中。
        /// </summary>
        /// <param name="client">要返回的 HttpClient 实例。</param>
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

        /// <summary>
        /// Finalizes an instance of the <see cref="SimpleHttpClientManager"/> class.
        /// 终结器，确保资源被释放。
        /// </summary>
        ~SimpleHttpClientManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        /// <param name="disposing">表示是否释放托管资源。.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
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
        /// 释放资源并调用 GC.SuppressFinalize。.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            // 关键优化：调用 GC.SuppressFinalize 来避免不必要的终结器执行
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 获取当前可用的 HttpClient 实例数量，即已创建但未被使用的实例数。
        /// </summary>
        public int AvailableCount => _availableClients.Count;

        /// <summary>
        /// 获取已创建的 HttpClient 实例总数，包括正在使用和可用的实例。
        /// </summary>
        public int TotalCount => _createdCount;

        /// <summary>
        /// 受管理的 HttpClient 包装类，用于确保 HttpClient 实例在使用后正确返回到管理器。
        /// </summary>
        public class ManagedHttpClient : IDisposable
        {
            /// <summary>
            /// 包装的 HttpClient 实例。
            /// </summary>
            private readonly HttpClient _client;

            /// <summary>
            /// 所属的 HttpClient 管理器。
            /// </summary>
            private readonly SimpleHttpClientManager _manager;

            /// <summary>
            /// 表示实例是否已被释放。
            /// </summary>
            private bool _disposed = false;

            /// <summary>
            /// Initializes a new instance of the <see cref="ManagedHttpClient"/> class.
            /// 初始化 <see cref="ManagedHttpClient"/> 类的新实例。
            /// </summary>
            /// <param name="client">要包装的 HttpClient 实例。</param>
            /// <param name="manager">所属的 HttpClient 管理器。</param>
            public ManagedHttpClient(HttpClient client, SimpleHttpClientManager manager)
            {
                _client = client;
                _manager = manager;
            }

            /// <summary>
            /// 获取包装的 HttpClient 实例。
            /// </summary>
            public HttpClient Client => _client;

            /// <summary>
            /// Finalizes an instance of the <see cref="ManagedHttpClient"/> class.
            /// 终结器，确保资源被释放。
            /// </summary>
            ~ManagedHttpClient() => Dispose(false);

            /// <summary>
            /// 释放资源。
            /// </summary>
            /// <param name="disposing">表示是否释放托管资源。</param>
            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        _manager.ReturnClient(_client);
                    }

                    _disposed = true;
                }
            }

            /// <summary>
            /// 释放资源并调用 GC.SuppressFinalize。
            /// </summary>
            public void Dispose()
            {
                Dispose(true);

                // 关键优化：调用 GC.SuppressFinalize 来避免不必要的终结器执行
                GC.SuppressFinalize(this);
            }
        }
    }
}