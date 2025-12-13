// CrawlerCore/Exceptions/CrawlerException.cs
using System;
using System.Collections.Generic;

namespace CrawlerCore.Exceptions
{
    /// <summary>
    /// 爬虫框架的基础异常类
    /// </summary>
    public class CrawlerException : Exception
    {
        /// <summary>
        /// 错误代码
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// 错误详情
        /// </summary>
        public IDictionary<string, object> Details { get; }

        /// <summary>
        /// 创建一个新的爬虫异常实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="errorCode">错误代码</param>
        /// <param name="details">错误详情</param>
        /// <param name="innerException">内部异常</param>
        public CrawlerException(
            string message,
            string errorCode = "CRAWLER_ERROR",
            IDictionary<string, object>? details = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Details = details ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 下载异常
    /// </summary>
    public class DownloadException : CrawlerException
    {
        /// <summary>
        /// 目标URL
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// HTTP状态码
        /// </summary>
        public int? StatusCode { get; }

        /// <summary>
        /// 创建一个新的下载异常实例
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <param name="message">错误消息</param>
        /// <param name="statusCode">HTTP状态码</param>
        /// <param name="innerException">内部异常</param>
        public DownloadException(
            string url,
            string message,
            int? statusCode = null,
            Exception? innerException = null)
            : base(message, "DOWNLOAD_ERROR", new Dictionary<string, object> { { "Url", url } }, innerException)
        {
            Url = url;
            StatusCode = statusCode;
            if (statusCode.HasValue)
            {
                Details["StatusCode"] = statusCode.Value;
            }
        }
    }

    /// <summary>
    /// 解析异常
    /// </summary>
    public class ParseException : CrawlerException
    {
        /// <summary>
        /// 目标URL
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// 创建一个新的解析异常实例
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <param name="message">错误消息</param>
        /// <param name="innerException">内部异常</param>
        public ParseException(
            string url,
            string message,
            Exception? innerException = null)
            : base(message, "PARSE_ERROR", new Dictionary<string, object> { { "Url", url } }, innerException)
        {
            Url = url;
        }
    }

    /// <summary>
    /// 存储异常
    /// </summary>
    public class StorageException : CrawlerException
    {
        /// <summary>
        /// 存储键
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// 创建一个新的存储异常实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="key">存储键</param>
        /// <param name="innerException">内部异常</param>
        public StorageException(
            string message,
            string? key = null,
            Exception? innerException = null)
            : base(message, "STORAGE_ERROR", key != null ? new Dictionary<string, object> { { "Key", key } } : null, innerException)
        {
            Key = key;
        }
    }

    /// <summary>
    /// 配置异常
    /// </summary>
    public class ConfigException : CrawlerException
    {
        /// <summary>
        /// 配置项名称
        /// </summary>
        public string? ConfigName { get; }

        /// <summary>
        /// 创建一个新的配置异常实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="configName">配置项名称</param>
        /// <param name="innerException">内部异常</param>
        public ConfigException(
            string message,
            string? configName = null,
            Exception? innerException = null)
            : base(message, "CONFIG_ERROR", configName != null ? new Dictionary<string, object> { { "ConfigName", configName } } : null, innerException)
        {
            ConfigName = configName;
        }
    }

    /// <summary>
    /// 反爬虫检测异常
    /// </summary>
    public class AntiBotException : CrawlerException
    {
        /// <summary>
        /// 目标URL
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// 检测结果
        /// </summary>
        public bool IsBlocked { get; }

        /// <summary>
        /// 创建一个新的反爬虫检测异常实例
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <param name="isBlocked">是否被阻止</param>
        /// <param name="message">错误消息</param>
        /// <param name="innerException">内部异常</param>
        public AntiBotException(
            string url,
            bool isBlocked,
            string message,
            Exception? innerException = null)
            : base(message, "ANTIBOT_ERROR", new Dictionary<string, object> { { "Url", url }, { "IsBlocked", isBlocked } }, innerException)
        {
            Url = url;
            IsBlocked = isBlocked;
        }
    }
}
