// CrawlerCore/Utils/ServiceLocator.cs
using Microsoft.Extensions.DependencyInjection;

namespace CrawlerCore.Utils
{
    /// <summary>
    /// 服务定位器（用于在不支持依赖注入的地方获取服务）
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider = null;

        /// <summary>
        /// 设置服务提供者
        /// </summary>
        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 获取服务
        /// </summary>
        public static T? GetService<T>() where T : class
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider has not been set. Call SetServiceProvider first.");
            }
            
            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// 获取必需的服务
        /// </summary>
        public static T GetRequiredService<T>() where T : class
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider has not been set. Call SetServiceProvider first.");
            }
            
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}