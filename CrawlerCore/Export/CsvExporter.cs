// <copyright file="CsvExporter.cs" company="PlaceholderCompany">
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
    /// 实现CSV格式数据导出功能，使用CsvHelper库进行CSV文件的生成和写入.
    /// </summary>
    public class CsvExporter : IDataExporter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CsvExporter"/> class.
        /// 初始化 <see cref="CsvExporter"/> 类的新实例.
        /// </summary>
        public CsvExporter()
        {
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
            try
            {
                using var writer = new StreamWriter(filePath);
                using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);

                await csv.WriteRecordsAsync(data);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}