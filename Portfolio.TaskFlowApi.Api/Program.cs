using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Portfolio.TaskFlowApi.Api.Authentication;
using Portfolio.TaskFlowApi.Api.ErrorHandling;
using Portfolio.TaskFlowApi.Api.OpenApi;
using Portfolio.TaskFlowApi.Core.Abstractions;
using Portfolio.TaskFlowApi.Core.Services;
using Portfolio.TaskFlowApi.Infrastructure;
using Portfolio.TaskFlowApi.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TaskFlow API",
        Version = "v1",
        Description = "Stage 2 API for authenticated, owner-scoped project and task management.",
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the JWT access token returned by POST /api/auth/login.",
    });
    options.OperationFilter<AuthorizeOperationFilter>();
});

var environmentJwtConfiguration = new Dictionary<string, string?>();
CopyEnvironmentJwtSetting("JWT:ISSUER", "Jwt:Issuer");
CopyEnvironmentJwtSetting("JWT:AUDIENCE", "Jwt:Audience");
CopyEnvironmentJwtSetting("JWT:SIGNING_KEY", "Jwt:SigningKey");
CopyEnvironmentJwtSetting("JWT:ACCESS_TOKEN_MINUTES", "Jwt:AccessTokenMinutes");

if (environmentJwtConfiguration.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(environmentJwtConfiguration);
}

void CopyEnvironmentJwtSetting(string environmentConfigurationKey, string optionsConfigurationKey)
{
    var value = builder.Configuration[environmentConfigurationKey];
    if (!string.IsNullOrWhiteSpace(value))
    {
        environmentJwtConfiguration[optionsConfigurationKey] = value;
    }
}

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required (JWT__ISSUER).")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required (JWT__AUDIENCE).")
    .Validate(
        options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
        "JWT signing key must contain at least 32 bytes (JWT__SIGNING_KEY).")
    .Validate(
        options => options.AccessTokenMinutes is >= 1 and <= 120,
        "JWT access token lifetime must be between 1 and 120 minutes.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;
        options.MapInboundClaims = false;
        options.EventsType = typeof(JwtBearerProblemDetailsEvents);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JwtBearerProblemDetailsEvents>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAccessTokenService, JwtAccessTokenService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddSingleton(TimeProvider.System);

var configuredDatabasePath = builder.Configuration["Database:Path"] ?? "Data/taskflow.db";
var databasePath = Path.GetFullPath(configuredDatabasePath, builder.Environment.ContentRootPath);
var databaseDirectory = Path.GetDirectoryName(databasePath)
    ?? throw new InvalidOperationException("The database path must include a directory.");
Directory.CreateDirectory(databaseDirectory);
builder.Services.AddInfrastructure($"Data Source={databasePath}");

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskFlow API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program
{
}
