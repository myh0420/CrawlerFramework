// <copyright file="ErrorViewModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerMonitor.Models
{
    /// <summary>
    /// 错误视图模型.
    /// </summary>
    public class ErrorViewModel
    {
        /// <summary>
        /// Gets or sets 请求ID.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Gets a value indicating whether 是否显示请求ID.
        /// </summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(this.RequestId);
    }
}
