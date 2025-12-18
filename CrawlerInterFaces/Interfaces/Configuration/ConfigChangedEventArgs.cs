// <copyright file="ConfigChangedEventArgs.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerInterFaces.Interfaces.Configuration
{
    using System;
    using CrawlerFramework.CrawlerEntity.Configuration;

    /// <summary>
    /// 配置改变事件参数.
    /// </summary>
    public class ConfigChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets 新配置.
        /// </summary>
        public AppCrawlerConfig NewConfig { get; set; } = new();

        /// <summary>
        /// Gets or sets 旧配置.
        /// </summary>
        public AppCrawlerConfig OldConfig { get; set; } = new();
    }
}
