// <copyright file="ExtractedData.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerStorage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CrawlerEntity.Enums;
    using CrawlerEntity.Models;
    using CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.Logging;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Conventions;
    using MongoDB.Driver;
    using Newtonsoft.Json;

    // MongoDB 文档模型

    /// <summary>
    /// 提取的数据文档.
    /// </summary>
    internal class ExtractedData
    {
        /// <summary>
        /// Gets or sets 文档ID.
        /// </summary>
        public ObjectId Id { get; set; }

        /// <summary>
        /// Gets or sets uRL.
        /// </summary>
        public required string Url { get; set; }

        /// <summary>
        /// Gets or sets 数据键.
        /// </summary>
        public required string DataKey { get; set; }

        /// <summary>
        /// Gets or sets 数据值.
        /// </summary>
        public required string DataValue { get; set; }

        /// <summary>
        /// Gets or sets 数据类型.
        /// </summary>
        public required string DataType { get; set; }
    }
}