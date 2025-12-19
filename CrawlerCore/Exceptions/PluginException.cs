// <copyright file="PluginException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Exceptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 插件异常.
    /// </summary>
    public class PluginException : CrawlerException
    {
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

        /// <summary>
        /// Gets 插件名称.
        /// </summary>
        public string PluginName { get; }

        /// <summary>
        /// Gets 插件类型.
        /// </summary>
        public string PluginType { get; }
    }
}