// <copyright file="PluginLoader.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Plugins;

using System.IO;
using System.Reflection;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// 插件加载器实现.
/// </summary>
public class PluginLoader : IPluginLoader
{
    /// <summary>
    /// 日志记录器实例.
    /// </summary>
    private readonly ILogger<PluginLoader> logger;

    /// <summary>
    /// 已加载的插件实例列表，用于存储所有成功加载和初始化的插件。.
    /// </summary>
    private readonly List<IPlugin> loadedPlugins = [];

    /// <summary>
    /// 已加载的插件程序集列表，用于跟踪和管理加载的插件程序集。.
    /// </summary>
    private readonly List<Assembly> loadedAssemblies = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoader"/> class.
    /// 初始化 <see cref="PluginLoader"/> 类的新实例.
    /// </summary>
    /// <param name="logger">日志记录器实例，用于记录插件加载过程中的日志信息.</param>
    /// <exception cref="ArgumentNullException">当 logger 参数为 null 时抛出.</exception>
    public PluginLoader(ILogger<PluginLoader> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger), "日志记录器不能为空");
    }

    /// <inheritdoc />
    /// <summary>
    /// 获取已加载的插件实例列表.
    /// </summary>
    /// <returns>已加载的插件实例列表.</returns>
    public IEnumerable<IPlugin> LoadedPlugins => this.loadedPlugins.AsReadOnly();

    /// <inheritdoc />
    /// <summary>
    /// 异步加载指定目录下的所有插件.
    /// </summary>
    /// <param name="pluginsDirectory">插件目录路径.</param>
    /// <returns>已加载的插件实例列表.</returns>
    public async Task<IEnumerable<IPlugin>> LoadPluginsAsync(string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            this.logger.LogWarning("Plugins directory not found: {Directory}", pluginsDirectory);
            return [];
        }

        var pluginFiles = Directory.GetFiles(pluginsDirectory, "*.dll");
        var loadedPlugins = new List<IPlugin>();

        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                var plugins = await this.LoadPluginFromAssemblyAsync(pluginFile);
                loadedPlugins.AddRange(plugins);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load plugin: {File}", pluginFile);
            }
        }

        this.logger.LogInformation("Loaded {Count} plugins from directory: {Directory}", loadedPlugins.Count, pluginsDirectory);
        return loadedPlugins;
    }

    /// <inheritdoc />
    /// <summary>
    /// 异步加载指定程序集中的所有插件.
    /// </summary>
    /// <param name="assemblyPath">插件程序集路径.</param>
    /// <returns>已加载的插件实例列表.</returns>
    public async Task<IEnumerable<IPlugin>> LoadPluginFromAssemblyAsync(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            this.logger.LogWarning("Plugin assembly not found: {Path}", assemblyPath);
            return [];
        }

        try
        {
            // 加载程序集
            var assembly = Assembly.LoadFrom(assemblyPath);
            this.loadedAssemblies.Add(assembly);

            // 查找实现了IPlugin接口的类型
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            var loadedPlugins = new List<IPlugin>();

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    // 创建插件实例
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;

                    // 初始化插件
                    await plugin.InitializeAsync();

                    this.loadedPlugins.Add(plugin);
                    loadedPlugins.Add(plugin);

                    this.logger.LogInformation(
                        "Loaded plugin: {Name} (v{Version}) by {Author}",
                        plugin.PluginName,
                        plugin.Version,
                        plugin.Author);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to initialize plugin type: {Type}", pluginType.FullName);
                }
            }

            return loadedPlugins;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to load plugin assembly: {Path}", assemblyPath);
            return [];
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// 根据插件类型获取所有已加载的插件实例.
    /// </summary>
    /// <param name="pluginType">插件类型.</param>
    /// <returns>符合指定类型的插件实例列表.</returns>
    public IEnumerable<IPlugin> GetPluginsByType(PluginType pluginType)
    {
        return this.loadedPlugins.Where(p => p.PluginType == pluginType);
    }

    /// <inheritdoc />
    /// <summary>
    /// 异步卸载所有已加载的插件.
    /// </summary>
    /// <returns>表示异步操作的任务.</returns>
    public async Task UnloadAllPluginsAsync()
    {
        foreach (var plugin in this.loadedPlugins)
        {
            try
            {
                await plugin.ShutdownAsync();
                this.logger.LogInformation("Unloaded plugin: {Name}", plugin.PluginName);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to unload plugin: {Name}", plugin.PluginName);
            }
        }

        this.loadedPlugins.Clear();
        this.loadedAssemblies.Clear();

        this.logger.LogInformation("Unloaded all plugins");
    }
}
