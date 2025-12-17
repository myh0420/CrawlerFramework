// <copyright file="AsyncStreamProcessor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// 异步流处理器，用于并行处理异步流中的项目，控制并发度。.
    /// </summary>
    /// <typeparam name="T">流中项目的类型。.</typeparam>
    public class AsyncStreamProcessor<T>
    {
        /// <summary>
        /// 处理单个项目的委托。.
        /// </summary>
        private readonly Func<T, Task> processor;

        /// <summary>
        /// 最大并发度。.
        /// </summary>
        private readonly int maxConcurrency;

        /// <summary>
        /// 用于控制并发度的信号量。.
        /// </summary>
        private readonly SemaphoreSlim semaphore;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncStreamProcessor{T}"/> class.
        /// 初始化 <see cref="AsyncStreamProcessor{T}"/> 类的新实例。.
        /// </summary>
        /// <param name="processor">处理单个项目的委托。.</param>
        /// <param name="maxConcurrency">最大并发度，默认值为 10。.</param>
        public AsyncStreamProcessor(Func<T, Task> processor, int maxConcurrency = 10)
        {
            this.processor = processor;
            this.maxConcurrency = maxConcurrency;
            this.semaphore = new SemaphoreSlim(maxConcurrency);
        }

        /// <summary>
        /// 并行处理异步流中的所有项目。.
        /// </summary>
        /// <param name="items">要处理的异步流。.</param>
        /// <param name="cancellationToken">取消令牌，用于取消处理操作。.</param>
        /// <returns>表示异步处理操作的任务。.</returns>
        public async Task ProcessAsync(IAsyncEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();

            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                await this.semaphore.WaitAsync(cancellationToken);

                var task = Task.Run(
                    async () =>
                {
                    try
                    {
                        await this.processor(item);
                    }
                    finally
                    {
                        this.semaphore.Release();
                    }
                },
                    cancellationToken);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 并行转换异步流中的项目并返回转换后的结果流。.
        /// </summary>
        /// <typeparam name="TResult">转换后项目的类型。.</typeparam>
        /// <param name="items">要转换的异步流。.</param>
        /// <param name="transformer">将输入项目转换为输出项目的委托。.</param>
        /// <param name="cancellationToken">取消令牌，用于取消转换操作。.</param>
        /// <returns>转换后的异步流。.</returns>
        public async IAsyncEnumerable<TResult> TransformAsync<TResult>(
            IAsyncEnumerable<T> items,
            Func<T, Task<TResult>> transformer,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                await this.semaphore.WaitAsync(cancellationToken);

                var result = await Task.Run(
                    async () =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return await transformer(item);
                    }
                    finally
                    {
                        this.semaphore.Release();
                    }
                },
                    cancellationToken);

                yield return result;
            }
        }
    }
}