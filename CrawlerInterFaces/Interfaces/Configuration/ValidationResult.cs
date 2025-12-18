// <copyright file="ValidationResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerInterFaces.Interfaces.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// 配置验证结果类，用于存储配置验证的结果信息.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether 配置验证是否通过.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets 配置验证过程中发现的错误信息列表.
        /// </summary>
        public List<string> Errors { get; set; } = [];

        /// <summary>
        /// Gets or sets 配置验证过程中发现的警告信息列表.
        /// </summary>
        public List<string> Warnings { get; set; } = [];
    }
}
