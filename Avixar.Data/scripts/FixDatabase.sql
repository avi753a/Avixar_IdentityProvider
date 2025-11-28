/*
 * SCRIPT: FixDatabase.sql
 * PURPOSE: Fixes the schema by merging the split "users" table definitions into one.
 *          Also updates stored procedures to work with the single table.
 */

-- 1. DROP EVERYTHING (Clean Slate)
DO $$ 
DECLARE 
    r RECORD;
BEGIN 
    FOR r IN (SELECT oid::regprocedure as name FROM pg_proc WHERE proname IN ('sp_createuser', 'sp_sociallogin', 'sp_updateuser')) LOOP 
        EXECUTE 'DROP PROCEDURE IF EXISTS ' || r.name || ' CASCADE'; 
    END LOOP; 
END $$;

DROP TABLE IF EXISTS "user_providers" CASCADE;
DROP TABLE IF EXISTS "org_users" CASCADE;
DROP TABLE IF EXISTS "org_roles" CASCADE;
DROP TABLE IF EXISTS "users" CASCADE; -- This drops the single incomplete table
DROP TABLE IF EXISTS "orgs" CASCADE;
DROP TYPE IF EXISTS "dto_user_update" CASCADE;
DROP TYPE IF EXISTS "auth_provider" CASCADE;

-- 2. RE-CREATE TYPES & EXTENSIONS
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

DO $$ BEGIN
    CREATE TYPE "auth_provider" AS ENUM ('LOCAL', 'GOOGLE', 'MICROSOFT', 'GITHUB', 'AMAZON');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

-- 3. CREATE TABLES

-- ORGS
CREATE TABLE "orgs" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Name" TEXT NOT NULL,
    "Slug" TEXT UNIQUE, 
    "IsPersonal" BOOLEAN DEFAULT FALSE,
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP DEFAULT NOW()
);

-- USERS (Merged Definition)
CREATE TABLE "users" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Public Profile
    "Username" TEXT UNIQUE,
    "DisplayName" TEXT,
    "ProfilePictureUrl" TEXT,
    "DefaultOrgId" UUID REFERENCES "orgs"("Id") ON DELETE SET NULL,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "IsSuspended" BOOLEAN DEFAULT FALSE,
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "LastLoginAt" TIMESTAMP,

    -- Secrets (Formerly in second table definition)
    "PasswordHash" TEXT, 
    "Email_Enc" BYTEA, 
    "Email_Hash" TEXT UNIQUE, 
    "Mobile_Enc" BYTEA,
    "Mobile_Hash" TEXT UNIQUE, 
    "SecurityStamp" UUID DEFAULT uuid_generate_v4()
);

-- ROLES
CREATE TABLE "org_roles" (
    "Id" SERIAL PRIMARY KEY,
    "RoleName" TEXT NOT NULL UNIQUE,
    "Description" TEXT
);

-- ORG USERS
CREATE TABLE "org_users" (
    "UserId" UUID REFERENCES "users"("Id") ON DELETE CASCADE,
    "OrgId" UUID REFERENCES "orgs"("Id") ON DELETE CASCADE,
    "RoleId" INT REFERENCES "org_roles"("Id"),
    "JoinedAt" TIMESTAMP DEFAULT NOW(),
    PRIMARY KEY ("UserId", "OrgId")
);

-- USER PROVIDERS
CREATE TABLE "user_providers" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "users"("Id") ON DELETE CASCADE,
    "Provider" "auth_provider" NOT NULL,
    "ProviderSubjectId" TEXT NOT NULL,
    "LinkedAt" TIMESTAMP DEFAULT NOW(),
    UNIQUE("Provider", "ProviderSubjectId")
);

-- 4. INDEXES
CREATE INDEX IF NOT EXISTS "IX_UserSecrets_EmailHash" ON "users"("Email_Hash");
CREATE INDEX IF NOT EXISTS "IX_UserSecrets_MobileHash" ON "users"("Mobile_Hash");
CREATE INDEX IF NOT EXISTS "IX_OrgMembers_UserId" ON "org_users"("UserId");
CREATE INDEX IF NOT EXISTS "IX_Organizations_Slug" ON "orgs"("Slug");

-- 5. STORED PROCEDURES

-- sp_CreateUser (Fixed for Single Table + Upsert Logic)
CREATE OR REPLACE PROCEDURE sp_CreateUser(
    _displayName TEXT,
    _email TEXT,
    _mobile TEXT,
    _passwordHash TEXT,
    INOUT _newUserId UUID
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_email_hash TEXT;
    v_mobile_hash TEXT;
    v_email_enc BYTEA;
    v_mobile_enc BYTEA;
    v_enc_key TEXT := current_setting('app.enc_key', true); 
    v_blind_key TEXT := current_setting('app.blind_key', true); 
    v_existing_id UUID;
BEGIN
    IF v_enc_key IS NULL THEN v_enc_key := 'DEV_AES_KEY'; END IF;
    IF v_blind_key IS NULL THEN v_blind_key := 'DEV_PEPPER_KEY'; END IF;

    -- Process Email
    IF _email IS NOT NULL THEN
        v_email_enc := pgp_sym_encrypt(_email, v_enc_key);
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
        
        -- Check existence
        SELECT "Id" INTO v_existing_id FROM "users" WHERE "Email_Hash" = v_email_hash;
    END IF;

    -- Upsert Logic
    IF v_existing_id IS NOT NULL THEN
        UPDATE "users" 
        SET "PasswordHash" = _passwordHash
        WHERE "Id" = v_existing_id;
        _newUserId := v_existing_id;
        RETURN;
    END IF;

    -- Process Mobile
    IF _mobile IS NOT NULL THEN
        v_mobile_enc := pgp_sym_encrypt(_mobile, v_enc_key);
        v_mobile_hash := encode(hmac(_mobile, v_blind_key, 'sha256'), 'hex');
    END IF;

    -- Single Insert
    INSERT INTO "users" (
        "DisplayName", 
        "PasswordHash", 
        "Email_Enc", "Email_Hash", 
        "Mobile_Enc", "Mobile_Hash"
    )
    VALUES (
        _displayName, 
        _passwordHash, 
        v_email_enc, v_email_hash, 
        v_mobile_enc, v_mobile_hash
    )
    RETURNING "Id" INTO _newUserId;
END;
$$;

-- sp_SocialLogin (Fixed for Single Table)
CREATE OR REPLACE PROCEDURE sp_SocialLogin(
    _provider TEXT,
    _subjectId TEXT,
    _email TEXT,
    _displayName TEXT,
    _pictureUrl TEXT,
    INOUT _userId UUID
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_email_hash TEXT;
    v_email_enc BYTEA;
    v_enc_key TEXT := current_setting('app.enc_key', true);
    v_blind_key TEXT := current_setting('app.blind_key', true);
    v_existing_user_id UUID;
BEGIN
    IF v_enc_key IS NULL OR v_blind_key IS NULL THEN
        RAISE EXCEPTION 'SECURITY ERROR: Encryption keys not found in session.';
    END IF;

    -- 1. Try Find by Provider Link
    SELECT "UserId" INTO _userId
    FROM "user_providers"
    WHERE "Provider" = _provider::auth_provider AND "ProviderSubjectId" = _subjectId;

    IF _userId IS NOT NULL THEN
        RETURN;
    END IF;

    -- 2. Try Find by Email
    IF _email IS NOT NULL THEN
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
        SELECT "Id" INTO v_existing_user_id FROM "users" WHERE "Email_Hash" = v_email_hash;
    END IF;

    IF v_existing_user_id IS NOT NULL THEN
        -- Link existing
        INSERT INTO "user_providers" ("UserId", "Provider", "ProviderSubjectId")
        VALUES (v_existing_user_id, _provider::auth_provider, _subjectId);
        _userId := v_existing_user_id;
        RETURN;
    END IF;

    -- 3. Create New User (Single Insert)
    v_email_enc := pgp_sym_encrypt(_email, v_enc_key);

    INSERT INTO "users" (
        "DisplayName", "ProfilePictureUrl", 
        "Email_Enc", "Email_Hash"
    )
    VALUES (
        _displayName, _pictureUrl, 
        v_email_enc, v_email_hash
    )
    RETURNING "Id" INTO _userId;

    -- Link Provider
    INSERT INTO "user_providers" ("UserId", "Provider", "ProviderSubjectId")
    VALUES (_userId, _provider::auth_provider, _subjectId);
END;
$$;

-- 6. SEED DATA
INSERT INTO "org_roles" ("RoleName", "Description") VALUES 
('Owner', 'Full administrative access'),
('Admin', 'Can manage members'),
('Editor', 'Can edit data'),
('Viewer', 'Read-only');

INSERT INTO "orgs" ("Name", "Slug", "IsPersonal") 
VALUES ('Platform Admin', 'platform', FALSE);
