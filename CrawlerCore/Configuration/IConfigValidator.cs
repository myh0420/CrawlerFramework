// <copyright file="IConfigValidator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// 配置验证服务接口，用于验证应用爬虫配置的有效性.
    /// </summary>
    public interface IConfigValidator
    {
        /// <summary>
        /// 验证应用爬虫配置的有效性，包括必填项检查、格式验证和业务规则验证.
        /// </summary>
        /// <param name="config">要验证的应用爬虫配置实例.</param>
        /// <returns>验证结果，包含验证是否通过、错误信息和警告信息.</returns>
        ValidationResult Validate(AppCrawlerConfig config);

        /// <summary>
        /// 获取应用爬虫配置的验证错误信息列表.
        /// </summary>
        /// <param name="config">要验证的应用爬虫配置实例.</param>
        /// <returns>验证错误信息列表.</returns>
        IEnumerable<string> GetValidationErrors(AppCrawlerConfig config);
    }
}