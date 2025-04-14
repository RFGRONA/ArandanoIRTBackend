using ArandanoIRT_Backend.Application.Interfaces.Auditing;
using ArandanoIRT_Backend.Application.Interfaces.Services;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Infrastructure.Interfaces.IServices;
using ArandanoIRT_Backend.Infrastructure.Persistence.Auditing;
using ArandanoIRT_Backend.Infrastructure.Repositories;
using ArandanoIRT_Backend.Infrastructure.Services;
using ArandanoIRT_Backend.Infrastructure.Utilities;
using ApplicationServices = ArandanoIRT_Backend.Application.Services;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    /// <summary>
    /// Provides extension methods for configuring dependency injection services
    /// for the Infrastructure and Application layers.
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Registers services and repositories from the Infrastructure and Application layers
        /// into the dependency injection container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configuration">The application <see cref="IConfiguration"/> instance, used for configuring services that require settings.</param>
        /// <remarks>
        /// This method configures dependencies with appropriate lifetimes (Scoped, Singleton)
        /// including repositories, infrastructure utilities (caching, hashing, email, etc.),
        /// auditing components, and application-level services.
        /// </remarks>
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // --- Infrastructure Layer: Repositories ---
            // Registers repository implementations with a scoped lifetime.
            services.AddScoped<IAuditCropRepository, AuditCropRepository>();
            services.AddScoped<IAuditDataTableRepository, AuditDataTableRepository>();
            services.AddScoped<IAuditDeviceRepository, AuditDeviceRepository>();
            services.AddScoped<IAuditPersonRepository, AuditPersonRepository>();
            services.AddScoped<IAuditSensitiveDataRepository, AuditSensitiveDataRepository>();
            services.AddScoped<IAuditSystemTableRepository, AuditSystemTableRepository>();
            services.AddScoped<IChangePasswordRepository, ChangePasswordRepository>();
            services.AddScoped<ICropInvitationRepository, CropInvitationRepository>();
            services.AddScoped<ICropRepository, CropRepository>();
            services.AddScoped<IDeviceActivationRepository, DeviceActivationRepository>();
            services.AddScoped<IDeviceDataRepository, DeviceDataRepository>();
            services.AddScoped<IDeviceLogRepository, DeviceLogRepository>();
            services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
            services.AddScoped<IFailedLoginAttemptRepository, FailedLoginAttemptRepository>();
            services.AddScoped<IPersonRepository, PersonRepository>();
            services.AddScoped<IPlantDataRepository, PlantDataRepository>();
            services.AddScoped<IPlantStateHistoryRepository, PlantStateHistoryRepository>();
            services.AddScoped<IPlantObservationRepository, PlantObservationRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<ISensorDataRepository, SensorDataRepository>();
            services.AddScoped<IStatusRepository, StatusRepository>();
            services.AddScoped<ITableRelationRepository, TableRelationRepository>();
            services.AddScoped<IThermalDataRepository, ThermalDataRepository>();

            // --- Infrastructure Layer: Services & Utilities ---
            services.AddSingleton<ICacheService, CacheService>(); 
            services.AddSingleton<SanitizerService>(); 
            services.AddScoped<CookiesService>(); 
            services.AddScoped<ICaptchaService, CloudflareTurnstileService>(); 
            services.AddScoped<ITokenService, JwtTokenService>(); 
            services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>(); 
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>(); 
            services.AddSingleton<IRsaService, RsaService>(); 
            services.AddScoped<IEmailService, SmtpEmailService>(); 
            services.AddSingleton<IUserAgentParser, UAParserAdapter>(); 
            services.AddScoped<IRequestContextAccessor, RequestContextAccessor>(); 

            // --- Infrastructure Layer: Auditing ---
            // Registers auditing helper service and multiple generators for different audit types.
            // Consumers injecting IEnumerable<IAuditEntryGenerator> will receive all registered instances.
            services.AddScoped<IAuditHelperService, AuditHelperService>();
            services.AddScoped<IAuditEntryGenerator, AuditSensitiveDataGenerator>();
            services.AddScoped<IAuditEntryGenerator, AuditDataTableGenerator>();
            services.AddScoped<IAuditEntryGenerator, AuditCropGenerator>();
            services.AddScoped<IAuditEntryGenerator, AuditPersonGenerator>();
            services.AddScoped<IAuditEntryGenerator, AuditSystemTableGenerator>();
            services.AddScoped<IAuditEntryGenerator, AuditDeviceGenerator>();

            // --- Application Layer: Services ---
            // Registers application-level services, mapping interfaces to implementations.
            services.AddScoped<IAuthService, ApplicationServices.AuthService>(); 
        }
    }
}