-- =====================================================================================
-- AVIXAR IDENTITY PROVIDER - DATABASE INITIALIZATION
-- =====================================================================================
-- PostgreSQL 13+ Database Setup Script
-- Run this script to initialize the database for Avixar Identity Provider
-- =====================================================================================

-- Create clients table for OAuth applications
CREATE TABLE IF NOT EXISTS "clients" (
    "client_id" TEXT PRIMARY KEY,
    "client_name" TEXT NOT NULL,
    "client_secret" TEXT,
    "allowed_redirect_uris" TEXT[],
    "allowed_logout_uris" TEXT[],
    "is_active" BOOLEAN DEFAULT TRUE,
    "created_at" TIMESTAMP DEFAULT NOW()
);

-- Create user_addresses table
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

-- Insert sample OAuth clients for testing
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
    'test_secret_456',
    ARRAY[
        'http://localhost:3000/callback',
        'http://localhost:3000/auth/callback',
        'https://oauth.pstmn.io/v1/callback'
    ],
    ARRAY[
        'http://localhost:3000/',
        'http://localhost:3000/logout'
    ],
    TRUE
),
(
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
ON CONFLICT ("client_id") DO NOTHING;

-- Verify setup
SELECT 
    "client_id", 
    "client_name", 
    "allowed_redirect_uris", 
    "is_active"
FROM "clients"
ORDER BY "created_at" DESC;
