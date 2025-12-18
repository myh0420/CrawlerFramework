// <copyright file="AesSecurityService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Security
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// AES加密服务实现，使用AES-256算法提供加密解密功能。
    /// </summary>
    public class AesSecurityService : ISecurityService
    {
        /// <summary>
        /// 日志记录器实例。
        /// </summary>
        private readonly ILogger<AesSecurityService> logger;

        /// <summary>
        /// AES加密算法密钥。
        /// </summary>
        private readonly byte[] key;

        /// <summary>
        /// AES加密算法初始化向量。
        /// </summary>
        private readonly byte[] iv;

        /// <summary>
        /// 初始化 <see cref="AesSecurityService"/> 类的新实例。
        /// </summary>
        /// <param name="logger">日志记录器实例（可选）。</param>
        /// <param name="encryptionKey">加密密钥（可选，默认使用内置密钥）。</param>
        /// <param name="initializationVector">初始化向量（可选，默认使用内置向量）。</param>
        public AesSecurityService(
            ILogger<AesSecurityService>? logger,
            string? encryptionKey = null,
            string? initializationVector = null)
        {
            this.logger = logger ?? new Logger<AesSecurityService>(new LoggerFactory());

            // 使用安全的默认密钥和IV，生产环境应使用环境变量或密钥管理服务提供的密钥
            string defaultKey = "CrawlerFrameworkDefaultEncryptionKey123456789012345678901234";
            string defaultIV = "DefaultIV123456789012";

            // 确保密钥长度为32字节（256位）
            this.key = Encoding.UTF8.GetBytes((encryptionKey ?? defaultKey).PadRight(32).Substring(0, 32));

            // 确保IV长度为16字节（128位）
            this.iv = Encoding.UTF8.GetBytes((initializationVector ?? defaultIV).PadRight(16).Substring(0, 16));
        }

        /// <summary>
        /// 异步加密字符串。
        /// </summary>
        /// <param name="plainText">要加密的明文。</param>
        /// <returns>加密后的字符串。</returns>
        public async Task<string> EncryptAsync(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            try
            {
                using var aesAlg = Aes.Create();
                aesAlg.Key = this.key;
                aesAlg.IV = this.iv;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using var msEncrypt = new MemoryStream();
                using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                using var swEncrypt = new StreamWriter(csEncrypt);

                await swEncrypt.WriteAsync(plainText);
                await swEncrypt.FlushAsync();
                await csEncrypt.FlushFinalBlockAsync();

                byte[] encrypted = msEncrypt.ToArray();
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to encrypt text");
                throw;
            }
        }

        /// <summary>
        /// 异步解密字符串。
        /// </summary>
        /// <param name="encryptedText">要解密的密文。</param>
        /// <returns>解密后的明文。</returns>
        public async Task<string> DecryptAsync(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                return encryptedText;
            }

            try
            {
                byte[] cipherText = Convert.FromBase64String(encryptedText);

                using var aesAlg = Aes.Create();
                aesAlg.Key = this.key;
                aesAlg.IV = this.iv;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using var msDecrypt = new MemoryStream(cipherText);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                return await srDecrypt.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to decrypt text");
                throw;
            }
        }

        /// <summary>
        /// 异步加密字符串数组。
        /// </summary>
        /// <param name="plainTexts">要加密的明文数组。</param>
        /// <returns>加密后的字符串数组。</returns>
        public async Task<string[]> EncryptArrayAsync(string[] plainTexts)
        {
            if (plainTexts == null || plainTexts.Length == 0)
            {
                return [];
            }

            string[] encryptedTexts = new string[plainTexts.Length];
            for (int i = 0; i < plainTexts.Length; i++)
            {
                encryptedTexts[i] = await this.EncryptAsync(plainTexts[i]);
            }

            return encryptedTexts;
        }

        /// <summary>
        /// 异步解密字符串数组。
        /// </summary>
        /// <param name="encryptedTexts">要解密的密文数组。</param>
        /// <returns>解密后的明文数组。</returns>
        public async Task<string[]> DecryptArrayAsync(string[] encryptedTexts)
        {
            if (encryptedTexts == null || encryptedTexts.Length == 0)
            {
                return [];
            }

            string[] plainTexts = new string[encryptedTexts.Length];
            for (int i = 0; i < encryptedTexts.Length; i++)
            {
                plainTexts[i] = await this.DecryptAsync(encryptedTexts[i]);
            }

            return plainTexts;
        }
    }
}