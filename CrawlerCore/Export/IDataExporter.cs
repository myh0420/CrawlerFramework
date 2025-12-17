// <copyright file="IDataExporter.cs" company="PlaceholderCompany">
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
    /// 数据导出功能.
    /// </summary>
    public interface IDataExporter
    {
        /// <summary>
        /// Gets 获取支持的导出格式.
        /// </summary>
        string SupportedFormat { get; }

        /// <summary>
        /// 异步导出数据.
        /// </summary>
        /// <typeparam name="T">数据类型.</typeparam>
        /// <param name="data">要导出的数据.</param>
        /// <param name="filePath">导出文件路径.</param>
        /// <returns>导出是否成功.</returns>
        Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath);
    }
}