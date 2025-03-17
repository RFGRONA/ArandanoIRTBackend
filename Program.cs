using ArandanoIRT_Backend.Infrastructure.Config;
using ArandanoIRT_Backend.Infrastructure.Data;
using ArandanoIRT_Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options 
    => options.UseNpgsql(builder.Configuration.GetConnectionString("ConnectionString")));

DependencyInjection.ConfigureServices(builder.Services, builder.Configuration);
LoggingConfig.ConfigureServices(builder.Services, builder.Configuration);
builder.Services.AddHostedService<LogCleanupService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();