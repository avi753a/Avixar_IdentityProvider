# Avixar Identity Provider - API Testing Guide

## 📋 Quick Start

### 1. Setup Database with Sample Data

```sql
-- Run this in your PostgreSQL database
-- File: Avixar.Data/scripts/SampleData.sql

-- This will create:
-- ✅ OAuth Client: test_client_123
-- ✅ Client Secret: test_secret_456
-- ✅ Redirect URIs configured
```

### 2. Import Postman Collection

**File**: `Avixar_IdentityProvider_API.postman_collection.json`

**Steps**:
1. Open Postman
2. Click **Import** button
3. Select the JSON file
4. Collection will be imported with all endpoints

### 3. Configure Postman Variables

The collection uses these variables (already pre-configured):

| Variable | Default Value | Description |
|----------|---------------|-------------|
| `base_url` | `https://localhost:7001` | API base URL |
| `ui_base_url` | `https://localhost:7000` | UI base URL |
| `client_id` | `test_client_123` | OAuth client ID |
| `client_secret` | `test_secret_456` | OAuth client secret |
| `redirect_uri` | `http://localhost:3000/callback` | OAuth callback URL |
| `auth_code` | _(empty)_ | Set after Step 1 |
| `access_token` | _(empty)_ | Set after Step 2 |

---

## 🔐 Complete OAuth Flow Testing

### Step 1: Register a Test User

**Endpoint**: `POST {{ui_base_url}}/Auth/Register`

**Request Body** (form-urlencoded):
```
Email: testuser@example.com
Password: Test@123456
DisplayName: Test User
```

**Expected Response**: Redirect to home page with authentication cookie set

---

### Step 2: Login (Get Authentication Cookie)

**Endpoint**: `POST {{ui_base_url}}/Auth/Login`

**Request Body** (form-urlencoded):
```
Email: testuser@example.com
Password: Test@123456
```

**Expected Response**: 
- Status: `302 Found` (redirect)
- Sets cookie: `.AspNetCore.Cookies=...`

**⚠️ IMPORTANT**: Copy the cookie value for subsequent requests!

---

### Step 3: OAuth Authorize (Get Authorization Code)

**Endpoint**: `GET {{base_url}}/connect/authorize`

**Query Parameters**:
```
client_id: test_client_123
redirect_uri: http://localhost:3000/callback
response_type: code
state: random_state_123
nonce: random_nonce_456
```

**Full URL**:
```
https://localhost:7001/connect/authorize?client_id=test_client_123&redirect_uri=http://localhost:3000/callback&response_type=code&state=random_state_123&nonce=random_nonce_456
```

**Expected Response**:
- Status: `302 Found`
- Location: `http://localhost:3000/callback?code=XXXXXXXX&state=random_state_123`

**Action Required**: 
1. Copy the `code` value from the redirect URL
2. Set it in Postman variable `{{auth_code}}`

**Example Redirect**:
```
http://localhost:3000/callback?code=a1b2c3d4e5f6g7h8&state=random_state_123
```

---

### Step 4: Exchange Code for Access Token

**Endpoint**: `POST {{base_url}}/connect/token`

**Request Body** (form-urlencoded):
```
client_id: test_client_123
client_secret: test_secret_456
code: {{auth_code}}
redirect_uri: http://localhost:3000/callback
grant_type: authorization_code
```

**Expected Response**:
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjNlNDU2Ny1lODliLTEyZDMtYTQ1Ni00MjY2MTQxNzQwMDAiLCJlbWFpbCI6InRlc3R1c2VyQGV4YW1wbGUuY29tIiwianRpIjoiYWJjZGVmZ2giLCJkaXNwbGF5TmFtZSI6IlRlc3QgVXNlciIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWVpZGVudGlmaWVyIjoiMTIzZTQ1NjctZTg5Yi0xMmQzLWE0NTYtNDI2NjE0MTc0MDAwIiwibmJmIjoxNzMyOTAzODAwLCJleHAiOjE3MzI5MDc0MDAsImlzcyI6IkF2aXhhcklkZW50aXR5UHJvdmlkZXIiLCJhdWQiOiJBdml4YXJDbGllbnRzIn0.SIGNATURE",
  "expires_in": 3600,
  "id_token": "TODO: Implement ID Token if needed"
}
```

**Action Required**:
1. Copy the `access_token` value
2. Set it in Postman variable `{{access_token}}`

---

### Step 5: Get User Info

**Endpoint**: `GET {{base_url}}/connect/userinfo`

**Headers**:
```
Authorization: Bearer {{access_token}}
```

**Expected Response**:
```json
{
  "sub": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Test User",
  "email": "testuser@example.com",
  "picture": null
}
```

---

### Step 6: Logout

**Endpoint**: `GET {{base_url}}/connect/logout`

**Query Parameters**:
```
post_logout_redirect_uri: http://localhost:3000/
client_id: test_client_123
```

**Expected Response**: Redirect to `http://localhost:3000/`

---

## 🧪 Testing Scenarios

### Scenario 1: Happy Path (All Steps Work)

1. ✅ Register user
2. ✅ Login user
3. ✅ Get authorization code
4. ✅ Exchange code for token
5. ✅ Get user info
6. ✅ Logout

**Expected**: All steps succeed with proper responses

---

### Scenario 2: Unauthorized Access

**Test**: Call `/connect/authorize` without logging in first

**Expected Response**:
- Status: `302 Found`
- Redirects to login page

---

### Scenario 3: Invalid Client

**Test**: Use wrong `client_id` in authorize request

**Expected Response**:
```json
{
  "error": "Invalid Client ID"
}
```

---

### Scenario 4: Invalid Redirect URI

**Test**: Use unauthorized redirect URI

**Expected Response**:
```json
{
  "error": "Unauthorized Redirect URI"
}
```

---

### Scenario 5: Expired Authorization Code

**Test**: Use the same authorization code twice

**Expected Response**:
```json
{
  "error": "invalid_grant",
  "error_description": "Invalid or expired authorization code"
}
```

---

### Scenario 6: Invalid Access Token

**Test**: Call `/connect/userinfo` with invalid token

**Expected Response**:
- Status: `401 Unauthorized`

---

## 📊 Sample Data Reference

### OAuth Clients

| Client ID | Client Secret | Redirect URIs |
|-----------|---------------|---------------|
| `test_client_123` | `test_secret_456` | `http://localhost:3000/callback`<br>`https://oauth.pstmn.io/v1/callback` |
| `mobile_app_001` | `mobile_secret_789` | `myapp://callback`<br>`http://localhost:8080/callback` |

### Test Users

Create your own test users via the Register endpoint.

**Recommended Test User**:
- Email: `testuser@example.com`
- Password: `Test@123456`
- Display Name: `Test User`

---

## 🔍 Debugging Tips

### 1. Check Application Logs

Logs are written to: `D:\Logs\ui_application.log` and `D:\Logs\api_application.log`

**Look for**:
- Entry logging: "OAuth Authorize request - ClientId: ..."
- Success logging: "Authorization code generated for user: ..."
- Error logging: Any exceptions or failures

### 2. Verify Database

```sql
-- Check if client exists
SELECT * FROM clients WHERE client_id = 'test_client_123';

-- Check if user exists
SELECT "Id", "DisplayName", "Email_Hash" FROM users;

-- Check user providers (for social login)
SELECT * FROM user_providers;
```

### 3. Common Issues

| Issue | Solution |
|-------|----------|
| "User not authenticated" | Login first via UI |
| "Invalid Client ID" | Run SampleData.sql |
| "Unauthorized Redirect URI" | Check allowed_redirect_uris in database |
| "Invalid or expired code" | Code can only be used once |
| SSL certificate errors | Accept self-signed cert in Postman settings |

---

## 🚀 Running the Applications

### Start API (Port 7001)

```powershell
cd d:\Repos\Avixar_IdentityProvider
dotnet run --project Avixar.API
```

### Start UI (Port 7000)

```powershell
cd d:\Repos\Avixar_IdentityProvider
dotnet run --project Avixar.UI
```

### Verify Both Running

- API: `https://localhost:7001`
- UI: `https://localhost:7000`

---

## 📝 Postman Collection Structure

```
📁 Avixar Identity Provider API
├── 📁 1. Authentication (UI)
│   ├── Register User
│   └── Login User
├── 📁 2. OAuth Authorization Flow
│   ├── Step 1: Authorize (Get Auth Code)
│   ├── Step 2: Exchange Code for Token
│   ├── Step 3: Get User Info
│   └── Step 4: Logout
├── 📁 3. User Profile Management
│   └── Get User Profile
└── 📁 4. Health Check
    ├── API Health Check
    └── UI Health Check
```

---

## ✅ Success Criteria

After testing, you should be able to:

- ✅ Register and login users
- ✅ Generate authorization codes
- ✅ Exchange codes for access tokens
- ✅ Access protected user information
- ✅ Logout users properly
- ✅ See comprehensive logs for all operations

---

## 🔗 Additional Resources

- **OAuth 2.0 Spec**: https://oauth.net/2/
- **JWT Debugger**: https://jwt.io/
- **Postman OAuth 2.0**: https://learning.postman.com/docs/sending-requests/authorization/#oauth-20

---

## 🆘 Need Help?

Check the logs first! All operations are now logged with:
- Entry logging (what was called)
- Success logging (what succeeded)
- Error logging (what failed and why)

Log files location:
- UI: `D:\Logs\ui_application.log`
- API: `D:\Logs\api_application.log`
