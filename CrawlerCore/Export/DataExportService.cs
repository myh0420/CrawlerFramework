// <copyright file="DataExportService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Export
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CsvHelper;
    using Newtonsoft.Json;

    /// <summary>
    /// 数据导出服务，用于管理不同格式的数据导出器并提供统一的导出接口.
    /// </summary>
    public class DataExportService
    {
        /// <summary>
        /// 已注册的数据导出器集合.
        /// </summary>
        private readonly List<IDataExporter> exporters;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataExportService"/> class.
        /// 初始化 <see cref="DataExportService"/> 类的新实例.
        /// </summary>
        public DataExportService()
        {
            this.exporters = [
                new JsonExporter(),
                new CsvExporter(),
                new ExcelExporter()
            ];
        }

        /// <summary>
        /// 注册一个数据导出器.
        /// </summary>
        /// <param name="exporter">要注册的导出器实例.</param>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="exporter"/> 为 <see langword="null"/> 时引发.
        /// </exception>
        public void RegisterExporter(IDataExporter exporter)
        {
            this.exporters.Add(exporter ?? throw new ArgumentNullException(nameof(exporter)));
        }

        /// <summary>
        /// 异步导出数据到指定文件路径.
        /// </summary>
        /// <typeparam name="T">数据类型.</typeparam>
        /// <param name="data">要导出的数据.</param>
        /// <param name="filePath">导出文件的路径.</param>
        /// <returns>一个任务, 表示异步操作. 任务结果为一个布尔值, 表示导出是否成功.</returns>
        /// <exception cref="NotSupportedException">
        /// 当指定的文件扩展名不支持导出时引发.
        /// </exception>
        public async Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower().TrimStart('.');
            var exporter = this.exporters.FirstOrDefault(e =>
                e.SupportedFormat.Equals(extension, StringComparison.OrdinalIgnoreCase));

            return exporter == null
                ? throw new NotSupportedException($"Format '{extension}' is not supported")
                : await exporter.ExportAsync(data, filePath);
        }

        /// <summary>
        /// 获取当前服务支持的导出格式.
        /// </summary>
        /// <returns>一个可枚举的字符串集合, 包含所有支持的导出格式.</returns>
        public IEnumerable<string> GetSupportedFormats()
        {
            return this.exporters.Select(e => e.SupportedFormat);
        }
    }
}