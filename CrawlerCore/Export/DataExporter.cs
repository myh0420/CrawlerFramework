// CrawlerCore/Export/DataExporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Newtonsoft.Json;

namespace CrawlerCore.Export
{
    /// <summary>
    /// 数据导出功能
    /// </summary>
    public interface IDataExporter
    {
        Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath);
        string SupportedFormat { get; }
    }
    /// <summary>
    /// Json数据导出功能
    /// </summary>
    public class JsonExporter : IDataExporter
    {
        public string SupportedFormat => "json";

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
    /// <summary>
    /// Csv数据导出功能
    /// </summary>
    public class CsvExporter : IDataExporter
    {
        public string SupportedFormat => "csv";

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
    /// <summary>
    /// Excel数据导出功能
    /// </summary>
    public class ExcelExporter : IDataExporter
    {
        public string SupportedFormat => "xlsx";

        public async Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath)
        {
            // 需要 EPPlus 或其他 Excel 库
            // 这里简化实现
            try
            {
                // 实际实现会使用 EPPlus 创建 Excel 文件
                // 这里先导出为 CSV 作为示例
                var csvExporter = new CsvExporter();
                var tempPath = Path.ChangeExtension(filePath, ".csv");
                var result = await csvExporter.ExportAsync(data, tempPath);
                
                if (result)
                {
                    // 在这里添加转换为 Excel 的逻辑
                    // File.Move(tempPath, filePath);
                }
                
                return result;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
    /// <summary>
    /// 数据导出服务功能
    /// </summary>
    public class DataExportService
    {
        private readonly List<IDataExporter> _exporters;

        public DataExportService()
        {
            _exporters = [
                new JsonExporter(),
                new CsvExporter(),
                new ExcelExporter()
            ];
        }

        public void RegisterExporter(IDataExporter exporter)
        {
            _exporters.Add(exporter);
        }

        public async Task<bool> ExportAsync<T>(IEnumerable<T> data, string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower().TrimStart('.');
            var exporter = _exporters.FirstOrDefault(e => 
                e.SupportedFormat.Equals(extension, StringComparison.OrdinalIgnoreCase));

            return exporter == null
                ? throw new NotSupportedException($"Format '{extension}' is not supported")
                : await exporter.ExportAsync(data, filePath);
        }

        public IEnumerable<string> GetSupportedFormats()
        {
            return _exporters.Select(e => e.SupportedFormat);
        }
    }
}