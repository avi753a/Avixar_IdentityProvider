--/*
-- * ======================================================================================
-- * 🛡️ AVIXAR IDENTITY PROVIDER - COMPLETE DATABASE SETUP
-- * ======================================================================================
-- * 
-- * ARCHITECTURE HIGHLIGHTS:
-- * 1. Single Unified Users Table: Combines public profile and encrypted secrets
-- * 2. Blind Indexing: Using HMAC-SHA256 for secure searchable encryption
-- * 3. Multi-Tenancy: One User, Many Organizations (Consultant Problem Solved)
-- * 4. Social Login: Supports Google, Microsoft, GitHub, Amazon
-- * 5. OAuth Clients: Manages authorized applications
-- * 6. Key Management: Secure encryption key storage
-- * 7. GDPR Compliant: Right to Erasure via cascading deletes
-- * 
-- * TARGET DB: PostgreSQL 13+
-- * ======================================================================================
-- */

---- --------------------------------------------------------------------------------------
---- SECTION 1: CLEANUP (Use with caution - drops all existing data)
---- --------------------------------------------------------------------------------------

--DO $$ 
--DECLARE 
--    r RECORD;
--BEGIN 
--    FOR r IN (SELECT oid::regprocedure as name FROM pg_proc WHERE proname IN ('sp_createuser', 'sp_sociallogin', 'sp_updateuser', 'debug_loadkeys')) LOOP 
--        EXECUTE 'DROP PROCEDURE IF EXISTS ' || r.name || ' CASCADE'; 
--    END LOOP; 
--END $$;

--DROP TABLE IF EXISTS "clients" CASCADE;
--DROP TABLE IF EXISTS "user_providers" CASCADE;
--DROP TABLE IF EXISTS "org_users" CASCADE;
--DROP TABLE IF EXISTS "org_roles" CASCADE;
--DROP TABLE IF EXISTS "users" CASCADE;
--DROP TABLE IF EXISTS "orgs" CASCADE;
--DROP TABLE IF EXISTS "keystore" CASCADE;
--DROP TYPE IF EXISTS "dto_user_update" CASCADE;
--DROP TYPE IF EXISTS "auth_provider" CASCADE;

---- --------------------------------------------------------------------------------------
---- SECTION 2: EXTENSIONS & TYPES
---- --------------------------------------------------------------------------------------

---- UUID generation for secure, non-guessable IDs
--CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

---- Encryption and hashing functions
--CREATE EXTENSION IF NOT EXISTS "pgcrypto";

---- Supported authentication providers
--DO $$ BEGIN
--    CREATE TYPE "auth_provider" AS ENUM ('LOCAL', 'GOOGLE', 'MICROSOFT', 'GITHUB', 'AMAZON');
--EXCEPTION
--    WHEN duplicate_object THEN null;
--END $$;

---- --------------------------------------------------------------------------------------
---- SECTION 3: CORE TABLES
---- --------------------------------------------------------------------------------------

--/* 
-- * TABLE: orgs
-- * PURPOSE: Multi-tenant organizations/workspaces
-- */
--CREATE TABLE "orgs" (
--    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
--    "Name" TEXT NOT NULL,
--    "Slug" TEXT UNIQUE,
--    "IsPersonal" BOOLEAN DEFAULT FALSE,
--    "CreatedAt" TIMESTAMP DEFAULT NOW(),
--    "UpdatedAt" TIMESTAMP DEFAULT NOW()
--);

--/* 
-- * TABLE: users
-- * PURPOSE: Unified user table with public profile and encrypted secrets
-- * SECURITY: Combines both public and sensitive data in one table
-- */
--CREATE TABLE "users" (
--    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
--    -- Public Profile
--    "Username" TEXT UNIQUE,
--    "DisplayName" TEXT,
--    "ProfilePictureUrl" TEXT,
--    "DefaultOrgId" UUID REFERENCES "orgs"("Id") ON DELETE SET NULL,
--    "IsActive" BOOLEAN DEFAULT TRUE,
--    "IsSuspended" BOOLEAN DEFAULT FALSE,
--    "CreatedAt" TIMESTAMP DEFAULT NOW(),
--    "LastLoginAt" TIMESTAMP,

--    -- Encrypted Secrets (Blind Index Strategy)
--    "PasswordHash" TEXT,
--    "Email_Enc" BYTEA,
--    "Email_Hash" TEXT UNIQUE,
--    "Mobile_Enc" BYTEA,
--    "Mobile_Hash" TEXT UNIQUE,
--    "SecurityStamp" UUID DEFAULT uuid_generate_v4()
--);

--/* 
-- * TABLE: org_roles
-- * PURPOSE: Role definitions for organization access control
-- */
--CREATE TABLE "org_roles" (
--    "Id" SERIAL PRIMARY KEY,
--    "RoleName" TEXT NOT NULL UNIQUE,
--    "Description" TEXT
--);

--/* 
-- * TABLE: org_users
-- * PURPOSE: Many-to-many relationship between users and organizations
-- */
--CREATE TABLE "org_users" (
--    "UserId" UUID REFERENCES "users"("Id") ON DELETE CASCADE,
--    "OrgId" UUID REFERENCES "orgs"("Id") ON DELETE CASCADE,
--    "RoleId" INT REFERENCES "org_roles"("Id"),
--    "JoinedAt" TIMESTAMP DEFAULT NOW(),
--    PRIMARY KEY ("UserId", "OrgId")
--);

--/* 
-- * TABLE: user_providers
-- * PURPOSE: Links users to social login providers (Google, Microsoft, etc.)
-- */
--CREATE TABLE "user_providers" (
--    "Id" SERIAL PRIMARY KEY,
--    "UserId" UUID NOT NULL REFERENCES "users"("Id") ON DELETE CASCADE,
--    "Provider" "auth_provider" NOT NULL,
--    "ProviderSubjectId" TEXT NOT NULL,
--    "LinkedAt" TIMESTAMP DEFAULT NOW(),
--    UNIQUE("Provider", "ProviderSubjectId")
--);

--/* 
-- * TABLE: clients
-- * PURPOSE: OAuth client applications authorized to use this identity provider
-- */
--CREATE TABLE IF NOT EXISTS "clients" (
--    "client_id" TEXT PRIMARY KEY,
--    "client_name" TEXT NOT NULL,
--    "client_secret" TEXT,
--    "allowed_redirect_uris" TEXT[],
--    "allowed_logout_uris" TEXT[],
--    "is_active" BOOLEAN DEFAULT TRUE,
--    "created_at" TIMESTAMP DEFAULT NOW()
--);

--/* 
-- * TABLE: keystore
-- * PURPOSE: Secure storage for encryption keys (superuser access only)
-- * SECURITY: Restricted to postgres superuser
-- */
--CREATE TABLE IF NOT EXISTS "keystore" (
--    "KeyName" TEXT PRIMARY KEY,
--    "KeyValue" TEXT NOT NULL,
--    "Description" TEXT,
--    "CreatedAt" TIMESTAMP DEFAULT NOW()
--);

---- --------------------------------------------------------------------------------------
---- SECTION 4: SECURITY & PERMISSIONS
---- --------------------------------------------------------------------------------------

---- Lock down keystore table
--REVOKE ALL ON "keystore" FROM PUBLIC;
--GRANT ALL ON "keystore" TO postgres;

---- --------------------------------------------------------------------------------------
---- SECTION 5: INDEXES (Performance Optimization)
---- --------------------------------------------------------------------------------------

--CREATE INDEX IF NOT EXISTS "IX_UserSecrets_EmailHash" ON "users"("Email_Hash");
--CREATE INDEX IF NOT EXISTS "IX_UserSecrets_MobileHash" ON "users"("Mobile_Hash");
--CREATE INDEX IF NOT EXISTS "IX_OrgMembers_UserId" ON "org_users"("UserId");
--CREATE INDEX IF NOT EXISTS "IX_Organizations_Slug" ON "orgs"("Slug");
--CREATE INDEX IF NOT EXISTS "IX_UserProviders_UserId" ON "user_providers"("UserId");
--CREATE INDEX IF NOT EXISTS "IX_UserProviders_Provider" ON "user_providers"("Provider", "ProviderSubjectId");

---- --------------------------------------------------------------------------------------
---- SECTION 6: STORED PROCEDURES
---- --------------------------------------------------------------------------------------

--/*
-- * PROCEDURE: sp_CreateUser
-- * PURPOSE: Create a new user with encrypted email/mobile
-- * FEATURES: Upsert logic, blind indexing, single table insert
-- */
--CREATE OR REPLACE PROCEDURE sp_CreateUser(
--    _displayName TEXT,
--    _email TEXT,
--    _mobile TEXT,
--    _passwordHash TEXT,
--    INOUT _newUserId UUID
--)
--LANGUAGE plpgsql
--AS $$
--DECLARE
--    v_email_hash TEXT;
--    v_mobile_hash TEXT;
--    v_email_enc BYTEA;
--    v_mobile_enc BYTEA;
--    v_enc_key TEXT := current_setting('app.enc_key', true); 
--    v_blind_key TEXT := current_setting('app.blind_key', true); 
--    v_existing_id UUID;
--BEGIN
--    -- Development fallback (remove in production)
--    IF v_enc_key IS NULL THEN v_enc_key := 'DEV_AES_KEY'; END IF;
--    IF v_blind_key IS NULL THEN v_blind_key := 'DEV_PEPPER_KEY'; END IF;

--    -- Process Email
--    IF _email IS NOT NULL THEN
--        v_email_enc := pgp_sym_encrypt(_email, v_enc_key);
--        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
        
--        -- Check if user exists
--        SELECT "Id" INTO v_existing_id FROM "users" WHERE "Email_Hash" = v_email_hash;
--    END IF;

--    -- Upsert Logic: Update if exists
--    IF v_existing_id IS NOT NULL THEN
--        UPDATE "users" 
--        SET "PasswordHash" = _passwordHash
--        WHERE "Id" = v_existing_id;
--        _newUserId := v_existing_id;
--        RETURN;
--    END IF;

--    -- Process Mobile
--    IF _mobile IS NOT NULL THEN
--        v_mobile_enc := pgp_sym_encrypt(_mobile, v_enc_key);
--        v_mobile_hash := encode(hmac(_mobile, v_blind_key, 'sha256'), 'hex');
--    END IF;

--    -- Create new user (single insert)
--    INSERT INTO "users" (
--        "DisplayName", 
--        "PasswordHash", 
--        "Email_Enc", "Email_Hash", 
--        "Mobile_Enc", "Mobile_Hash"
--    )
--    VALUES (
--        _displayName, 
--        _passwordHash, 
--        v_email_enc, v_email_hash, 
--        v_mobile_enc, v_mobile_hash
--    )
--    RETURNING "Id" INTO _newUserId;
--END;
--$$;

--/*
-- * PROCEDURE: sp_SocialLogin
-- * PURPOSE: Atomic login/register/link for social providers
-- * LOGIC:
-- *   1. Try to find user by Provider + SubjectId
-- *   2. If not found, try to find by Email (blind index)
-- *   3. If found by Email, link the provider to existing user
-- *   4. If neither, create new user and link provider
-- */
--CREATE OR REPLACE PROCEDURE sp_SocialLogin(
--    _provider TEXT,
--    _subjectId TEXT,
--    _email TEXT,
--    _displayName TEXT,
--    _pictureUrl TEXT,
--    INOUT _userId UUID
--)
--LANGUAGE plpgsql
--AS $$
--DECLARE
--    v_email_hash TEXT;
--    v_email_enc BYTEA;
--    v_enc_key TEXT := current_setting('app.enc_key', true);
--    v_blind_key TEXT := current_setting('app.blind_key', true);
--    v_existing_user_id UUID;
--BEGIN
--    -- Security check
--    IF v_enc_key IS NULL OR v_blind_key IS NULL THEN
--        RAISE EXCEPTION 'SECURITY ERROR: Encryption keys not found in session.';
--    END IF;

--    -- 1. Try to find by provider link (fastest)
--    SELECT "UserId" INTO _userId
--    FROM "user_providers"
--    WHERE "Provider" = _provider::auth_provider AND "ProviderSubjectId" = _subjectId;

--    IF _userId IS NOT NULL THEN
--        -- User exists and is already linked
--        UPDATE "users" SET "LastLoginAt" = NOW() WHERE "Id" = _userId;
--        RETURN;
--    END IF;

--    -- 2. Try to find by email
--    IF _email IS NOT NULL THEN
--        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
--        SELECT "Id" INTO v_existing_user_id FROM "users" WHERE "Email_Hash" = v_email_hash;
--    END IF;

--    IF v_existing_user_id IS NOT NULL THEN
--        -- 3. User exists but not linked - link them
--        INSERT INTO "user_providers" ("UserId", "Provider", "ProviderSubjectId")
--        VALUES (v_existing_user_id, _provider::auth_provider, _subjectId);
        
--        UPDATE "users" SET "LastLoginAt" = NOW() WHERE "Id" = v_existing_user_id;
--        _userId := v_existing_user_id;
--        RETURN;
--    END IF;

--    -- 4. Create new user (single insert)
--    v_email_enc := pgp_sym_encrypt(_email, v_enc_key);

--    INSERT INTO "users" (
--        "DisplayName", "ProfilePictureUrl", 
--        "Email_Enc", "Email_Hash",
--        "LastLoginAt"
--    )
--    VALUES (
--        _displayName, _pictureUrl, 
--        v_email_enc, v_email_hash,
--        NOW()
--    )
--    RETURNING "Id" INTO _userId;

--    -- Link provider
--    INSERT INTO "user_providers" ("UserId", "Provider", "ProviderSubjectId")
--    VALUES (_userId, _provider::auth_provider, _subjectId);
--END;
--$$;

--/*
-- * PROCEDURE: sp_UpdateUser
-- * PURPOSE: Update user profile and encrypted data
-- * FEATURES: Partial updates, re-encryption, security stamp rotation
-- */
--DROP TYPE IF EXISTS "dto_user_update" CASCADE;
--CREATE TYPE "dto_user_update" AS (
--    "UserId" UUID,
--    "DisplayName" TEXT,
--    "Email" TEXT,
--    "Mobile" TEXT,
--    "DefaultOrgId" UUID
--);

--CREATE OR REPLACE PROCEDURE sp_UpdateUser(
--    _payload "dto_user_update"
--)
--LANGUAGE plpgsql
--AS $$
--DECLARE
--    v_enc_key TEXT := current_setting('app.enc_key', true);
--    v_blind_key TEXT := current_setting('app.blind_key', true);
--BEGIN
--    IF v_enc_key IS NULL THEN v_enc_key := 'DEV_AES_KEY'; END IF;
--    IF v_blind_key IS NULL THEN v_blind_key := 'DEV_PEPPER_KEY'; END IF;

--    -- Update user data
--    UPDATE "users"
--    SET 
--        "DisplayName" = COALESCE(_payload."DisplayName", "DisplayName"),
--        "DefaultOrgId" = COALESCE(_payload."DefaultOrgId", "DefaultOrgId"),
--        "Email_Enc" = CASE 
--            WHEN _payload."Email" IS NOT NULL THEN pgp_sym_encrypt(_payload."Email", v_enc_key) 
--            ELSE "Email_Enc" 
--        END,
--        "Email_Hash" = CASE 
--            WHEN _payload."Email" IS NOT NULL THEN encode(hmac(_payload."Email", v_blind_key, 'sha256'), 'hex') 
--            ELSE "Email_Hash" 
--        END,
--        "Mobile_Enc" = CASE 
--            WHEN _payload."Mobile" IS NOT NULL THEN pgp_sym_encrypt(_payload."Mobile", v_enc_key) 
--            ELSE "Mobile_Enc" 
--        END,
--        "Mobile_Hash" = CASE 
--            WHEN _payload."Mobile" IS NOT NULL THEN encode(hmac(_payload."Mobile", v_blind_key, 'sha256'), 'hex') 
--            ELSE "Mobile_Hash" 
--        END,
--        "SecurityStamp" = CASE
--            WHEN _payload."Email" IS NOT NULL OR _payload."Mobile" IS NOT NULL THEN uuid_generate_v4()
--            ELSE "SecurityStamp"
--        END
--    WHERE "Id" = _payload."UserId";
--END;
--$$;

---- --------------------------------------------------------------------------------------
---- SECTION 7: KEY MANAGEMENT FUNCTIONS
---- --------------------------------------------------------------------------------------

--/*
-- * FUNCTION: debug_LoadKeys
-- * PURPOSE: Load encryption keys from keystore into session (for manual testing)
-- * SECURITY: Only accessible to superusers
-- */
--CREATE OR REPLACE FUNCTION debug_LoadKeys()
--RETURNS TEXT
--LANGUAGE plpgsql
--SECURITY DEFINER
--AS $$
--DECLARE
--    v_enc TEXT;
--    v_blind TEXT;
--BEGIN
--    -- Security check: superuser only
--    IF NOT (SELECT usesuper FROM pg_user WHERE usename = CURRENT_USER) THEN
--        RAISE EXCEPTION 'ACCESS DENIED: Only Superusers can load debug keys.';
--    END IF;

--    -- Fetch keys from keystore
--    SELECT "KeyValue" INTO v_enc FROM "keystore" WHERE "KeyName" = 'app.enc_key';
--    SELECT "KeyValue" INTO v_blind FROM "keystore" WHERE "KeyName" = 'app.blind_key';

--    -- Load into session memory
--    PERFORM set_config('app.enc_key', v_enc, false);
--    PERFORM set_config('app.blind_key', v_blind, false);

--    RETURN 'Keys loaded into session memory. You can now query encrypted data.';
--END;
--$$;

---- --------------------------------------------------------------------------------------
---- SECTION 8: SEED DATA
---- --------------------------------------------------------------------------------------

---- Standard organization roles
--INSERT INTO "org_roles" ("RoleName", "Description") VALUES 
--('Owner', 'Full administrative access'),
--('Admin', 'Can manage members and settings'),
--('Editor', 'Can create and edit data'),
--('Viewer', 'Read-only access');

---- Default platform organization
--INSERT INTO "orgs" ("Name", "Slug", "IsPersonal") 
--VALUES ('Platform Admin', 'platform', FALSE);

---- Sample OAuth client
--INSERT INTO "clients" ("client_id", "client_name", "allowed_redirect_uris", "allowed_logout_uris")
--VALUES (
--    'chat_app', 
--    'My Chat Application', 
--    ARRAY['http://localhost:3000/callback', 'https://myapp.com/callback'],
--    ARRAY['http://localhost:3000/', 'https://myapp.com/']
--);

---- Encryption keys (⚠️ CHANGE THESE IN PRODUCTION!)
--INSERT INTO "keystore" ("KeyName", "KeyValue", "Description")
--VALUES 
--    ('app.enc_key',   'YOUR_STRONG_AES_KEY_32_CHARS_LONG!!', 'AES Encryption Key'),
--    ('app.blind_key', 'YOUR_STRONG_PEPPER_KEY_FOR_HASHING',  'HMAC Blind Index Key')
--ON CONFLICT ("KeyName") 
--DO UPDATE SET "KeyValue" = EXCLUDED."KeyValue";

--/*
-- * ======================================================================================
-- * USAGE EXAMPLES (C# with Npgsql)
-- * ======================================================================================
-- * 
-- * 1. LOGIN QUERY:
-- *    SET app.blind_key = @key; 
-- *    SELECT * FROM users 
-- *    WHERE Email_Hash = encode(hmac(@email, current_setting('app.blind_key'), 'sha256'), 'hex');
-- *
-- * 2. CREATE USER:
-- *    SET app.enc_key = @k1; SET app.blind_key = @k2; 
-- *    CALL sp_CreateUser(@name, @email, @mobile, @hash, @userId);
-- *
-- * 3. SOCIAL LOGIN:
-- *    SET app.enc_key = @k1; SET app.blind_key = @k2;
-- *    CALL sp_SocialLogin('GOOGLE', @subId, @email, @name, @pic, @userId);
-- *
-- * 4. DECRYPT EMAIL (for sending):
-- *    SET app.enc_key = @key; 
-- *    SELECT pgp_sym_decrypt(Email_Enc, current_setting('app.enc_key')) 
-- *    FROM users WHERE Id = @uid;
-- *
-- * 5. MANUAL TESTING (in pgAdmin):
-- *    SELECT debug_LoadKeys();  -- Run this first
-- *    CALL sp_CreateUser('Test User', 'test@example.com', NULL, 'hash123', NULL);
-- * ======================================================================================
-- */


-- ALTER TABLE users
--ADD COLUMN first_name TEXT,
--ADD COLUMN last_name TEXT,
--ADD CONSTRAINT first_name_len CHECK (length(first_name) < 300),
--ADD CONSTRAINT last_name_len CHECK (length(last_name) < 300);