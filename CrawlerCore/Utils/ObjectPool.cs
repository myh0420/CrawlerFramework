// CrawlerCore/Utils/ObjectPool.cs
using System;
using System.Collections.Concurrent;

namespace CrawlerCore.Utils
{
    /// <summary>
    /// 内存优化和对象池
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectPool<T>(Func<T>? objectGenerator = null, Action<T>? resetAction = null) where T : class, new()
    {
        private readonly ConcurrentBag<T> _objects = [];
        private readonly Func<T> _objectGenerator = objectGenerator ?? (() => new T());
        private readonly Action<T>? _resetAction = resetAction;

        public T Get()
        {
            if (_objects.TryTake(out T? item))
            {
                return item;
            }

            return _objectGenerator();
        }

        public void Return(T item)
        {
            _resetAction?.Invoke(item);
            _objects.Add(item);
        }

        public int Count => _objects.Count;
        // 添加一个方法来获取所有对象（用于清理）
        public System.Collections.Generic.IEnumerable<T> GetAllObjects()
        {
            return [.. _objects]; // 创建副本以避免并发问题
        }
    }

    // 使用示例：HttpClient 池
    public class HttpClientPool : IDisposable
    {
        private readonly ObjectPool<HttpClient> _pool;
        private bool _disposed = false;

        public HttpClientPool()
        {
            _pool = new ObjectPool<HttpClient>(
                objectGenerator: () => new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                resetAction: client =>
                {
                    // 重置 HttpClient 状态
                    client.DefaultRequestHeaders.Clear();
                }
            );
        }

        public PooledHttpClient GetClient()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var client = _pool.Get();
            return new PooledHttpClient(client, this);
        }

        private void ReturnClient(HttpClient client)
        {
            if (!_disposed)
            {
                _pool.Return(client);
            }
            else
            {
                client.Dispose();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    var allClients = _pool.GetAllObjects();
                    foreach (var client in allClients)
                    {
                        client.Dispose();
                    }
                }
                _disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public class PooledHttpClient(HttpClient client, HttpClientPool pool) : IDisposable
        {
            private readonly HttpClient _client = client;
            private readonly HttpClientPool _pool = pool;
            private bool _disposed = false;

            public HttpClient Client => _client;

            //public void Dispose()
            //{
            //    if (!_disposed)
            //    {
            //        _disposed = true;
            //        _pool.ReturnClient(_client);
            //    }
            //}
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            ~PooledHttpClient() {
                Dispose(false);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        // 释放托管资源
                        _pool.ReturnClient(_client);
                    }

                    _disposed = true;
                }
            }
        }
    }
}