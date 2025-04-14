using ArandanoIRT_Backend.Infrastructure.Config;
using ArandanoIRT_Backend.Infrastructure.Data;
using ArandanoIRT_Backend.Infrastructure.Persistence.Interceptors;
using ArandanoIRT_Backend.Infrastructure.Services;
using FluentValidation.AspNetCore;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ArandanoIRT_Backend.UI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

var connectionString = builder.Configuration.GetConnectionString("ConnectionString");
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
});

DependencyInjection.ConfigureServices(builder.Services, builder.Configuration);
LoggingConfig.ConfigureServices(builder.Services, builder.Configuration);
builder.Services.AddHostedService<LogCleanupService>();
RateLimitConfig.ConfigureRateLimiting(builder.Services, builder.Configuration);

builder.Host.UseSerilog();
builder.Services.AddMemoryCache();
builder.Services.AddCustomCors();
builder.Services.AddJwtAuthentication(builder.Configuration);
//builder.Services.AddValidatorsFromAssemblyContaining<ActivityRequestDtoValidator>();
builder.Services.ConfigureResponseCompression();
builder.Services.ConfigureJsonOptions();

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureSwagger();
builder.Services.AddOutputCache();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseReDoc(c =>
    {
        c.Path = "/redoc";
        c.DocumentPath = "/swagger/v1/swagger.json";
    });
}
app.UseHttpsRedirection();
app.UseExceptionHandling();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("OnlyFrontend");
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseSanitization();
app.UseResponseCompression();
app.UseOutputCache();
app.MapControllers();
app.Run();

app.Run();