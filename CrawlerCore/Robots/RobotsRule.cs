// <copyright file="RobotsRule.cs" company="PlaceholderCompany">
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
    using CrawlerFramework.CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 表示 robots.txt 文件中的单个规则，定义特定用户代理对特定路径的访问权限。
    /// </summary>
    public class RobotsRule
    {
        /// <summary>
        /// Gets or sets 获取或设置规则适用的用户代理。
        /// 可以是特定的用户代理名称，或使用 "*" 表示适用于所有用户代理。
        /// </summary>
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets 获取或设置规则适用的 URL 路径。
        /// 路径匹配采用前缀匹配方式，例如 "/admin" 匹配 "/admin", "/admin/", "/admin/page" 等。
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether 获取或设置一个值，指示该规则是允许还是禁止访问指定路径。
        /// true 表示允许访问，false 表示禁止访问。
        /// </summary>
        public bool Allow { get; set; }
    }
}