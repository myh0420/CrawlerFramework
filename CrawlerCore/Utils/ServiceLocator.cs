// <copyright file="ServiceLocator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Utils
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// 服务定位器（用于在不支持依赖注入的地方获取服务）.
    /// </summary>
    public static class ServiceLocator
    {
        /// <summary>
        /// 服务提供者实例.
        /// </summary>
        private static IServiceProvider? serviceProvider = null;

        /// <summary>
        /// 设置服务提供者.
        /// </summary>
        /// <param name="serviceProvider">服务提供者实例.</param>
        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            ServiceLocator.serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 获取服务.
        /// </summary>
        /// <typeparam name="T">服务类型.</typeparam>
        /// <returns>服务实例（如果存在），否则为 null.</returns>
        public static T? GetService<T>()
            where T : class
        {
            if (serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider has not been set. Call SetServiceProvider first.");
            }

            return serviceProvider.GetService<T>();
        }

        /// <summary>
        /// 获取必需的服务.
        /// </summary>
        /// <typeparam name="T">服务类型.</typeparam>
        /// <returns>服务实例（如果存在），否则抛出 InvalidOperationException.</returns>
        public static T GetRequiredService<T>()
            where T : class
        {
            if (serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider has not been set. Call SetServiceProvider first.");
            }

            return serviceProvider.GetRequiredService<T>();
        }
    }
}