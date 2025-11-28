# Microsoft OAuth Troubleshooting Guide

## Current Issue: "server_error" during Microsoft Authentication

### What I Fixed:

1. **Added Error Handling** in [`AuthController.cs`](file:///d:/Repos/Avixar_IdentityProvider/Avixar.Application/Controllers/AuthController.cs)
   - Wrapped `ExternalLoginCallback` in try-catch
   - Added detailed error logging to console
   - Added TempData error messages for user feedback

2. **Added Event Handlers** in [`Program.cs`](file:///d:/Repos/Avixar_IdentityProvider/Avixar.Application/Program.cs)
   - `OnRemoteFailure` - Catches OAuth errors from Microsoft
   - `OnAccessDenied` - Handles user denying permissions
   - `OnCreatingTicket` - Logs successful authentication
   - All errors redirect to `/Auth/Login?error=...`

### Next Steps to Debug:

1. **Stop IIS Express** (it's locking the DLL files)
   - Close Visual Studio or stop the running app
   - Run: `dotnet build` again

2. **Run the app and check console output**:
   ```bash
   cd d:\Repos\Avixar_IdentityProvider\Avixar.Application
   dotnet run
   ```

3. **Try Microsoft login again** and watch the console for:
   - "Microsoft Auth Remote Failure: ..." - Shows the actual error
   - "Microsoft Auth: Creating ticket" - Means OAuth succeeded
   - Claim logs showing what data Microsoft returned

### Common Causes of "server_error":

#### 1. **Redirect URI Mismatch** (Most Common)
**Check**: Your Microsoft Entra ID redirect URI **exactly** matches your app's URL

**In Azure Portal**:
- Go to App Registrations → Your App → Authentication
- Redirect URI should be: `https://localhost:XXXX/signin-microsoft`
- Replace `XXXX` with your actual port (check console when running app)

#### 2. **Missing Permissions**
**Check**: Your app has the right API permissions

**In Azure Portal**:
- Go to App Registrations → Your App → API Permissions
- Should have at least:
  - `openid`
  - `profile`
  - `email`
- Click "Grant admin consent"

#### 3. **Wrong Client Secret**
**Check**: `appsettings.json` has the correct secret

Your current config:
```json
"Microsoft": {
  "ClientId": "c321cd73-e349-4796-a764-0bcb1270e72c",
  "ClientSecret": "fda2fc32-3691-4ac3-8e8d-fd47ab7b3531"
}
```

**Verify**: This secret is still valid in Azure Portal → Certificates & secrets

#### 4. **Account Type Restriction**
**Check**: Supported account types in Azure

**In Azure Portal**:
- App Registrations → Your App → Overview
- "Supported account types" should be:
  - "Accounts in any organizational directory and personal Microsoft accounts"
  - NOT "Single tenant"

### How to See the Actual Error:

After rebuilding and running, the console will show:
```
Microsoft Auth Remote Failure: [ACTUAL ERROR MESSAGE]
Stack: [STACK TRACE]
```

This will tell you exactly what's wrong!

### Testing Checklist:

- [ ] Stop IIS Express / Visual Studio
- [ ] Run `dotnet build` successfully
- [ ] Run `dotnet run` and note the port number
- [ ] Verify redirect URI in Azure matches: `https://localhost:PORT/signin-microsoft`
- [ ] Try Microsoft login
- [ ] Check console output for error details
- [ ] Share the error message if still failing

### Quick Fix Commands:

```bash
# Stop and rebuild
cd d:\Repos\Avixar_IdentityProvider\Avixar.Application
dotnet clean
dotnet build
dotnet run
```

Then try Microsoft login and check the console!
