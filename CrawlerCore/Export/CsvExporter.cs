// <copyright file="CsvExporter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Export
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CsvHelper;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// 实现CSV格式数据导出功能，使用CsvHelper库进行CSV文件的生成和写入.
    /// </summary>
    public class CsvExporter : IDataExporter
    {
        /// <summary>
        /// 内部日志记录器实例.
        /// </summary>
        private readonly ILogger<CsvExporter>? logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CsvExporter"/> class.
        /// 初始化 <see cref="CsvExporter"/> 类的新实例.
        /// </summary>
        /// <param name="logger">日志记录器实例（可选）.</param>
        public CsvExporter(ILogger<CsvExporter>? logger = null)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public string SupportedFormat => "csv";

        /// <inheritdoc/>
        /// <summary>
        /// 将数据异步导出为CSV文件.
        /// </summary>
        /// <typeparam name="T">数据项的类型.</typeparam>
        /// <param name="data">要导出的数据集合.</param>
        /// <param name="filePath">要生成的CSV文件的完整路径.</param>
        /// <returns>
        /// 一个表示异步操作的任务，其结果为 <c>true</c> 如果导出成功；否则为 <c>false</c>.
        /// </returns>
        public async Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath)
        {
            if (data == null)
            {
                logger?.LogError("数据集合为空，无法导出为CSV格式");
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                logger?.LogError("文件路径为空，无法导出为CSV格式");
                return false;
            }

            try
            {
                logger?.LogInformation("开始将数据导出为CSV格式，文件路径: {FilePath}", filePath);

                // 确保目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

                // 使用流式处理减少内存占用
                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                using var streamWriter = new StreamWriter(stream);
                using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

                await csvWriter.WriteRecordsAsync(data);

                logger?.LogInformation("CSV数据导出成功，文件路径: {FilePath}", filePath);
                return true;
            }
            catch (ArgumentException ex)
            {
                logger?.LogError(ex, "CSV导出参数错误，文件路径: {FilePath}", filePath);
                return false;
            }
            catch (IOException ex)
            {
                logger?.LogError(ex, "CSV导出文件IO错误，文件路径: {FilePath}", filePath);
                return false;
            }
            catch (CsvHelperException ex)
            {
                logger?.LogError(ex, "CSV序列化错误，文件路径: {FilePath}", filePath);
                return false;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CSV导出发生未知错误，文件路径: {FilePath}", filePath);
                return false;
            }
        }
    }
}