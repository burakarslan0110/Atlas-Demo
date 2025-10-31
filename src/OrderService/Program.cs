using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using OrderService.Data;
using OrderService.Services;
using Atlas.Logging;
using Atlas.EventBus;
using Atlas.Tracing;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ConfigureAtlasLogger(builder.Configuration, "OrderService");

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=atlas_order;Username=postgres;Password=postgres";

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(connectionString));

var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";

var redisOptions = ConfigurationOptions.Parse(redisConnection);
redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectRetry = 5;
redisOptions.ConnectTimeout = 5000;

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));

builder.Services.AddHttpClient("ProductService", client =>
{
    var productServiceUrl = builder.Configuration["Services:ProductService:Url"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(productServiceUrl);
});

var rabbitMqHost = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitMqUser = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
var rabbitMqPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitMqConnection = $"amqp://{rabbitMqUser}:{rabbitMqPass}@{rabbitMqHost}:5672";

builder.Services.AddRabbitMQEventBus(rabbitMqConnection);

builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderServiceImpl>();

builder.Services.AddAtlasTracing(builder.Configuration, "order-service");
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret is required. Set Jwt:Secret in configuration.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Atlas.UserService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Atlas.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

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
    .AddDbContextCheck<OrderDbContext>("database")
    .AddRedis(redisConnection, "redis");

builder.Services.AddHostedService<OrderEventConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await context.Database.MigrateAsync();
}

Log.Information("OrderService started successfully");

app.Run();
