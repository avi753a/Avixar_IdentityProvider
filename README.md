# Avixar Identity Provider

A modern OAuth 2.0 Identity Provider built with ASP.NET Core 10, featuring social login, JWT authentication, and a beautiful UI.

## 🚀 Quick Start

### Prerequisites
- .NET 10 SDK
- PostgreSQL 13+
- Docker Desktop (optional, for Redis/MongoDB)
- Visual Studio 2022 or VS Code

### 1. Clone and Setup

```powershell
git clone <repository-url>
cd Avixar_IdentityProvider
```

### 2. Configure Database

Update connection string in `Avixar.UI/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=avidevdb;Username=appuser;Password=Temp@123"
  }
}
```

Run the database initialization script:

```powershell
psql -U appuser -d avidevdb -f database-init.sql
```

### 3. Configure OAuth Providers

Update `Avixar.UI/appsettings.json` with your OAuth credentials:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    },
    "Microsoft": {
      "ClientId": "YOUR_MICROSOFT_CLIENT_ID",
      "ClientSecret": "YOUR_MICROSOFT_CLIENT_SECRET"
    }
  }
}
```

### 4. Start Docker Services (Optional)

```powershell
docker-compose up -d
```

This starts:
- **Redis** on port 6379 (caching)
- **MongoDB** on port 27017 (optional database)

### 5. Run the Application

```powershell
dotnet run --project Avixar.UI
```

Access the application at: **https://localhost:7000**

## 📁 Project Structure

```
Avixar_IdentityProvider/
├── Avixar.UI/              # Main web application (MVC + API)
├── Avixar.Domain/          # Business logic and services
├── Avixar.Data/            # Data access layer
├── Avixar.Entity/          # Domain entities and DTOs
├── Avixar.Infrastructure/  # Cross-cutting concerns
├── database-init.sql       # Database setup script
├── docker-compose.yml      # Docker services configuration
└── README.md              # This file
```

## 🔐 Features

### Authentication
- ✅ Local authentication (email/password)
- ✅ Social login (Google, Microsoft)
- ✅ OAuth 2.0 Authorization Code Flow
- ✅ JWT token generation and validation
- ✅ Secure cookie-based sessions

### API Endpoints

#### OAuth 2.0 Endpoints
- `GET /connect/authorize` - Initiate OAuth flow
- `POST /connect/token` - Exchange code for access token
- `GET /connect/userinfo` - Get authenticated user info
- `GET /connect/logout` - Logout user

#### Authentication Endpoints
- `POST /Auth/Login` - User login
- `POST /Auth/SignUp` - User registration
- `POST /Auth/Register` - Alternative registration endpoint
- `POST /Auth/ExternalLogin` - Social login initiation
- `POST /Auth/Logout` - User logout

#### Health Check
- `GET /health` - Application health status

### Security Features
- Password hashing with BCrypt
- JWT token-based API authentication
- HTTPS enforcement
- CORS configuration
- Cookie security (HttpOnly, Secure, SameSite)

## 🧪 Testing

### Using Postman

1. Import the Postman collection: `Avixar_IdentityProvider_API.postman_collection.json`
2. Follow the OAuth flow:
   - Register/Login a user
   - Get authorization code
   - Exchange for access token
   - Access protected endpoints

See [API_TESTING_GUIDE.md](API_TESTING_GUIDE.md) for detailed testing instructions.

### Test Credentials

**OAuth Client:**
- Client ID: `test_client_123`
- Client Secret: `test_secret_456`
- Redirect URI: `http://localhost:3000/callback`

**Test User:**
- Email: `testuser@example.com`
- Password: `Test@123456`

## 🛠️ Configuration

### Application Settings

Key configuration in `appsettings.json`:

```json
{
  "Jwt": {
    "Secret": "YourSuperSecretKeyForJWTTokenGeneration123456789",
    "Issuer": "AvixarIdentityProvider",
    "Audience": "AvixarClients"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Security": {
    "EncryptionKey": "your-encryption-key",
    "BlindIndexKey": "your-blind-index-key"
  }
}
```

### Environment Variables

Set these for production:
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=https://+:443;http://+:80`

## 📊 Database Schema

### Main Tables
- `clients` - OAuth client applications
- `user_addresses` - User address information

See `database-init.sql` for complete schema.

## 🐳 Docker Support

The project includes Docker Compose configuration for:
- **Redis** - Session caching
- **MongoDB** - Optional document storage

Start services:
```powershell
docker-compose up -d
```

Stop services:
```powershell
docker-compose stop
```

## 📝 Logging

Logs are written to:
- `D:\Logs\ui_application.log` - Application logs
- Console output during development

Configure logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## 🔧 Development

### Build

```powershell
dotnet build
```

### Run Tests

```powershell
dotnet test
```

### Watch Mode

```powershell
dotnet watch run --project Avixar.UI
```

## 🚢 Deployment

### Production Checklist

- [ ] Update JWT secret keys
- [ ] Configure production database
- [ ] Set up HTTPS certificates
- [ ] Configure OAuth redirect URLs
- [ ] Update CORS policy
- [ ] Set environment to Production
- [ ] Configure logging
- [ ] Set up monitoring

### Publish

```powershell
dotnet publish -c Release -o ./publish
```

## 📚 Additional Documentation

- [API Testing Guide](API_TESTING_GUIDE.md) - Comprehensive API testing instructions
- [Quick Reference](QUICK_REFERENCE.md) - Quick command reference
- [DevTunnel Configuration](DevTunnel_Config.md) - Dev tunnel setup for testing

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📄 License

This project is proprietary software.

## 🆘 Support

For issues and questions:
- Check the logs in `D:\Logs\`
- Review the API Testing Guide
- Check database connectivity
- Verify OAuth configuration

## 🔗 Resources

- [OAuth 2.0 Specification](https://oauth.net/2/)
- [JWT.io](https://jwt.io/) - JWT debugger
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
