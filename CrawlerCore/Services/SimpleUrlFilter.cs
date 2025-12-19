// <copyright file="SimpleUrlFilter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;

    /// <summary>
    /// 简单的URL过滤器实现，用于过滤爬虫要访问的URL，支持域名白名单和正则表达式模式过滤.
    /// </summary>
    public class SimpleUrlFilter : IUrlFilter
    {
        /// <summary>
        /// 允许的域名集合，使用ConcurrentDictionary实现线程安全的访问和更新.
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> _allowedDomains = new ();

        /// <summary>
        /// 阻止的URL模式列表，使用预编译的正则表达式提高性能.
        /// </summary>
        private readonly ConcurrentBag<Regex> _blockedPatterns = [];

        /// <summary>
        /// 配置版本号，用于跟踪配置的更新.
        /// </summary>
        private int _configVersion = 0;

        /// <summary>
        /// 检查URL是否被允许.
        /// </summary>
        /// <param name="url">要检查的URL.</param>
        /// <returns>如果URL被允许，则为true；否则为false.</returns>
        public bool IsAllowed(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            // 检查协议
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                return false;
            }

            // 检查域名
            if (!this._allowedDomains.IsEmpty)
            {
                var domain = GetDomain(url);
                if (!_allowedDomains.ContainsKey(domain))
                    return false;
            }

            // 检查阻止模式
            if (_blockedPatterns.Any(pattern => pattern.IsMatch(url)))
                return false;

            return true;
        }

        /// <summary>
        /// 添加允许的域名.
        /// </summary>
        /// <param name="domain">要添加的域名.</param>
        public void AddAllowedDomain(string domain)
        {
            _allowedDomains.TryAdd(domain.ToLowerInvariant(), true);
            IncrementConfigVersion();
        }

        /// <summary>
        /// 添加阻止的URL模式.
        /// </summary>
        /// <param name="pattern">要添加的正则表达式模式.</param>
        public void AddBlockedPattern(string pattern)
        {
            _blockedPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            IncrementConfigVersion();
        }

        /// <summary>
        /// 加载域名配置，替换当前的允许域名和阻止模式.
        /// </summary>
        /// <param name="allowedDomains">允许的域名列表.</param>
        /// <param name="blockedPatterns">阻止的URL模式列表.</param>
        public void LoadDomainsConfig(string[] allowedDomains, string[] blockedPatterns)
        {
            // 清空当前配置
            this._allowedDomains.Clear();
            while (this._blockedPatterns.TryTake(out _))
            {
            }

            // 加载允许的域名
            foreach (var domain in allowedDomains)
            {
                if (!string.IsNullOrWhiteSpace(domain))
                {
                    _allowedDomains.TryAdd(domain.ToLowerInvariant(), true);
                }
            }

            // 加载阻止的模式并预编译
            foreach (var pattern in blockedPatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    _blockedPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
                }
            }

            IncrementConfigVersion();
        }

        /// <summary>
        /// 获取当前配置版本号.
        /// </summary>
        /// <returns>配置版本号.</returns>
        public int GetConfigVersion()
        {
            return _configVersion;
        }

        /// <summary>
        /// 从URL中提取域名.
        /// </summary>
        /// <param name="url">要提取域名的URL.</param>
        /// <returns>提取的域名，如果提取失败则返回空字符串.</returns>
        private static string GetDomain(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 增加配置版本号.
        /// </summary>
        private void IncrementConfigVersion()
        {
            // 使用Interlocked.Increment确保线程安全
            System.Threading.Interlocked.Increment(ref _configVersion);
        }
    }
}
