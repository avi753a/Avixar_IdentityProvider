using Avixar.Domain.Services;
using Avixar.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = "Host=localhost;Port=5432;Database=avidevdb;Username=appuser;Password=Temp@123";
}

// Register Services (implementations are in Data layer, registered via Domain)
builder.Services.AddScoped<IUserRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new Avixar.Data.Repositories.UserRepository(config);
});

// Register Domain Services
builder.Services.AddScoped<IUserService, UserService>();

// Configure Cookie Policy for cross-origin OAuth (dev tunnel compatibility)
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.Secure = CookieSecurePolicy.Always;
});

// Authentication (Cookie + External)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.Cookie.SameSite = SameSiteMode.None;
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
    
    // Add event handlers for debugging
    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        OnRemoteFailure = context =>
        {
            Console.WriteLine($"Google Auth Remote Failure: {context.Failure?.Message}");
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
    
    // Add event handlers for debugging
    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        OnRemoteFailure = context =>
        {
            Console.WriteLine($"Microsoft Auth Remote Failure: {context.Failure?.Message}");
            Console.WriteLine($"Stack: {context.Failure?.StackTrace}");
            var baseUrl = context.Request.Scheme + "://" + context.Request.Host;
            context.Response.Redirect($"{baseUrl}/Auth/Login?error=" + Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error"));
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnAccessDenied = context =>
        {
            Console.WriteLine("Microsoft Auth Access Denied");
            var baseUrl = context.Request.Scheme + "://" + context.Request.Host;
            context.Response.Redirect($"{baseUrl}/Auth/Login?error=access_denied");
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnCreatingTicket = context =>
        {
            Console.WriteLine("Microsoft Auth: Creating ticket");
            Console.WriteLine($"Access Token: {context.AccessToken?.Substring(0, Math.Min(20, context.AccessToken.Length))}...");
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// IMPORTANT: Cookie policy must come before UseRouting for OAuth to work with dev tunnels
app.UseCookiePolicy();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
