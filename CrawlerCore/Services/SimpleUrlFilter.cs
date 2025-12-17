// <copyright file="SimpleUrlFilter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using CrawlerInterFaces.Interfaces;

    /// <summary>
    /// 简单的URL过滤器实现，用于过滤爬虫要访问的URL，支持域名白名单和正则表达式模式过滤.
    /// </summary>
    public class SimpleUrlFilter : IUrlFilter
    {
        /// <summary>
        /// 允许的域名集合.
        /// </summary>
        private readonly HashSet<string> _allowedDomains = [];

        /// <summary>
        /// 阻止的URL模式列表.
        /// </summary>
        private readonly List<Regex> _blockedPatterns = [];

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
            if (_allowedDomains.Count != 0)
            {
                var domain = GetDomain(url);
                if (!_allowedDomains.Contains(domain))
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
            _allowedDomains.Add(domain.ToLowerInvariant());
        }

        /// <summary>
        /// 添加阻止的URL模式.
        /// </summary>
        /// <param name="pattern">要添加的正则表达式模式.</param>
        public void AddBlockedPattern(string pattern)
        {
            _blockedPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase));
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
    }
}
