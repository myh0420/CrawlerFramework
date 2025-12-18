// <copyright file="IDataExporter.cs" company="PlaceholderCompany">
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
    /// 数据导出器接口，定义了将数据导出到不同格式文件的标准方法.
    /// 实现此接口可以创建支持不同格式的导出器，如JSON、CSV、Excel等.
    /// </summary>
    public interface IDataExporter
    {
        /// <summary>
        /// 获取导出器支持的文件格式扩展名（不包含点号）.
        /// 例如："json"、"csv"、"xlsx"等.
        /// </summary>
        string SupportedFormat { get; }

        /// <summary>
        /// 异步将数据导出到指定路径的文件中.
        /// </summary>
        /// <typeparam name="T">要导出的数据类型.</typeparam>
        /// <param name="data">要导出的数据集合.</param>
        /// <param name="filePath">导出文件的完整路径，包括文件名和扩展名.</param>
        /// <returns>
        /// 一个表示异步操作的任务，其结果为布尔值：
        /// <c>true</c> 表示数据导出成功；
        /// <c>false</c> 表示数据导出失败.
        /// </returns>
        Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath);
    }
}