using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using OpenSearch.Client;
using Serilog;
using StackExchange.Redis;
using ProductService.Repositories;
using ProductService.Services;
using ProductService.Data;
using ProductService.Models;
using ProductService.Serializers;
using Atlas.Logging;
using Atlas.EventBus;
using Atlas.Tracing;

var pack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
ConventionRegistry.Register("IgnoreExtraElements", pack, t => true);

if (!BsonClassMap.IsClassMapRegistered(typeof(CategoryInfo)))
{
    BsonClassMap.RegisterClassMap<CategoryInfo>(cm =>
    {
        cm.MapMember(c => c.Id).SetSerializer(new ObjectIdToStringSerializer());
        cm.MapMember(c => c.Name);
        cm.MapMember(c => c.Path);
    });
}

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ConfigureAtlasLogger(builder.Configuration, "ProductService");

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB")
    ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "atlas_product";

builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient(mongoConnectionString));
builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";

var redisOptions = ConfigurationOptions.Parse(redisConnection);
redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectRetry = 5;
redisOptions.ConnectTimeout = 5000;

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));

var openSearchUrl = builder.Configuration["OpenSearch:Url"] ?? "http://localhost:9200";
builder.Services.AddSingleton<OpenSearchClient>(sp =>
{
    var settings = new ConnectionSettings(new Uri(openSearchUrl))
        .DefaultIndex("products")
        .DisableDirectStreaming()
        .PrettyJson();

    return new OpenSearchClient(settings);
});

var rabbitMqHost = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitMqUser = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
var rabbitMqPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitMqConnection = $"amqp://{rabbitMqUser}:{rabbitMqPass}@{rabbitMqHost}:5672";

builder.Services.AddRabbitMQEventBus(rabbitMqConnection);

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

builder.Services.AddScoped<IProductService, ProductServiceImpl>();
builder.Services.AddSingleton<IOpenSearchIndexer, OpenSearchIndexer>();
builder.Services.AddScoped<IImageStorageService, MinioImageStorageService>();

builder.Services.AddAtlasTracing(builder.Configuration, "product-service");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks()
    .AddMongoDb(_ => new MongoClient(mongoConnectionString).GetDatabase(mongoDatabaseName), "mongodb")
    .AddRedis(redisConnection, "redis");

builder.Services.AddHostedService<ProductEventConsumer>();
builder.Services.AddHostedService<ProductService.Consumers.StockManagementConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    var searchIndexer = scope.ServiceProvider.GetRequiredService<IOpenSearchIndexer>();
    await DbSeeder.SeedAsync(database, searchIndexer);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.MapControllers();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

Log.Information("ProductService started successfully");

app.Run();
