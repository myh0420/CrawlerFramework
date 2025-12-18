// <copyright file="StorageConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// 存储配置节，用于配置爬虫爬取数据的存储方式.
    /// </summary>
    public class StorageConfig
    {
        /// <summary>
        /// Gets or sets 存储类型，可选值：FileSystem（文件系统）、Database（数据库）或 None（不存储）.
        /// </summary>
        public string Type { get; set; } = "FileSystem";

        /// <summary>
        /// Gets or sets 文件系统存储路径，当Type为FileSystem时有效.
        /// </summary>
        public string FileSystemPath { get; set; } = "crawler_data";

        /// <summary>
        /// Gets or sets 数据库连接字符串，当Type为Database时有效.
        /// </summary>
        public string DatabaseConnection { get; set; } = "Data Source=crawler.db";
    }
}