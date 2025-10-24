// CrawlerCore/Utils/SimpleHttpClientManager.cs
using System;
using System.Collections.Concurrent;
using System.Net.Http;

namespace CrawlerCore.Utils
{
    /// <summary>
    /// 简化的 HttpClient 管理器
    /// </summary>
    public class SimpleHttpClientManager(int maxClients = 10) : IDisposable
    {
        private readonly ConcurrentQueue<HttpClient> _availableClients = [];
        private readonly ConcurrentBag<HttpClient> _allClients = [];
        private readonly Lock _lockObject = new();
        private readonly int _maxClients = maxClients;
        private int _createdCount = 0;
        private bool _disposed = false;

        public ManagedHttpClient GetClient()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_availableClients.TryDequeue(out HttpClient? client))
            {
                return new ManagedHttpClient(client, this);
            }

            lock (_lockObject)
            {
                if (_createdCount < _maxClients)
                {
                    client = CreateNewClient();
                    _createdCount++;
                    _allClients.Add(client);
                    return new ManagedHttpClient(client, this);
                }
            }

            // 如果达到最大数量，等待可用的客户端
            // 在实际应用中，你可能需要更复杂的等待策略
            throw new InvalidOperationException("No available HttpClient instances");
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