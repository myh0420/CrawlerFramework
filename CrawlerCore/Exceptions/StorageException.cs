// <copyright file="StorageException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Exceptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 存储异常.
    /// </summary>
    public class StorageException : CrawlerException
    {
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

        /// <summary>
        /// Gets 存储键.
        /// </summary>
        public string? Key { get; }
    }
}