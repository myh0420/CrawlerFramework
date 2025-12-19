// <copyright file="ConfigException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Exceptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 配置异常.
    /// </summary>
    public class ConfigException : CrawlerException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigException"/> class.
        /// 创建一个新的配置异常实例.
        /// </summary>
        /// <param name="message">错误消息.</param>
        /// <param name="configName">配置项名称.</param>
        /// <param name="innerException">内部异常.</param>
        public ConfigException(
            string message,
            string? configName = null,
            Exception? innerException = null)
            : base(message, "CONFIG_ERROR", configName != null ? new Dictionary<string, object> { { "ConfigName", configName } } : null, innerException)
        {
            this.ConfigName = configName;
        }

        /// <summary>
        /// Gets 配置项名称.
        /// </summary>
        public string? ConfigName { get; }
    }
}