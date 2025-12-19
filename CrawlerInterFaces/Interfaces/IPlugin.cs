// CrawlerInterFaces/Interfaces/IPlugin.cs
using System.Reflection;
using CrawlerFramework.CrawlerInterFaces.Interfaces;

namespace CrawlerFramework.CrawlerInterFaces.Interfaces;
/// <summary>
/// 插件接口，所有插件都必须实现此接口
/// </summary>
public interface IPlugin : ICrawlerComponent
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 插件描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 插件类型
    /// </summary>
    PluginType PluginType { get; }

    /// <summary>
    /// 插件作者
    /// </summary>
    string Author { get; }

    /// <summary>
    /// 插件入口点类型
    /// </summary>
    Type EntryPointType { get; }

    /// <summary>
    /// 插件优先级（数值越大，优先级越高）
    /// </summary>
    int Priority { get; }
}

/// <summary>
/// 插件类型枚举
/// </summary>
public enum PluginType
{
    /// <summary>
    /// 下载器插件
    /// </summary>
    Downloader,

    /// <summary>
    /// 解析器插件
    /// </summary>
    Parser,

    /// <summary>
    /// 提取器插件
    /// </summary>
    Extractor,

    /// <summary>
    /// 存储插件
    /// </summary>
    Storage,

    /// <summary>
    /// 调度器插件
    /// </summary>
    Scheduler,

    /// <summary>
    /// 其他类型插件
    /// </summary>
    Other
}

/// <summary>
/// 插件加载器接口
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// 加载指定目录中的所有插件
    /// </summary>
    /// <param name="pluginsDirectory">插件目录路径</param>
    /// <returns>已加载的插件列表</returns>
    Task<IEnumerable<IPlugin>> LoadPluginsAsync(string pluginsDirectory);

    /// <summary>
    /// 加载指定程序集中的插件
    /// </summary>
    /// <param name="assemblyPath">程序集路径</param>
    /// <returns>已加载的插件列表</returns>
    Task<IEnumerable<IPlugin>> LoadPluginFromAssemblyAsync(string assemblyPath);

    /// <summary>
    /// 获取指定类型的插件
    /// </summary>
    /// <param name="pluginType">插件类型</param>
    /// <returns>匹配的插件列表</returns>
    IEnumerable<IPlugin> GetPluginsByType(PluginType pluginType);

    /// <summary>
    /// 卸载所有插件
    /// </summary>
    Task UnloadAllPluginsAsync();

    /// <summary>
    /// 已加载的所有插件
    /// </summary>
    IEnumerable<IPlugin> LoadedPlugins { get; }
}