// <copyright file="TelemetryService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Metrics
{
    using System;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;

    /// <summary>
    /// OpenTelemetry 服务.
    /// </summary>
    public class TelemetryService : IDisposable
    {
        /// <summary>
        /// 分布式追踪的追踪提供程序.
        /// </summary>
        private readonly TracerProvider tracerProvider;

        /// <summary>
        /// 指标收集的仪表提供程序.
        /// </summary>
        private readonly MeterProvider meterProvider;

        /// <summary>
        /// 表示遥测服务是否已被释放.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryService"/> class.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        public TelemetryService(string serviceName = "Crawler")
        {
            // 创建资源
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName);

            // 配置追踪
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(serviceName)
                .AddHttpClientInstrumentation()
                .AddConsoleExporter()
                .Build();

            // 配置指标
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(serviceName)
                .AddConsoleExporter()
                .Build();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="TelemetryService"/> class.
        /// </summary>
        ~TelemetryService()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Gets the tracer provider for distributed tracing.
        /// </summary>
        /// <returns>The tracer provider instance.</returns>
        public TracerProvider GetTracerProvider() => this.tracerProvider;

        /// <summary>
        /// Gets the meter provider for metrics collection.
        /// </summary>
        /// <returns>The meter provider instance.</returns>
        public MeterProvider GetMeterProvider() => this.meterProvider;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the telemetry service resources.
        /// </summary>
        /// <param name="disposing">A value indicating whether to dispose managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.tracerProvider?.Dispose();
                    this.meterProvider?.Dispose();
                }

                this.disposed = true;
            }
        }
    }
}
