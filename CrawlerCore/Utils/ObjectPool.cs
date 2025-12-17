// <copyright file="ObjectPool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Utils
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// 对象池实现，用于管理可重用对象的创建和复用.
    /// </summary>
    /// <typeparam name="T">池化对象的类型.</typeparam>
    public class ObjectPool<T>
        where T : class, new()
    {
        /// <summary>
        /// 存储可重用对象的并发集合.
        /// </summary>
        private readonly ConcurrentBag<T> _objects = [];

        /// <summary>
        /// 对象生成器函数.
        /// </summary>
        private readonly Func<T> _objectGenerator;

        /// <summary>
        /// 对象重置操作.
        /// </summary>
        private readonly Action<T>? _resetAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class.
        /// 初始化 <see cref="ObjectPool{T}"/> 类的新实例.
        /// </summary>
        /// <param name="objectGenerator">对象生成器函数，用于创建新对象。如果为 null，则使用默认构造函数。</param>
        /// <param name="resetAction">对象重置操作，用于在对象返回池时重置其状态。</param>
        public ObjectPool(Func<T>? objectGenerator = null, Action<T>? resetAction = null)
        {
            this._objectGenerator = objectGenerator ?? (() => new T());
            this._resetAction = resetAction;
        }

        /// <summary>
        /// Gets 获取对象池中当前可用对象的数量.
        /// </summary>
        public int Count => this._objects.Count;

        /// <summary>
        /// 从对象池中获取一个对象.
        /// </summary>
        /// <returns>获取到的对象.</returns>
        public T Get()
        {
            if (this._objects.TryTake(out T? item))
            {
                return item;
            }

            return this._objectGenerator();
        }

        /// <summary>
        /// 将对象返回给对象池.
        /// </summary>
        /// <param name="item">要返回的对象.</param>
        public void Return(T item)
        {
            this._resetAction?.Invoke(item);
            this._objects.Add(item);
        }


        /// <summary>
        /// 获取对象池中的所有对象（用于清理或调试）.
        /// </summary>
        /// <returns>对象池中的所有对象集合.</returns>
        public System.Collections.Generic.IEnumerable<T> GetAllObjects()
        {
            return [.. this._objects]; // 创建副本以避免并发问题
        }
    }

    /// <summary>
    /// HttpClient 对象池实现.
    /// </summary>
    public class HttpClientPool : IDisposable
    {
        /// <summary>
        /// HttpClient 对象池.
        /// </summary>
        private readonly ObjectPool<HttpClient> _pool;

        /// <summary>
        /// 是否已释放资源.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientPool"/> class.
        /// 初始化 <see cref="HttpClientPool"/> 类的新实例.
        /// </summary>
        public HttpClientPool()
        {
            _pool = new ObjectPool<HttpClient>(
                objectGenerator: () => new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                resetAction: client =>
                {
                    // 重置 HttpClient 状态
                    client.DefaultRequestHeaders.Clear();
                });
        }

        /// <summary>
        /// 从对象池中获取一个HttpClient实例.
        /// </summary>
        /// <returns>包装了HttpClient的PooledHttpClient实例.</returns>
        /// <exception cref="ObjectDisposedException">当对象池已被释放时抛出.</exception>
        public PooledHttpClient GetClient()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var client = _pool.Get();
            return new PooledHttpClient(client, this);
        }

        /// <summary>
        /// 将HttpClient实例返回给对象池.
        /// </summary>
        /// <param name="client">要返回的HttpClient实例.</param>
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

        /// <summary>
        /// 释放由 <see cref="HttpClientPool"/> 实例占用的资源。.
        /// </summary>
        /// <param name="disposing">如果为 true，则释放托管资源和非托管资源；如果为 false，则仅释放非托管资源。.</param>
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

        /// <summary>
        /// 释放当前实例占用的资源.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 池化的HttpClient类，用于管理从对象池获取的HttpClient实例的生命周期.
        /// </summary>
        public class PooledHttpClient : IDisposable
        {
            /// <summary>
            /// 获取包装的HttpClient实例.
            /// </summary>
            private readonly HttpClient _client;

            /// <summary>
            /// 获取对象池实例.
            /// </summary>
            private readonly HttpClientPool _pool;

            /// <summary>
            /// 获取一个值，指示当前实例是否已被释放.
            /// </summary>
            private bool _disposed = false;

            /// <summary>
            /// 初始化 <see cref="PooledHttpClient"/> 类的新实例.
            /// </summary>
            /// <param name="client">要包装的HttpClient实例.</param>
            /// <param name="pool">返回HttpClient实例的对象池.</param>
            public PooledHttpClient(HttpClient client, HttpClientPool pool)
            {
                this._client = client;
                this._pool = pool;
            }

            /// <summary>
            /// 获取包装的HttpClient实例.
            /// </summary>
            public HttpClient Client => _client;

            /// <summary>
            /// 释放当前实例占用的资源.
            /// </summary>
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Finalizes an instance of the <see cref="PooledHttpClient"/> class.
            /// 终结器，用于确保资源被释放.
            /// </summary>
            ~PooledHttpClient()
            {
                this.Dispose(false);
            }

            /// <summary>
            /// 释放当前实例占用的资源.
            /// </summary>
            /// <param name="disposing">如果为true，表示是主动释放资源；如果为false，表示是从终结器调用.</param>
            protected virtual void Dispose(bool disposing)
            {
                if (!this._disposed)
                {
                    if (disposing)
                    {
                        // 释放托管资源
                        this._pool.ReturnClient(this._client);
                    }

                    this._disposed = true;
                }
            }
        }
    }
}