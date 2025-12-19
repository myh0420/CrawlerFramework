// <copyright file="CrawlerException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Exceptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 爬虫框架的基础异常类.
    /// </summary>
    public class CrawlerException : Exception
    {
        /// <summary>
        /// Gets 错误代码.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets 错误详情.
        /// </summary>
        public IDictionary<string, object> Details { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CrawlerException"/> class.
        /// 创建一个新的爬虫异常实例.
        /// </summary>
        /// <param name="message">错误消息.</param>
        /// <param name="errorCode">错误代码.</param>
        /// <param name="details">错误详情.</param>
        /// <param name="innerException">内部异常.</param>
        public CrawlerException(
            string message,
            string errorCode = "CRAWLER_ERROR",
            IDictionary<string, object>? details = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            this.ErrorCode = errorCode;
            this.Details = details ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 下载异常.
    /// </summary>
    public class DownloadException : CrawlerException
    {
        /// <summary>
        /// Gets 目标URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Gets hTTP状态码.
        /// </summary>
        public int? StatusCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadException"/> class.
        /// 创建一个新的下载异常实例.
        /// </summary>
        /// <param name="url">目标URL.</param>
        /// <param name="message">错误消息.</param>
        /// <param name="statusCode">HTTP状态码.</param>
        /// <param name="innerException">内部异常.</param>
        public DownloadException(
            string url,
            string message,
            int? statusCode = null,
            Exception? innerException = null)
            : base(message, "DOWNLOAD_ERROR", new Dictionary<string, object> { { "Url", url } }, innerException)
        {
            this.Url = url;
            this.StatusCode = statusCode;
            if (statusCode.HasValue)
            {
                this.Details["StatusCode"] = statusCode.Value;
            }
        }
    }

    /// <summary>
    /// 解析异常.
    /// </summary>
    public class ParseException : CrawlerException
    {
        /// <summary>
        /// Gets 目标URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParseException"/> class.
        /// 创建一个新的解析异常实例.
        /// </summary>
        /// <param name="url">目标URL.</param>
        /// <param name="message">错误消息.</param>
        /// <param name="innerException">内部异常.</param>
        public ParseException(
            string url,
            string message,
            Exception? innerException = null)
            : base(message, "PARSE_ERROR", new Dictionary<string, object> { { "Url", url } }, innerException)
        {
            this.Url = url;
        }
    }

    /// <summary>
    /// 存储异常.
    /// </summary>
    public class StorageException : CrawlerException
    {
        /// <summary>
        /// Gets 存储键.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageException"/> class.
        /// 创建一个新的存储异常实例.
        /// </summary>
        /// <param name="message">错误消息.</param>
        /// <param name="key">存储键.</param>
        /// <param name="innerException">内部异常.</param>
        public StorageException(
            string message,
            string? key = null,
            Exception? innerException = null)
            : base(message, "STORAGE_ERROR", key != null ? new Dictionary<string, object> { { "Key", key } } : null, innerException)
        {
            this.Key = key;
        }
    }

    /// <summary>
    /// 配置异常.
    /// </summary>
    public class ConfigException : CrawlerException
    {
        /// <summary>
        /// Gets 配置项名称.
        /// </summary>
        public string? ConfigName { get; }

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
    }

    /// <summary>
    /// 反爬虫检测异常.
    /// </summary>
    public class AntiBotException : CrawlerException
    {
        /// <summary>
        /// Gets 目标URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Gets a value indicating whether 检测结果.
        /// </summary>
        public bool IsBlocked { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AntiBotException"/> class.
        /// 创建一个新的反爬虫检测异常实例.
        /// </summary>
        /// <param name="url">目标URL.</param>
        /// <param name="isBlocked">是否被阻止.</param>
        /// <param name="message">错误消息.</param>
        /// <param name="innerException">内部异常.</param>
        public AntiBotException(
            string url,
            bool isBlocked,
            string message,
            Exception? innerException = null)
            : base(message, "ANTIBOT_ERROR", new Dictionary<string, object> { { "Url", url }, { "IsBlocked", isBlocked } }, innerException)
        {
            this.Url = url;
            this.IsBlocked = isBlocked;
        }
    }

    /// <summary>
    /// Robots.txt检查异常.
    /// </summary>
    public class RobotsTxtException : CrawlerException
    {
        /// <summary>
        /// Gets 目标URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RobotsTxtException"/> class.
        /// 创建一个新的Robots.txt检查异常实例.
        /// </summary>
        /// <param name="url">目标URL.</param>
        /// <param name="message">错误消息.</param>
        /// <param name="innerException">内部异常.</param>
        public RobotsTxtException(
            string url,
            string message,
            Exception? innerException = null)
            : base(message, "ROBOTS_TXT_ERROR", new Dictionary<string, object> { { "Url", url } }, innerException)
        {
            this.Url = url;
        }
    }

    /// <summary>
    /// 插件异常.
    /// </summary>
    public class PluginException : CrawlerException
    {
        /// <summary>
        /// Gets 插件名称.
        /// </summary>
        public string PluginName { get; }

        /// <summary>
        /// Gets 插件类型.
        /// </summary>
        public string PluginType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginException"/> class.
        /// 创建一个新的插件异常实例.
        /// </summary>
        /// <param name="pluginName">插件名称.</param>
        /// <param name="pluginType">插件类型.</param>
        /// <param name="message">错误消息.</param>
        /// <param name="innerException">内部异常.</param>
        public PluginException(
            string pluginName,
            string pluginType,
            string message,
            Exception? innerException = null)
            : base(message, "PLUGIN_ERROR", new Dictionary<string, object> { { "PluginName", pluginName }, { "PluginType", pluginType } }, innerException)
        {
            this.PluginName = pluginName;
            this.PluginType = pluginType;
        }
    }
}
