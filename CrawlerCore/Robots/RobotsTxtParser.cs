// <copyright file="RobotsTxtParser.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Robots
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Robots.txt解析器，用于检查URL是否被网站robots.txt规则允许访问.
    /// </summary>
    public class RobotsTxtParser : ICrawlerComponent
    {
        /// <summary>
        /// 日志记录器实例.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// HTTP客户端实例，用于下载robots.txt文件.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// 缓存的robots.txt内容，键为域名，值为解析后的RobotsTxt对象.
        /// </summary>
        private readonly Dictionary<string, RobotsTxt> cache = [];

        /// <summary>
        /// 表示是否内部创建了 HTTP 客户端.
        /// </summary>
        private readonly bool httpClientCreatedInternally;

        /// <summary>
        /// 表示是否已初始化.
        /// </summary>
        private bool isInitialized = false;

        /// <summary>
        /// 初始化 <see cref="RobotsTxtParser"/> 类的新实例.
        /// </summary>
        /// <param name="logger">日志记录器实例.</param>
        /// <param name="httpClient">HTTP客户端实例，用于下载robots.txt文件.</param>
        public RobotsTxtParser(ILogger<RobotsTxtParser>? logger = null, HttpClient? httpClient = null)
        {
            this.logger = logger ?? new Logger<RobotsTxtParser>(new LoggerFactory());
            this.httpClient = httpClient ?? new HttpClient();
            this.httpClientCreatedInternally = httpClient == null;
        }

        /// <summary>
        /// 检查 URL 是否被允许.
        /// </summary>
        /// <param name="url">要检查的 URL.</param>
        /// <param name="userAgent">用户代理. 默认值为 "*".</param>
        /// <returns>如果 URL 被允许，则为 true；否则为 false.</returns>
        public async Task<bool> IsAllowedAsync(string url, string userAgent = "*")
        {
            try
            {
                var uri = new Uri(url);
                var domain = $"{uri.Scheme}://{uri.Host}";

                if (!this.cache.ContainsKey(domain))
                {
                    await this.LoadRobotsTxtAsync(domain);
                }

                if (this.cache.TryGetValue(domain, out var robotsTxt))
                {
                    return robotsTxt.IsAllowed(url, userAgent);
                }

                return true; // 如果没有 robots.txt，默认允许
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to check robots.txt for {Url}", url);
                return true; // 出错时默认允许
            }
        }

        /// <summary>
        /// 清除缓存.
        /// </summary>
        public void ClearCache()
        {
            this.cache.Clear();
        }

        /// <inheritdoc/>
        /// <summary>
        /// 初始化组件.
        /// </summary>
        public Task InitializeAsync()
        {
            if (!this.isInitialized)
            {
                this.isInitialized = true;
                this.logger.LogDebug("RobotsTxtParser initialized successfully");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        /// <summary>
        /// 关闭组件.
        /// </summary>
        public Task ShutdownAsync()
        {
            if (this.isInitialized)
            {
                this.isInitialized = false;
                this.ClearCache();

                if (this.httpClientCreatedInternally)
                {
                    this.httpClient.Dispose();
                    this.logger.LogDebug("Disposed internal HttpClient");
                }

                this.logger.LogDebug("RobotsTxtParser shutdown successfully");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 异步加载 robots.txt 内容.
        /// </summary>
        /// <param name="domain">域名.</param>
        private async Task LoadRobotsTxtAsync(string domain)
        {
            try
            {
                var robotsUrl = $"{domain}/robots.txt";
                var response = await this.httpClient.GetAsync(robotsUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    this.cache[domain] = new RobotsTxt(content);
                    this.logger.LogDebug("Loaded robots.txt for {Domain}", domain);
                }
                else
                {
                    this.cache[domain] = new RobotsTxt(string.Empty); // 空的 robots.txt
                    this.logger.LogDebug("No robots.txt found for {Domain}", domain);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to load robots.txt for {Domain}", domain);
                this.cache[domain] = new RobotsTxt(string.Empty); // 出错时创建空的
            }
        }
    }
}