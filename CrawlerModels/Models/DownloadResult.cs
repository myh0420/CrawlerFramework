using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrawlerEntity.Models;

/// <summary>
/// 下载结果
/// </summary>
public class DownloadResult
{
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public byte[] RawData { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DownloadTimeMs { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
}
