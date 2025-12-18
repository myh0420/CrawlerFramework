// <copyright file="DataProcessingConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// 数据处理配置节.
    /// </summary>
    public class DataProcessingConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether 是否移除重复内容.
        /// </summary>
        public bool RemoveDuplicateContent { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether 是否移除脚本和样式.
        /// </summary>
        public bool RemoveScriptsAndStyles { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether 是否归一化文本.
        /// </summary>
        public bool NormalizeText { get; set; } = true;

        /// <summary>
        /// Gets or sets 最小内容长度.
        /// </summary>
        public int MinContentLength { get; set; } = 100;
    }
}