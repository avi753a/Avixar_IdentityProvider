using Avixar.Domain;
using Avixar.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(@"D:\Logs\ui_application.log", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllersWithViews();

    // Database
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = "Host=localhost;Port=5432;Database=avidevdb;Username=appuser;Password=Temp@123";
    }

    // 1. Redis Registration
    var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    try
    {
        var redis = ConnectionMultiplexer.Connect(redisConn + ",abortConnect=false");
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
        builder.Services.AddScoped<ICacheService, RedisCacheService>();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to connect to Redis. Caching will be disabled.");
        // Register a dummy or let DI fail if ICacheService is required?
        // RedisCacheService depends on IConnectionMultiplexer. If not registered, it will fail.
        // So we should register a disconnected multiplexer if possible, or just let it be.
        // But abortConnect=false usually prevents the exception here.
        // The try-catch is a second layer of defense.
    }

    // Register Repositories
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IClientRepository, ClientRepository>();

    // Register Domain Services
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IConnectService, ConnectService>();
    builder.Services.AddScoped<TokenService>();

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddRedis(redisConn, name: "redis");

    // Configure Cookie Policy
    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        options.MinimumSameSitePolicy = SameSiteMode.None;
        options.Secure = CookieSecurePolicy.Always;
    });

    // JWT Configuration
    var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "YourSuperSecretKeyForJWTTokenGeneration123456789";
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AvixarIdentityProvider";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AvixarClients";

    // Authentication (Cookie + External + JWT)
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
    })
    .AddCookie("ExternalCookie", options =>
    {
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "missing-client-id";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "missing-client-secret";
        options.SignInScheme = "ExternalCookie";
        options.SaveTokens = true;
        
        options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
        {
            OnRemoteFailure = context =>
            {
                Log.Warning("Google Auth Remote Failure: {Error}", context.Failure?.Message);
                var baseUrl = context.Request.Scheme + "://" + context.Request.Host;
                context.Response.Redirect($"{baseUrl}/Auth/Login?error=" + Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error"));
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    })
    .AddMicrosoftAccount(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? "missing-client-id";
        options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? "missing-client-secret";
        options.SignInScheme = "ExternalCookie";
        options.SaveTokens = true;
        
        options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
        {
            OnRemoteFailure = context =>
            {
                Log.Warning("Microsoft Auth Remote Failure: {Error}", context.Failure?.Message);
                var baseUrl = context.Request.Scheme + "://" + context.Request.Host;
                context.Response.Redirect($"{baseUrl}/Auth/Login?error=" + Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error"));
                context.HandleResponse();
                return Task.CompletedTask;
            },
            OnAccessDenied = context =>
            {
                Log.Warning("Microsoft Auth Access Denied");
                var baseUrl = context.Request.Scheme + "://" + context.Request.Host;
                context.Response.Redirect($"{baseUrl}/Auth/Login?error=access_denied");
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };

    });

    builder.Services.AddAuthorization();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Swagger with JWT Support
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Avixar Identity Provider API",
            Version = "v1",
            Description = "External API for third-party applications with JWT authentication"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // Global Exception Middleware (MUST be first to catch all exceptions)
    app.UseMiddleware<Avixar.Infrastructure.Middleware.GlobalExceptionMiddleware>();

    // Use Serilog request logging
    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Avixar Identity Provider API v1");
        });
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowAll");
    app.UseStaticFiles();
    app.UseCookiePolicy();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    app.MapHealthChecks("/health");

    Log.Information("Starting Avixar UI");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
