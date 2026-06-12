using System.Text;
using System.Threading.RateLimiting;
using Fundo.Application.Loans;
using Fundo.Applications.WebApi.Authentication;
using Fundo.Applications.WebApi.Errors;
using Fundo.Applications.WebApi.Health;
using Fundo.Infrastructure;
using Fundo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sql-server", tags: ["ready"]);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<LoanService>();

var loanProductOptions = builder.Configuration
    .GetSection(LoanProductOptions.SectionName)
    .Get<LoanProductOptions>() ?? new LoanProductOptions();
loanProductOptions.Validate();
builder.Services.AddSingleton<ILoanProductCatalog>(
    new LoanProductCatalog(loanProductOptions.DefaultType, loanProductOptions.Products));

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<TokenService>();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "ApplicationAuth";
        options.DefaultChallengeScheme = "ApplicationAuth";
    })
    .AddPolicyScheme("ApplicationAuth", "JWT bearer or application cookie", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.ToString().StartsWith(
                $"{JwtBearerDefaults.AuthenticationScheme} ",
                StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "fundo.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Loans.Read", policy =>
        policy.RequireClaim(LoanPermissions.ClaimType, LoanPermissions.Read));
    options.AddPolicy("Loans.Write", policy =>
        policy.RequireClaim(LoanPermissions.ClaimType, LoanPermissions.Write));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var writePermitLimit = builder.Configuration.GetValue("RateLimiting:WritePermitLimit", 30);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("writes", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = writePermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await app.Services.ApplyMigrationsAsync();
}

app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var isUnsafeMethod = HttpMethods.IsPost(context.Request.Method) ||
        HttpMethods.IsPut(context.Request.Method) ||
        HttpMethods.IsPatch(context.Request.Method) ||
        HttpMethods.IsDelete(context.Request.Method);
    var usesCookieSession = context.User.Identity?.IsAuthenticated == true &&
        string.IsNullOrWhiteSpace(context.Request.Headers.Authorization);
    var hasRequestedWithHeader = string.Equals(
        context.Request.Headers["X-Requested-With"],
        "XMLHttpRequest",
        StringComparison.Ordinal);

    if (isUnsafeMethod && usesCookieSession && !hasRequestedWithHeader)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Write request rejected",
            detail = "Cookie-authenticated write requests must originate from the application client.",
            traceId = context.TraceIdentifier,
        });
        return;
    }

    await next();
});
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new()
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready"),
}).AllowAnonymous();

app.Run();

public partial class Program;
