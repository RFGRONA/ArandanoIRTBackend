using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    /// <summary>
    /// Provides extension methods for configuring response compression services
    /// for the ASP.NET Core application pipeline.
    /// </summary>
    public static class ResponseCompressionConfig
    {
        /// <summary>
        /// Configures and adds response compression services to the application's service collection.
        /// Enables compression for HTTPS and adds both Gzip and Brotli providers,
        /// configured for the fastest compression level.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add response compression services to.</param>
        /// <remarks>
        /// The compression level is set to <see cref="CompressionLevel.Fastest"/>, which prioritizes
        /// minimal CPU usage during compression over achieving the highest compression ratio.
        /// Brotli is generally preferred by modern browsers over Gzip when available.
        /// </remarks>
        public static void ConfigureResponseCompression(this IServiceCollection services)
        {
            // Adds response compression services to the DI container.
            services.AddResponseCompression(options =>
            {
                // Enables response compression over HTTPS connections.
                options.EnableForHttps = true;
                // Adds the Gzip compression provider to the list of supported providers.
                options.Providers.Add<GzipCompressionProvider>();
                // Adds the Brotli compression provider (generally more efficient than Gzip).
                options.Providers.Add<BrotliCompressionProvider>();
            });

            // Configures options specifically for the Gzip compression provider.
            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                // Sets the compression level to Fastest, optimizing for speed over size reduction.
                options.Level = CompressionLevel.Fastest;
            });

            // Configures options specifically for the Brotli compression provider.
            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                // Sets the compression level to Fastest, optimizing for speed over size reduction.
                options.Level = CompressionLevel.Fastest;
            });
        }
    }
}