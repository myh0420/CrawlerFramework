// <copyright file="SimpleHttpClientManager.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Utils
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// 简化的 HttpClient 管理器，用于管理 HttpClient 实例池，实现高效的资源复用。
    /// </summary>
    public class SimpleHttpClientManager : IDisposable
    {
        /// <summary>
        /// 全局可用的 HttpClient 实例队列（用于不指定域名的请求）。
        /// </summary>
        private readonly ConcurrentQueue<ClientInfo> _availableClients = [];

        /// <summary>
        /// 基于域名的可用 HttpClient 实例队列。
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ClientInfo>> _domainClients = [];

        /// <summary>
        /// 所有创建的 HttpClient 实例集合。
        /// </summary>
        private readonly ConcurrentBag<ClientInfo> _allClients = [];

        /// <summary>
        /// 基于域名的客户端数量计数。
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _domainClientCounts = [];

        /// <summary>
        /// 用于线程同步的信号量。
        /// </summary>
        private readonly SemaphoreSlim _semaphore = new (1);

        /// <summary>
        /// 客户端创建工厂函数。
        /// </summary>
        private readonly Func<HttpClient> _clientFactory;

        /// <summary>
        /// 用于通知全局客户端可用的信号量。
        /// </summary>
        private readonly SemaphoreSlim _clientAvailableSemaphore = new (0);

        /// <summary>
        /// 基于域名的客户端可用信号量。
        /// </summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _domainClientAvailableSemaphores = [];

        /// <summary>
        /// 管理器创建时间（Unix时间戳，毫秒）。
        /// </summary>
        private readonly long _createdTime;

        /// <summary>
        /// 客户端最大生命周期（毫秒），默认1小时。
        /// </summary>
        private readonly long _maxClientLifetimeMs;

        /// <summary>
        /// 最大空闲时间（毫秒），默认30分钟。
        /// </summary>
        private readonly long _maxIdleTimeMs;

        /// <summary>
        /// 最大客户端数量限制。
        /// </summary>
        private readonly int _maxClients;

        /// <summary>
        /// 每个域名的最大客户端数量限制。
        /// </summary>
        private readonly int _maxClientsPerDomain;

        /// <summary>
        /// 是否启用基于域名的客户端隔离。
        /// </summary>
        private readonly bool _enableDomainIsolation;

        /// <summary>
        /// 自动清理的时间间隔（毫秒）。
        /// </summary>
        private readonly long _cleanupIntervalMs;

        /// <summary>
        /// 是否验证域名格式。
        /// </summary>
        private readonly bool _validateDomainFormat;

        /// <summary>
        /// 已创建的客户端数量。
        /// </summary>
        private int _createdCount = 0;

        /// <summary>
        /// 表示管理器是否已被释放。
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// 客户端请求总数。
        /// </summary>
        private long _totalClientRequests = 0;

        /// <summary>
        /// 客户端等待总时间（毫秒）。
        /// </summary>
        private long _waitTimeMs = 0;

        /// <summary>
        /// 已过期客户端数量。
        /// </summary>
        private long _expiredClients = 0;

        /// <summary>
        /// 最后清理时间（Unix时间戳，毫秒）。
        /// </summary>
        private long _lastCleanupTimeMs;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleHttpClientManager"/> class.
        /// 初始化 <see cref="SimpleHttpClientManager"/> 类的新实例。
        /// </summary>
        /// <param name="maxClients">最大客户端数量限制，默认值为10。</param>
        /// <param name="clientFactory">客户端创建工厂函数，用于自定义HttpClient的创建逻辑。</param>
        /// <param name="maxClientLifetime">客户端最大生命周期，默认值为1小时。</param>
        /// <param name="maxIdleTime">客户端最大空闲时间，默认值为30分钟。</param>
        // [Obsolete("Use constructor with HttpClientManagerOptions instead")]
        public SimpleHttpClientManager(int maxClients = 10, Func<HttpClient>? clientFactory = null, TimeSpan? maxClientLifetime = null, TimeSpan? maxIdleTime = null)
            : this(
                new HttpClientManagerOptions
            {
                MaxClients = maxClients,
                MaxClientLifetime = maxClientLifetime ?? TimeSpan.FromHours(1),
                MaxIdleTime = maxIdleTime ?? TimeSpan.FromMinutes(30),
            }, clientFactory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleHttpClientManager"/> class with configuration options.
        /// 使用配置选项初始化 <see cref="SimpleHttpClientManager"/> 类的新实例。
        /// </summary>
        /// <param name="options">HttpClient管理器配置选项。</param>
        /// <param name="clientFactory">客户端创建工厂函数，用于自定义HttpClient的创建逻辑。</param>
        public SimpleHttpClientManager(HttpClientManagerOptions options, Func<HttpClient>? clientFactory = null)
        {
            options = options ?? throw new ArgumentNullException(nameof(options));

            _maxClients = options.MaxClients;
            _maxClientsPerDomain = options.MaxClientsPerDomain;
            _clientFactory = clientFactory ?? CreateNewClient;
            _createdTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            _lastCleanupTimeMs = _createdTime;
            _maxClientLifetimeMs = (long)options.MaxClientLifetime.TotalMilliseconds;
            _maxIdleTimeMs = (long)options.MaxIdleTime.TotalMilliseconds;
            _enableDomainIsolation = options.EnableDomainIsolation;
            _cleanupIntervalMs = (long)options.CleanupInterval.TotalMilliseconds;
            _validateDomainFormat = options.ValidateDomainFormat;
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
        /// Gets the current number of available HttpClient instances, which are created but not in use.
        /// </summary>
        public int AvailableCount => _availableClients.Count;

        /// <summary>
        /// Gets 获取已创建的 HttpClient 实例总数，包括正在使用和可用的实例。
        /// </summary>
        public int TotalCount => _createdCount;

        /// <summary>
        /// 获取一个HttpClient实例，如果没有可用实例且未达到最大数量，则创建新实例；否则等待可用实例。
        /// </summary>
        /// <param name="domain">可选的域名参数，用于实现基于域名的客户端隔离</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>管理的HttpClient实例</returns>
        public async Task<ManagedHttpClient> GetClientAsync(string? domain = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Interlocked.Increment(ref _totalClientRequests);

            // 定期清理过期客户端
            await CleanupExpiredClientsAsync();

            // 标准化域名（如果提供）
            domain = NormalizeDomain(domain);
            bool useDomainIsolation = _enableDomainIsolation && !string.IsNullOrEmpty(domain);

            // 尝试直接获取可用客户端
            ClientInfo clientInfo;
            if (useDomainIsolation)
            {
                // 尝试从域名专用队列获取客户端
                if (_domainClients.TryGetValue(domain!, out var domainQueue))
                {
                    while (domainQueue.TryDequeue(out clientInfo))
                    {
                        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        if (!IsClientExpired(clientInfo, now))
                        {
                            return new ManagedHttpClient(clientInfo, this, domain);
                        }

                        // 清理过期客户端
                        clientInfo.Client.Dispose();
                        Interlocked.Increment(ref _expiredClients);
                        Interlocked.Decrement(ref _createdCount);
                        _domainClientCounts.AddOrUpdate(domain!, 0, (_, count) => Math.Max(0, count - 1));

                        // 释放域名专用信号量，因为该客户端将不再可用
                        try
                        {
                            if (_domainClientAvailableSemaphores.TryGetValue(domain!, out var semaphore))
                            {
                                semaphore.Release();
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // 忽略对象已释放的异常
                        }
                    }
                }
            }
            else
            {
                // 尝试从全局队列获取客户端
                while (_availableClients.TryDequeue(out clientInfo))
                {
                    var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    if (!IsClientExpired(clientInfo, now))
                    {
                        return new ManagedHttpClient(clientInfo, this, null);
                    }

                    // 清理过期客户端
                    clientInfo.Client.Dispose();
                    Interlocked.Increment(ref _expiredClients);
                    Interlocked.Decrement(ref _createdCount);

                    // 释放全局信号量，因为该客户端将不再可用
                    try
                    {
                        _clientAvailableSemaphore.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // 忽略对象已释放的异常
                    }
                }
            }

            var waitStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // 尝试创建新客户端
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // 再次检查是否有可用客户端（可能在等待期间有客户端被释放）
                if (useDomainIsolation)
                {
                    if (_domainClients.TryGetValue(domain!, out var domainQueue))
                    {
                        while (domainQueue.TryDequeue(out clientInfo))
                        {
                            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                            if (!IsClientExpired(clientInfo, now))
                            {
                                return new ManagedHttpClient(clientInfo, this, domain);
                            }

                            // 清理过期客户端
                            clientInfo.Client.Dispose();
                            Interlocked.Increment(ref _expiredClients);
                            Interlocked.Decrement(ref _createdCount);
                            _domainClientCounts.AddOrUpdate(domain!, 0, (_, count) => Math.Max(0, count - 1));

                            // 释放域名专用信号量，因为该客户端将不再可用
                            try
                            {
                                if (_domainClientAvailableSemaphores.TryGetValue(domain!, out var semaphore))
                                {
                                    semaphore.Release();
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // 忽略对象已释放的异常
                            }
                        }
                    }
                }
                else
                {
                    while (_availableClients.TryDequeue(out clientInfo))
                    {
                        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        if (!IsClientExpired(clientInfo, now))
                        {
                            return new ManagedHttpClient(clientInfo, this, null);
                        }

                        // 清理过期客户端
                        clientInfo.Client.Dispose();
                        Interlocked.Increment(ref _expiredClients);
                        Interlocked.Decrement(ref _createdCount);

                        // 释放全局信号量，因为该客户端将不再可用
                        try
                        {
                            _clientAvailableSemaphore.Release();
                        }
                        catch (ObjectDisposedException)
                        {
                            // 忽略对象已释放的异常
                        }
                    }
                }

                // 检查是否可以创建新客户端
                bool canCreateNewClient = _createdCount < _maxClients;
                if (useDomainIsolation)
                {
                    // 对于域名隔离的客户端，还需要检查域名级别的限制
                    int domainClientCount = _domainClientCounts.GetOrAdd(domain!, 0);
                    canCreateNewClient &= domainClientCount < _maxClientsPerDomain;
                }

                if (canCreateNewClient)
                {
                    var client = _clientFactory();
                    _createdCount++;
                    var newClientInfo = new ClientInfo
                    {
                        Client = client,
                        CreatedTimeMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        LastUsedTimeMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    };
                    _allClients.Add(newClientInfo);

                    if (useDomainIsolation)
                    {
                        // 更新域名客户端计数
                        _domainClientCounts.AddOrUpdate(domain!, 1, (_, count) => count + 1);
                    }

                    return new ManagedHttpClient(newClientInfo, this, domain);
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
                    if (useDomainIsolation)
                    {
                        // 获取或创建域名专用信号量
                        var domainSemaphore = _domainClientAvailableSemaphores.GetOrAdd(
                            domain!, _ => new SemaphoreSlim(0));

                        if (await domainSemaphore.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken))
                        {
                            if (_domainClients.TryGetValue(domain!, out var domainQueue))
                            {
                                while (domainQueue.TryDequeue(out clientInfo))
                                {
                                    var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                                    if (!IsClientExpired(clientInfo, now))
                                    {
                                        var waitEndTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                                        Interlocked.Add(ref _waitTimeMs, waitEndTime - waitStartTime);
                                        return new ManagedHttpClient(clientInfo, this, domain);
                                    }

                                    // 清理过期客户端
                                    clientInfo.Client.Dispose();
                                    Interlocked.Increment(ref _expiredClients);
                                    Interlocked.Decrement(ref _createdCount);
                                    _domainClientCounts.AddOrUpdate(domain!, 0, (_, count) => Math.Max(0, count - 1));

                                    // 释放域名专用信号量，因为该客户端将不再可用
                                    try
                                    {
                                        if (_domainClientAvailableSemaphores.TryGetValue(domain!, out var semaphore))
                                        {
                                            semaphore.Release();
                                        }
                                    }
                                    catch (ObjectDisposedException)
                                    {
                                        // 忽略对象已释放的异常
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (await _clientAvailableSemaphore.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken))
                        {
                            while (_availableClients.TryDequeue(out clientInfo))
                            {
                                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                                if (!IsClientExpired(clientInfo, now))
                                {
                                    var waitEndTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                                    Interlocked.Add(ref _waitTimeMs, waitEndTime - waitStartTime);
                                    return new ManagedHttpClient(clientInfo, this, null);
                                }

                                // 清理过期客户端
                                clientInfo.Client.Dispose();
                                Interlocked.Increment(ref _expiredClients);
                                Interlocked.Decrement(ref _createdCount);

                                // 释放全局信号量，因为该客户端将不再可用
                                try
                                {
                                    _clientAvailableSemaphore.Release();
                                }
                                catch (ObjectDisposedException)
                                {
                                    // 忽略对象已释放的异常
                                }
                            }
                        }
                    }

                    // 定期清理过期客户端
                    await CleanupExpiredClientsAsync();
                }
                catch (OperationCanceledException)
                {
                }
            }

            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            // 理论上不会到达这里，但为了编译安全
            throw new InvalidOperationException("No available HttpClient instances");
        }

        /// <summary>
        /// 标准化域名格式。
        /// </summary>
        /// <param name="domain">原始域名</param>
        /// <returns>标准化后的域名</returns>
        private static string? NormalizeDomain(string? domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                return null;
            }

            // 移除前后空格
            domain = domain.Trim();

            // 移除协议前缀
            if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                domain = domain["http://".Length..];
            }
            else if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                domain = domain["https://".Length..];
            }

            // 移除端口号
            int portIndex = domain.IndexOf(':');
            if (portIndex > 0)
            {
                domain = domain[..portIndex];
            }

            // 移除路径部分
            int pathIndex = domain.IndexOf('/');
            if (pathIndex > 0)
            {
                domain = domain[..pathIndex];
            }

            return domain.ToLowerInvariant();
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
        /// 释放资源。
        /// </summary>
        /// <param name="disposing">表示是否释放托管资源。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 清理所有客户端
                    foreach (var client in _allClients)
                    {
                        client.Client.Dispose();
                    }

                    _availableClients.Clear();
                    _allClients.Clear();

                    // 释放信号量资源
                    _clientAvailableSemaphore.Dispose();
                    _semaphore.Dispose();

                    // 释放域名相关的信号量资源
                    foreach (var semaphore in _domainClientAvailableSemaphores.Values)
                    {
                        semaphore.Dispose();
                    }

                    _domainClientAvailableSemaphores.Clear();

                    // 清理域名客户端队列和计数
                    _domainClients.Clear();
                    _domainClientCounts.Clear();
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
        /// 清理过期的客户端实例。
        /// </summary>
        private async Task CleanupExpiredClientsAsync()
        {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // 每隔指定间隔清理一次过期客户端
            if (now - _lastCleanupTimeMs < _cleanupIntervalMs)
            {
                return;
            }

            await _semaphore.WaitAsync();
            try
            {
                _lastCleanupTimeMs = now;
                int totalExpiredCount = 0;

                // 清理全局客户端队列
                var validClients = new List<ClientInfo>();
                int expiredCount = 0;

                while (_availableClients.TryDequeue(out ClientInfo clientInfo))
                {
                    if (IsClientExpired(clientInfo, now))
                    {
                        clientInfo.Client.Dispose();
                        expiredCount++;
                    }
                    else
                    {
                        validClients.Add(clientInfo);
                    }
                }

                // 将有效客户端重新入队
                foreach (var clientInfo in validClients)
                {
                    _availableClients.Enqueue(clientInfo);
                }

                // 更新过期客户端计数
                if (expiredCount > 0)
                {
                    Interlocked.Add(ref _expiredClients, expiredCount);
                    Interlocked.Add(ref _createdCount, -expiredCount);
                    totalExpiredCount += expiredCount;

                    // 释放相应数量的全局信号量，因为这些客户端将不再可用
                    try
                    {
                        for (int i = 0; i < expiredCount; i++)
                        {
                            _clientAvailableSemaphore.Release();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // 忽略对象已释放的异常
                    }
                }

                // 清理基于域名的客户端队列
                if (_enableDomainIsolation && !_domainClients.IsEmpty)
                {
                    // 创建一个新的字典来存储有效的域名客户端队列
                    var validDomainClients = new ConcurrentDictionary<string, ConcurrentQueue<ClientInfo>>();
                    var domainExpiredCounts = new Dictionary<string, int>();

                    foreach (var domainEntry in _domainClients)
                    {
                        string domain = domainEntry.Key;
                        var domainQueue = domainEntry.Value;
                        var validDomainQueue = new ConcurrentQueue<ClientInfo>();
                        int domainExpiredCount = 0;

                        while (domainQueue.TryDequeue(out ClientInfo clientInfo))
                        {
                            if (IsClientExpired(clientInfo, now))
                            {
                                clientInfo.Client.Dispose();
                                domainExpiredCount++;
                            }
                            else
                            {
                                validDomainQueue.Enqueue(clientInfo);
                            }
                        }

                        if (!validDomainQueue.IsEmpty)
                        {
                            validDomainClients[domain] = validDomainQueue;
                        }

                        if (domainExpiredCount > 0)
                        {
                            domainExpiredCounts[domain] = domainExpiredCount;
                            Interlocked.Add(ref _expiredClients, domainExpiredCount);
                            Interlocked.Add(ref _createdCount, -domainExpiredCount);
                            totalExpiredCount += domainExpiredCount;

                            // 释放相应数量的域名专用信号量，因为这些客户端将不再可用
                            try
                            {
                                if (_domainClientAvailableSemaphores.TryGetValue(domain, out var semaphore))
                                {
                                    for (int i = 0; i < domainExpiredCount; i++)
                                    {
                                        semaphore.Release();
                                    }
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // 忽略对象已释放的异常
                            }
                        }
                    }

                    // 更新域名客户端队列
                    foreach (var domain in _domainClients.Keys)
                    {
                        _domainClients.TryRemove(domain, out _);
                    }

                    foreach (var validDomainEntry in validDomainClients)
                    {
                        _domainClients[validDomainEntry.Key] = validDomainEntry.Value;
                    }

                    // 更新域名客户端计数
                    foreach (var domainExpiredEntry in domainExpiredCounts)
                    {
                        _domainClientCounts.AddOrUpdate(domainExpiredEntry.Key, 0, (_, count) =>
                            Math.Max(0, count - domainExpiredEntry.Value));
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 检查客户端是否已过期。
        /// </summary>
        /// <param name="clientInfo">客户端信息。</param>
        /// <param name="currentTimeMs">当前时间戳（毫秒）。</param>
        /// <returns>如果客户端已过期则返回true，否则返回false。</returns>
        private bool IsClientExpired(ClientInfo clientInfo, long currentTimeMs)
        {
            // 检查生命周期是否过期
            if (currentTimeMs - clientInfo.CreatedTimeMs > _maxClientLifetimeMs)
            {
                return true;
            }

            // 检查空闲时间是否过期
            if (currentTimeMs - clientInfo.LastUsedTimeMs > _maxIdleTimeMs)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 将 HttpClient 实例返回到管理器的可用队列中。
        /// </summary>
        /// <param name="clientInfo">要返回的客户端信息。</param>
        /// <param name="domain">客户端所属的域名（如果有）。</param>
        internal void ReturnClient(ClientInfo clientInfo, string? domain)
        {
            if (!_disposed)
            {
                // 重置客户端状态
                clientInfo.Client.DefaultRequestHeaders.Clear();

                // 重新添加基本头信息
                clientInfo.Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                clientInfo.Client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                clientInfo.Client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

                // 更新最后使用时间
                var updatedClientInfo = new ClientInfo
                {
                    Client = clientInfo.Client,
                    CreatedTimeMs = clientInfo.CreatedTimeMs, // 保留原始创建时间
                    LastUsedTimeMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                };

                // 标准化域名
                domain = NormalizeDomain(domain);
                bool useDomainIsolation = _enableDomainIsolation && !string.IsNullOrEmpty(domain);

                if (useDomainIsolation)
                {
                    // 获取或创建域名专用队列
                    var domainQueue = _domainClients.GetOrAdd(domain!, _ => new ConcurrentQueue<ClientInfo>());
                    domainQueue.Enqueue(updatedClientInfo);

                    // 触发域名专用信号量，通知等待的线程有客户端可用
                    try
                    {
                        if (_domainClientAvailableSemaphores.TryGetValue(domain!, out var semaphore))
                        {
                            semaphore.Release();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
                else
                {
                    // 返回全局队列
                    _availableClients.Enqueue(updatedClientInfo);

                    // 触发全局信号量，通知等待的线程有客户端可用
                    try
                    {
                        _clientAvailableSemaphore.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
            else
            {
                clientInfo.Client.Dispose();
            }
        }

        /// <summary>
        /// 将 HttpClient 实例返回到管理器的可用队列中（重载方法，用于兼容旧代码）。
        /// </summary>
        /// <param name="client">要返回的 HttpClient 实例。</param>
        private void ReturnClient(HttpClient client)
        {
            var clientInfo = new ClientInfo
            {
                Client = client,
                CreatedTimeMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                LastUsedTimeMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            };

            ReturnClient(clientInfo, null);
        }

        /// <summary>
        /// 将 HttpClient 实例返回到管理器的可用队列中（重载方法，用于兼容旧代码）。
        /// </summary>
        /// <param name="clientInfo">要返回的客户端信息。</param>
        private void ReturnClient(ClientInfo clientInfo)
        {
            ReturnClient(clientInfo, null);
        }

        /// <summary>
        /// Client information structure containing client instance and creation time.
        /// </summary>
        public readonly struct ClientInfo
        {
            /// <summary>
            /// Gets the HttpClient instance.
            /// </summary>
            public HttpClient Client { get; init; }

            /// <summary>
            /// Gets the client creation time.
            /// </summary>
            public long CreatedTimeMs { get; init; }

            /// <summary>
            /// Gets the client last used time.
            /// </summary>
            public long LastUsedTimeMs { get; init; }
        }

        /// <summary>
        /// 受管理的 HttpClient 包装类，用于确保 HttpClient 实例在使用后正确返回到管理器。
        /// </summary>
        public class ManagedHttpClient : IDisposable
        {
            /// <summary>
            /// 包装的客户端信息。
            /// </summary>
            private readonly ClientInfo _clientInfo;

            /// <summary>
            /// 所属的 HttpClient 管理器。
            /// </summary>
            private readonly SimpleHttpClientManager _manager;

            /// <summary>
            /// 客户端所属的域名（如果有）。
            /// </summary>
            private readonly string? _domain;

            /// <summary>
            /// 表示实例是否已被释放。
            /// </summary>
            private bool _disposed = false;

            /// <summary>
            /// Initializes a new instance of the <see cref="ManagedHttpClient"/> class.
            /// 初始化 <see cref="ManagedHttpClient"/> 类的新实例。
            /// </summary>
            /// <param name="clientInfo">要包装的客户端信息。</param>
            /// <param name="manager">所属的 HttpClient 管理器。</param>
            /// <param name="domain">客户端所属的域名（如果有）。</param>
            public ManagedHttpClient(ClientInfo clientInfo, SimpleHttpClientManager manager, string? domain = null)
            {
                _clientInfo = clientInfo;
                _manager = manager;
                _domain = domain;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="ManagedHttpClient"/> class.
            /// 初始化 <see cref="ManagedHttpClient"/> 类的新实例。
            /// </summary>
            /// <param name="client">要包装的 HttpClient 实例。</param>
            /// <param name="manager">所属的 HttpClient 管理器。</param>
            /// <param name="domain">客户端所属的域名（如果有）。</param>
            public ManagedHttpClient(HttpClient client, SimpleHttpClientManager manager, string? domain = null)
                : this(new ClientInfo { Client = client, CreatedTimeMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(), LastUsedTimeMs = DateTimeOffset.Now.ToUnixTimeMilliseconds() }, manager, domain)
            {
            }

            /// <summary>
            /// Finalizes an instance of the <see cref="ManagedHttpClient"/> class.
            /// 终结器，确保资源被释放。
            /// </summary>
            ~ManagedHttpClient() => Dispose(false);

            /// <summary>
            /// Gets 获取包装的 HttpClient 实例。
            /// </summary>
            public HttpClient Client => _clientInfo.Client;

            /// <summary>
            /// Gets 获取客户端信息。
            /// </summary>
            internal ClientInfo ClientInfo => _clientInfo;

            /// <summary>
            /// Gets 获取客户端所属的域名（如果有）。
            /// </summary>
            internal string? Domain => _domain;

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
                        _manager.ReturnClient(_clientInfo, _domain);
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