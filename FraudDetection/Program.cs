using Microsoft.ML;
using Serilog;
using StackExchange.Redis;
using RabbitMQ.Client;
using SharedKernel;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console()
    .ReadFrom.Configuration(ctx.Configuration));

// ML.NET
builder.Services.AddSingleton<MLContext>(new MLContext(seed: 42));

// Redis for real-time scoring
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(
    builder.Configuration.GetConnectionString("Redis")));

// RabbitMQ for event processing
builder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
{
    Uri = new Uri(builder.Configuration.GetConnectionString("RabbitMQ")),
    DispatchConsumersAsync = true
});

// Fraud Detection Services
builder.Services.AddSingleton<IFraudDetectionService, FraudDetectionService>();
builder.Services.AddSingleton<IFeatureEngineeringService, FeatureEngineeringService>();
builder.Services.AddSingleton<IModelTrainingService, ModelTrainingService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddRedis(builder.Configuration.GetConnectionString("Redis"));

// Controllers, Swagger, etc.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
