using HslGateway.Config;
using HslGateway.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<GatewayConfig>(builder.Configuration.GetSection("Gateway"));
builder.Services.AddSingleton<TagValueCache>();
builder.Services.AddSingleton<GatewayConfigStore>();
builder.Services.AddSingleton<DeviceRegistry>();
builder.Services.AddHostedService<PollingWorker>();

builder.Services.AddGrpc();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(50051, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GatewayService>();
app.MapGrpcService<ConfigManagerService>();
app.MapGet("/", () => "HSL Gateway gRPC service is running.");
app.MapGet("/health", () => new { status = "ok" });

app.Run();
