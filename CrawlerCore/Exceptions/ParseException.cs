// <copyright file="ParseException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Exceptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 解析异常.
    /// </summary>
    public class ParseException : CrawlerException
    {
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

        /// <summary>
        /// Gets 目标URL.
        /// </summary>
        public string Url { get; }
    }
}