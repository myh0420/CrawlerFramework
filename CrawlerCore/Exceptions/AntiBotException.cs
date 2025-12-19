// <copyright file="AntiBotException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Exceptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 反爬虫检测异常.
    /// </summary>
    public class AntiBotException : CrawlerException
    {
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

        /// <summary>
        /// Gets 目标URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Gets a value indicating whether 检测结果.
        /// </summary>
        public bool IsBlocked { get; }
    }
}