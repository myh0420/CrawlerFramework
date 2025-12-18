// <copyright file="PluginBase.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Plugins;

using System.IO;
using System.Reflection;
using CrawlerFramework.CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// 插件基类，提供默认实现.
/// </summary>
public abstract class PluginBase : IPlugin
{
    /// <inheritdoc />
    /// <summary>
    /// 获取插件名称.
    /// </summary>
    public abstract string PluginName { get; }

    /// <inheritdoc />
    /// <summary>
    /// 获取插件版本.
    /// </summary>
    public abstract string Version { get; }

    /// <inheritdoc />
    /// <summary>
    /// 获取插件描述.
    /// </summary>
    public abstract string Description { get; }

    /// <inheritdoc />
    /// <summary>
    /// 获取插件类型.
    /// </summary>
    public abstract PluginType PluginType { get; }

    /// <inheritdoc />
    /// <summary>
    /// 获取插件作者.
    /// </summary>
    public abstract string Author { get; }

    /// <inheritdoc />
    /// <summary>
    /// 获取插件入口点类型.
    /// </summary>
    public Type EntryPointType => this.GetType();

    /// <inheritdoc />
    /// <summary>
    /// 异步初始化插件.
    /// </summary>
    /// <returns>表示异步操作的任务.</returns>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <summary>
    /// 异步卸载插件.
    /// </summary>
    /// <returns>表示异步操作的任务.</returns>
    public virtual Task ShutdownAsync()
    {
        return Task.CompletedTask;
    }
}