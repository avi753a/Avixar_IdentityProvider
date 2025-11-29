-- =====================================================================================
-- AVIXAR IDENTITY PROVIDER - SAMPLE DATA FOR TESTING
-- =====================================================================================
-- Run this script to insert sample data for testing OAuth flows
-- =====================================================================================

-- 1. Insert Sample OAuth Client
INSERT INTO "clients" (
    "client_id", 
    "client_name", 
    "client_secret",
    "allowed_redirect_uris", 
    "allowed_logout_uris",
    "is_active"
)
VALUES (
    'test_client_123', 
    'Test Chat Application', 
    'test_secret_456',  -- In production, this should be hashed!
    ARRAY[
        'http://localhost:3000/callback',
        'http://localhost:3000/auth/callback',
        'https://oauth.pstmn.io/v1/callback'  -- Postman OAuth callback
    ],
    ARRAY[
        'http://localhost:3000/',
        'http://localhost:3000/logout'
    ],
    TRUE
)
ON CONFLICT ("client_id") 
DO UPDATE SET 
    "client_name" = EXCLUDED."client_name",
    "client_secret" = EXCLUDED."client_secret",
    "allowed_redirect_uris" = EXCLUDED."allowed_redirect_uris",
    "allowed_logout_uris" = EXCLUDED."allowed_logout_uris";

-- 2. Insert Another Sample Client for Testing
INSERT INTO "clients" (
    "client_id", 
    "client_name", 
    "client_secret",
    "allowed_redirect_uris", 
    "allowed_logout_uris",
    "is_active"
)
VALUES (
    'mobile_app_001', 
    'Mobile App Client', 
    'mobile_secret_789',
    ARRAY[
        'myapp://callback',
        'http://localhost:8080/callback'
    ],
    ARRAY[
        'myapp://logout',
        'http://localhost:8080/'
    ],
    TRUE
)
ON CONFLICT ("client_id") 
DO UPDATE SET 
    "client_name" = EXCLUDED."client_name",
    "client_secret" = EXCLUDED."client_secret",
    "allowed_redirect_uris" = EXCLUDED."allowed_redirect_uris",
    "allowed_logout_uris" = EXCLUDED."allowed_logout_uris";

-- 3. Create a test user table if using the new schema
CREATE TABLE IF NOT EXISTS "user_addresses" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "user_id" UUID NOT NULL,
    "label" TEXT,
    "address_line_1" TEXT NOT NULL,
    "address_line_2" TEXT,
    "city" TEXT NOT NULL,
    "postal_code" TEXT NOT NULL,
    "created_at" TIMESTAMP DEFAULT NOW()
);

-- 4. Verify the data
SELECT 
    "client_id", 
    "client_name", 
    "allowed_redirect_uris", 
    "is_active"
FROM "clients"
ORDER BY "created_at" DESC;

-- =====================================================================================
-- TESTING NOTES:
-- =====================================================================================
-- 
-- CLIENT CREDENTIALS:
-- -------------------
-- Client ID: test_client_123
-- Client Secret: test_secret_456
-- Redirect URI: http://localhost:3000/callback
--
-- OAUTH FLOW:
-- -----------
-- 1. User must be logged in to the UI application first
-- 2. Then call /connect/authorize with the client_id and redirect_uri
-- 3. System will generate an authorization code
-- 4. Exchange the code for an access token using /connect/token
-- 5. Use the access token to call /connect/userinfo
--
-- =====================================================================================
