// <copyright file="ObjectPool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Utils
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
}