// <copyright file="CrawlerException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Exceptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 爬虫框架的基础异常类.
    /// </summary>
    public class CrawlerException : Exception
    {
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

        /// <summary>
        /// Gets 错误代码.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets 错误详情.
        /// </summary>
        public IDictionary<string, object> Details { get; }
    }
}
