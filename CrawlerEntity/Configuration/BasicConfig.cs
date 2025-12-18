// <copyright file="BasicConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    // 配置子节类

    /// <summary>
    /// 基础配置节.
    /// </summary>
    public class BasicConfig
    {
        /// <summary>
        /// Gets or sets 最大并发任务数.
        /// </summary>
        public int MaxConcurrentTasks { get; set; } = 10;

        /// <summary>
        /// Gets or sets 最大深度.
        /// </summary>
        public int MaxDepth { get; set; } = 3;

        /// <summary>
        /// Gets or sets 最大页面数.
        /// </summary>
        public int MaxPages { get; set; } = 1000;

        /// <summary>
        /// Gets or sets 请求延迟.
        /// </summary>
        public TimeSpan RequestDelay { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Gets or sets 请求超时时间.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether 是否遵守 robots.txt.
        /// </summary>
        public bool RespectRobotsTxt { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether 是否遵循重定向.
        /// </summary>
        public bool FollowRedirects { get; set; } = true;
    }
}