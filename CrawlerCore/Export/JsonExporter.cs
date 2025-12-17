// <copyright file="JsonExporter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Export
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CsvHelper;
    using Newtonsoft.Json;

    /// <summary>
    /// 实现JSON格式数据导出功能，使用Newtonsoft.Json库进行JSON序列化和文件写入.
    /// </summary>
    public class JsonExporter : IDataExporter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonExporter"/> class.
        /// 初始化 <see cref="JsonExporter"/> 类的新实例.
        /// </summary>
        public JsonExporter()
        {
        }

        /// <inheritdoc/>
        public string SupportedFormat => "json";

        /// <inheritdoc/>
        /// <summary>
        /// 将数据异步导出为JSON文件.
        /// </summary>
        /// <typeparam name="T">数据项的类型.</typeparam>
        /// <param name="data">要导出的数据集合.</param>
        /// <param name="filePath">要生成的JSON文件的完整路径.</param>
        /// <returns>
        /// 一个表示异步操作的任务，其结果为 <c>true</c> 如果导出成功；否则为 <c>false</c>.
        /// </returns>
        public async Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}