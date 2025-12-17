// <copyright file="RobotsTxt.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Robots
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
    /// 表示 robots.txt 文件的解析结果.
    /// </summary>
    public class RobotsTxt
    {
        /// <summary>
        /// 存储解析后的robots.txt规则列表.
        /// </summary>
        private readonly List<RobotsRule> rules;

        /// <summary>
        /// 初始化 <see cref="RobotsTxt"/> 类的新实例.
        /// </summary>
        /// <param name="content">robots.txt 文件的内容.</param>
        public RobotsTxt(string content)
        {
            this.rules = ParseContent(content);
        }

        /// <summary>
        /// 检查 URL 是否被允许.
        /// </summary>
        /// <param name="url">要检查的 URL.</param>
        /// <param name="userAgent">用户代理. 默认值为 "*".</param>
        /// <returns>如果 URL 被允许，则为 true；否则为 false.</returns>
        public bool IsAllowed(string url, string userAgent = "*")
        {
            var path = new Uri(url).AbsolutePath;
            var applicableRules = this.rules.Where(r =>
                r.UserAgent == "*" || r.UserAgent.Equals(userAgent, StringComparison.OrdinalIgnoreCase));

            foreach (var rule in applicableRules)
            {
                if (path.StartsWith(rule.Path))
                {
                    return rule.Allow;
                }
            }

            return true; // 默认允许
        }

        /// <summary>
        /// 解析 robots.txt 文件内容.
        /// </summary>
        /// <param name="content">robots.txt 文件的内容.</param>
        /// <returns>解析后的规则列表.</returns>
        private static List<RobotsRule> ParseContent(string content)
        {
            var rules = new List<RobotsRule>();
            var lines = content.Split('\n');
            string currentUserAgent = "*";

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                {
                    currentUserAgent = trimmed[11..].Trim();
                }
                else if (trimmed.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = trimmed[9..].Trim();
                    rules.Add(new RobotsRule { UserAgent = currentUserAgent, Path = path, Allow = false });
                }
                else if (trimmed.StartsWith("Allow:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = trimmed[6..].Trim();
                    rules.Add(new RobotsRule { UserAgent = currentUserAgent, Path = path, Allow = true });
                }
            }

            return rules;
        }
    }
}