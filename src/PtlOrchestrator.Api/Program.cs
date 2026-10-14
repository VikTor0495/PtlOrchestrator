using Microsoft.EntityFrameworkCore;
using PtlOrchestrator.Api.Endpoints;
using PtlOrchestrator.Api.Persistence;
using PtlOrchestrator.Api.Security;
using PtlOrchestrator.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// installabile come servizio Windows sul server aziendale; altrove non ha effetto
builder.Host.UseWindowsService();

builder.Services.AddApiKeySecurity(builder.Configuration);

builder.Services.AddDbContext<PtlDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Ptl")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IOperatorService, OperatorService>();
builder.Services.AddScoped<IOperationEventIngestService, OperationEventIngestService>();
builder.Services.AddScoped<ISessionReportService, SessionReportService>();
builder.Services.AddScoped<IStationService, StationService>();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<PtlDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapOperationEventEndpoints();
app.MapOperatorEndpoints();
app.MapSessionEndpoints();
app.MapStationEndpoints();

await app.RunAsync();

// reso visibile ai test di integrazione (WebApplicationFactory)
public partial class Program;
