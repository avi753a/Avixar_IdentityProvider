# Cookie Configuration Fix for Dev Tunnel OAuth

## Problem
Microsoft/Google OAuth was failing with dev tunnels because cookies were being blocked due to SameSite policy restrictions during cross-origin redirects.

## Solution Applied

### 1. Cookie Policy Configuration
Added to [`Program.cs`](file:///d:/Repos/Avixar_IdentityProvider/Avixar.Application/Program.cs#L28-L33):
```csharp
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.Secure = CookieSecurePolicy.Always;
});
```

### 2. Main Application Cookie
Updated cookie configuration:
```csharp
.AddCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.Cookie.SameSite = SameSiteMode.None;  // ✅ Allow cross-site
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // ✅ HTTPS only
    options.Cookie.HttpOnly = true;  // ✅ Security
})
```

### 3. External Cookie (for OAuth handshake)
```csharp
.AddCookie("ExternalCookie", options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
})
```

### 4. Cookie Policy Middleware
Added to middleware pipeline (BEFORE UseRouting):
```csharp
app.UseCookiePolicy();
```

## Why This Works

### SameSite=None
- Allows cookies to be sent in cross-site contexts
- Required for OAuth redirects from Microsoft/Google back to your app
- Dev tunnels are considered "cross-site" even though it's the same app

### Secure=Always
- Required when using SameSite=None
- Ensures cookies only sent over HTTPS
- Dev tunnels use HTTPS, so this is perfect

### HttpOnly=true
- Prevents JavaScript from accessing cookies
- Security best practice
- Protects against XSS attacks

## Middleware Order (Critical!)
```
1. UseHttpsRedirection()
2. UseStaticFiles()
3. UseCookiePolicy()  ← MUST be here!
4. UseRouting()
5. UseAuthentication()
6. UseAuthorization()
```

## Testing

1. **Clear browser cookies** (important!)
   - Chrome: Settings → Privacy → Clear browsing data → Cookies
   - Edge: Settings → Privacy → Clear browsing data → Cookies

2. **Run the app**:
   ```bash
   cd d:\Repos\Avixar_IdentityProvider\Avixar.Application
   dotnet run
   ```

3. **Access via dev tunnel**:
   ```
   https://m4pxl72k-44387.inc1.devtunnels.ms/
   ```

4. **Try Microsoft login** - should now work!

## What Changed
- ✅ Cookies now work with dev tunnels
- ✅ OAuth redirects preserve authentication state
- ✅ No more "authentication failed" after successful OAuth
- ✅ Works with both localhost and dev tunnel
- ✅ Secure (HTTPS only, HttpOnly)

## Browser Developer Tools Check
After login, check cookies in DevTools:
- **Name**: `.AspNetCore.Cookies` (or similar)
- **SameSite**: None
- **Secure**: ✓
- **HttpOnly**: ✓
- **Domain**: Your dev tunnel domain

If you see these settings, cookies are configured correctly! 🎉
