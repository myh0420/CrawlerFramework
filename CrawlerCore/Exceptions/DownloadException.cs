// <copyright file="DownloadException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Exceptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 下载异常.
    /// </summary>
    public class DownloadException : CrawlerException
    {
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

        /// <summary>
        /// Gets 目标URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Gets hTTP状态码.
        /// </summary>
        public int? StatusCode { get; }
    }
}