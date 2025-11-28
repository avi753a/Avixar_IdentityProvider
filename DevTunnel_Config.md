# Dev Tunnel Configuration

## Your Standard Dev Tunnel URL
```
https://m4pxl72k-44387.inc1.devtunnels.ms/
```

## OAuth Redirect URLs for Microsoft Entra ID

**Add these exact URLs in Microsoft Entra ID → App Registrations → Authentication:**

1. **Dev Tunnel (Primary)**:
   ```
   https://m4pxl72k-44387.inc1.devtunnels.ms/signin-microsoft
   ```

2. **Localhost (Backup)**:
   ```
   https://localhost:44387/signin-microsoft
   ```

## OAuth Redirect URLs for Google Cloud Console

**Add these exact URLs in Google Cloud Console → Credentials → OAuth 2.0 Client:**

1. **Dev Tunnel (Primary)**:
   ```
   https://m4pxl72k-44387.inc1.devtunnels.ms/signin-google
   ```

2. **Localhost (Backup)**:
   ```
   https://localhost:44387/signin-google
   ```

## Running with Dev Tunnel

1. **Start Dev Tunnel** (if not already running):
   ```bash
   devtunnel host -p 44387 --allow-anonymous
   ```

2. **Run the app**:
   ```bash
   cd d:\Repos\Avixar_IdentityProvider\Avixar.Application
   dotnet run
   ```

3. **Access via**:
   - Dev Tunnel: `https://m4pxl72k-44387.inc1.devtunnels.ms/`
   - Local: `https://localhost:44387/`

## Important Notes

- The app now uses **dynamic redirect URLs** based on the current request host
- Works automatically with both localhost and dev tunnel
- No code changes needed when switching between local and tunnel
- All error redirects now preserve the current host (no more localhost redirects when using tunnel)

## Error Handling

- Errors now show a **detailed error page** instead of silently redirecting
- Console logs show full exception details
- Error page includes:
  - Error title
  - Error message
  - Technical details (expandable)
  - "Try Again" and "Go Home" buttons
