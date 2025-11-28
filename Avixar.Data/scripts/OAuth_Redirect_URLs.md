# OAuth Redirect URL Configuration

## Microsoft Entra ID (Azure AD) - Redirect URL

**Exact URL to configure in Microsoft Entra ID:**
```
https://localhost:7000/signin-microsoft
```

**For production (replace with your domain):**
```
https://yourdomain.com/signin-microsoft
```

### How to Configure in Microsoft Entra ID:

1. Go to **Azure Portal** → **App Registrations**
2. Select your application
3. Go to **Authentication** → **Platform configurations** → **Web**
4. Add Redirect URI: `https://localhost:7000/signin-microsoft`
5. Under **Implicit grant and hybrid flows**, enable:
   - ✅ ID tokens (used for implicit and hybrid flows)
6. Click **Save**

---

## Google OAuth - Redirect URL

**Exact URL to configure in Google Cloud Console:**
```
https://localhost:7000/signin-google
```

**For production:**
```
https://yourdomain.com/signin-google
```

### How to Configure in Google Cloud Console:

1. Go to **Google Cloud Console** → **APIs & Services** → **Credentials**
2. Select your OAuth 2.0 Client ID
3. Under **Authorized redirect URIs**, add:
   - `https://localhost:7000/signin-google`
4. Click **Save**

---

## Important Notes

### Port Number
- The default ASP.NET Core HTTPS port is usually **7000** or **5001**
- Check your `launchSettings.json` to confirm the exact port
- To verify, run `dotnet run` and check the console output

### Finding Your Port
Run this command to see your configured ports:
```bash
cat d:\Repos\Avixar_IdentityProvider\Avixar.Application\Properties\launchSettings.json
```

Or check the console when you run the app:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7000
```

### Why These URLs?
ASP.NET Core's Microsoft and Google authentication middleware automatically handles these callback paths:
- `/signin-microsoft` - Microsoft Account middleware callback
- `/signin-google` - Google OAuth middleware callback

These are **convention-based** and handled automatically by the authentication middleware you configured in `Program.cs`.

---

## Testing the Flow

1. **Start your app**: `dotnet run`
2. **Click "Continue with Microsoft"** on Login/SignUp page
3. **Microsoft redirects to**: `https://login.microsoftonline.com/...`
4. **After authentication, Microsoft redirects back to**: `https://localhost:7000/signin-microsoft`
5. **Middleware processes the callback** and redirects to: `/Auth/ExternalLoginCallback`
6. **Your code** in `ExternalLoginCallback` processes the user and redirects to Welcome page

---

## Troubleshooting

### "Redirect URI mismatch" error
- Ensure the URL in Azure/Google **exactly matches** what your app is running on
- Check HTTPS vs HTTP
- Check port number
- No trailing slashes

### "Invalid redirect_uri" error
- The redirect URI must be registered in Azure/Google
- Must use HTTPS for production (HTTP allowed for localhost only)

### Current Configuration
Based on your `appsettings.json`:
- **Microsoft ClientId**: `c321cd73-e349-4796-a764-0bcb1270e72c`
- **Google ClientId**: `862991345766-cnjqc83si77g7ipma9g6d3nasdgt67n1.apps.googleusercontent.com`

Make sure these redirect URIs are added to **both** of these OAuth applications.
