using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrawlerEntity.Models;

/// <summary>
/// 解析结果
/// </summary>
public class ParseResult
{
    public string Url { get; set; } = string.Empty;
    public List<string> Links { get; set; } = [];
    public Dictionary<string, object> ExtractedData { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public List<string> Images { get; set; } = [];
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
}
