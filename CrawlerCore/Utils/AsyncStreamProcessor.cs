// CrawlerCore/Utils/AsyncStreamProcessor.cs
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CrawlerCore.Utils
{
    /// <summary>
    /// 异步流处理
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AsyncStreamProcessor<T>(Func<T, Task> processor, int maxConcurrency = 10)
    {
        private readonly Func<T, Task> _processor = processor;
        private readonly int _maxConcurrency = maxConcurrency;
        private readonly SemaphoreSlim _semaphore = new(maxConcurrency);

        public async Task ProcessAsync(IAsyncEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();

            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                await _semaphore.WaitAsync(cancellationToken);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await _processor(item);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        public async IAsyncEnumerable<TResult> TransformAsync<TResult>(
            IAsyncEnumerable<T> items, 
            Func<T, Task<TResult>> transformer,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                await _semaphore.WaitAsync(cancellationToken);
                
                var result = await Task.Run(async () =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return await transformer(item);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, cancellationToken);

                yield return result;
            }
        }
    }
}