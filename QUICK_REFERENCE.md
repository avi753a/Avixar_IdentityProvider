# 🚀 Quick Reference - OAuth Testing

## 1️⃣ Setup (One Time)

```sql
-- Run in PostgreSQL
\i 'd:/Repos/Avixar_IdentityProvider/Avixar.Data/scripts/SampleData.sql'
```

Import Postman collection: `Avixar_IdentityProvider_API.postman_collection.json`

---

## 2️⃣ Start Applications

```powershell
# Terminal 1 - API
dotnet run --project d:\Repos\Avixar_IdentityProvider\Avixar.API

# Terminal 2 - UI  
dotnet run --project d:\Repos\Avixar_IdentityProvider\Avixar.UI
```

---

## 3️⃣ Test OAuth Flow (5 Steps)

### Step 1: Register User
```http
POST https://localhost:7000/Auth/Register
Content-Type: application/x-www-form-urlencoded

Email=testuser@example.com&Password=Test@123456&DisplayName=Test User
```

### Step 2: Login User
```http
POST https://localhost:7000/Auth/Login
Content-Type: application/x-www-form-urlencoded

Email=testuser@example.com&Password=Test@123456
```
**→ Copy cookie from response**

### Step 3: Get Authorization Code
```http
GET https://localhost:7001/connect/authorize?client_id=test_client_123&redirect_uri=http://localhost:3000/callback&response_type=code&state=abc123&nonce=xyz789
Cookie: .AspNetCore.Cookies=YOUR_COOKIE
```
**→ Copy 'code' from redirect URL**

### Step 4: Get Access Token
```http
POST https://localhost:7001/connect/token
Content-Type: application/x-www-form-urlencoded

client_id=test_client_123&client_secret=test_secret_456&code=YOUR_CODE&redirect_uri=http://localhost:3000/callback&grant_type=authorization_code
```
**→ Copy 'access_token' from response**

### Step 5: Get User Info
```http
GET https://localhost:7001/connect/userinfo
Authorization: Bearer YOUR_ACCESS_TOKEN
```

---

## 📊 Sample Credentials

| Item | Value |
|------|-------|
| **Client ID** | `test_client_123` |
| **Client Secret** | `test_secret_456` |
| **Redirect URI** | `http://localhost:3000/callback` |
| **Test Email** | `testuser@example.com` |
| **Test Password** | `Test@123456` |

---

## 🔍 Quick Debug

```sql
-- Check client exists
SELECT * FROM clients WHERE client_id = 'test_client_123';

-- Check users
SELECT "Id", "DisplayName" FROM users;
```

**Logs**: `D:\Logs\ui_application.log` and `D:\Logs\api_application.log`

---

## ✅ Expected Responses

**Token Response**:
```json
{
  "access_token": "eyJhbGci...",
  "expires_in": 3600,
  "id_token": "TODO: Implement ID Token if needed"
}
```

**UserInfo Response**:
```json
{
  "sub": "user-guid",
  "name": "Test User",
  "email": "testuser@example.com",
  "picture": null
}
```

---

## 🆘 Common Issues

| Error | Fix |
|-------|-----|
| "User not authenticated" | Login first (Step 2) |
| "Invalid Client ID" | Run SampleData.sql |
| "Invalid code" | Code expires after 5 min |
| SSL errors | Accept cert in Postman |

---

**Full Guide**: See `API_TESTING_GUIDE.md`
