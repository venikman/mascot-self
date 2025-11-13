using AgentHello.AI;
using AgentHello.Endpoints;
using AgentHello.Telemetry;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5052";
builder.WebHost.UseUrls(urls);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

TelemetryConfigurator.Configure(builder);
builder.Services.AddAgentHelloServices(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging(RequestLoggingCustomization.Configure);
app.MapAgentHelloEndpoints();

app.Run();

public partial class Program { }
