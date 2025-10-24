
// CrawlerCore/Services/EncodingDetector.cs
using System;
using System.Text;

namespace CrawlerCore.Services
{
    /// <summary>
    /// 编码检测器
    /// </summary>
    public static class EncodingDetector
    {
        /// <summary>
        /// 从内容中检测编码
        /// </summary>
        public static Encoding DetectFromContent(byte[] content)
        {
            if (content == null || content.Length == 0)
                return Encoding.UTF8;

            // 检查BOM
            if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
                return Encoding.UTF8;
            if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
                return Encoding.Unicode;

            // 从HTML meta标签检测charset
            try
            {
                var contentStart = Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 1024));
                var charsetIndex = contentStart.IndexOf("charset=", StringComparison.OrdinalIgnoreCase);
                if (charsetIndex >= 0)
                {
                    charsetIndex += 8;
                    var endIndex = contentStart.IndexOfAny(['"', '\'', ' ', '>', ';'], charsetIndex);
                    if (endIndex > charsetIndex)
                    {
                        var charset = contentStart[charsetIndex..endIndex].Trim();
                        return Encoding.GetEncoding(charset);
                    }
                }
            }
            catch
            {
                // 忽略编码检测错误
            }

            return Encoding.UTF8;
        }
    }
}