// <copyright file="ExcelExporter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Export
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using OfficeOpenXml;

    /// <summary>
    /// 实现Excel格式数据导出功能，使用EPPlus库进行Excel文件的生成和写入.
    /// </summary>
    public class ExcelExporter : IDataExporter
    {
        /// <summary>
        /// 内部日志记录器实例.
        /// </summary>
        private readonly ILogger<ExcelExporter>? logger;

        /// <summary>
        /// 初始化 <see cref="ExcelExporter"/> 类的新实例.
        /// 设置EPPlus库的许可证上下文为非商业用途.
        /// </summary>
        /// <param name="logger">日志记录器实例（可选）.</param>
        public ExcelExporter(ILogger<ExcelExporter>? logger = null)
        {
            // 设置EPPlus的许可证上下文
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public string SupportedFormat => "xlsx";

        /// <inheritdoc/>
        /// <summary>
        /// 将数据异步导出为Excel文件.
        /// </summary>
        /// <typeparam name="T">数据项的类型.</typeparam>
        /// <param name="data">要导出的数据集合.</param>
        /// <param name="filePath">要生成的Excel文件的完整路径.</param>
        /// <returns>
        /// 一个表示异步操作的任务，其结果为 <c>true</c> 如果导出成功；否则为 <c>false</c>.
        /// </returns>
        public async Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath)
        {
            if (data == null)
            {
                logger?.LogError("数据集合为空，无法导出为Excel格式");
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                logger?.LogError("文件路径为空，无法导出为Excel格式");
                return false;
            }

            try
            {
                logger?.LogInformation("开始将数据导出为Excel格式，文件路径: {FilePath}", filePath);
                
                // 创建Excel包
                using var package = new ExcelPackage();

                // 添加工作表
                var worksheet = package.Workbook.Worksheets.Add(typeof(T).Name);

                // 获取数据的属性信息
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // 写入表头
                for (int i = 0; i < properties.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = properties[i].Name;
                    // 设置表头样式
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // 写入数据（使用流式方式处理大数据）
                int row = 2;
                foreach (var item in data)
                {
                    for (int i = 0; i < properties.Length; i++)
                    {
                        var value = properties[i].GetValue(item);
                        worksheet.Cells[row, i + 1].Value = value;
                    }
                    row++;

                    // 每处理1000行刷新一次，减少内存占用
                    if (row % 1000 == 0)
                    {
                        logger?.LogDebug("Excel导出进度：已处理 {RowCount} 行数据", row - 1);
                        await Task.Yield();
                    }
                }

                // 自动调整列宽
                worksheet.Cells.AutoFitColumns();

                // 保存Excel文件
                await Task.Run(() =>
                {
                    // 确保目录存在
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
                    package.SaveAs(new FileInfo(filePath));
                });

                logger?.LogInformation("Excel数据导出成功，文件路径: {FilePath}", filePath);
                return true;
            }
            catch (ArgumentException ex)
            {
                logger?.LogError(ex, "Excel导出参数错误，文件路径: {FilePath}", filePath);
                return false;
            }
            catch (IOException ex)
            {
                logger?.LogError(ex, "Excel导出文件IO错误，文件路径: {FilePath}", filePath);
                return false;
            }
            catch (ReflectionTypeLoadException ex)
            {
                logger?.LogError(ex, "Excel导出反射类型加载错误，文件路径: {FilePath}", filePath);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                logger?.LogError(ex, "Excel导出操作无效错误，文件路径: {FilePath}", filePath);
                return false;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Excel导出发生未知错误，文件路径: {FilePath}", filePath);
                return false;
            }
        }
    }
}