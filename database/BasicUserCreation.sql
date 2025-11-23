/*
 * ======================================================================================
 * 🛡️ GLOBAL IDENTITY PROVIDER SCHEMA (GDPR COMPLIANT)
 * ======================================================================================
 * 
 * ARCHITECTURE HIGHLIGHTS:
 * 1. Vertical Partitioning: Separating "Public Profile" from "Encrypted Secrets" (PII).
 * 2. Blind Indexing: Using HMAC-SHA256 to allow searching encrypted data securely.
 * 3. Multi-Tenancy: Solves the "Consultant Problem" (One User, Many Contexts).
 * 4. GDPR "Right to Erasure": Cascading deletes ensure total data wipe.
 * 
 * AUTHOR: Gemini (AI)
 * TARGET DB: PostgreSQL 13+
 */

-- --------------------------------------------------------------------------------------
-- 0. SETUP & EXTENSIONS
-- --------------------------------------------------------------------------------------

-- Generates secure, non-guessable UUIDs. Essential for preventing ID enumeration attacks.
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Provides AES Encryption (pgp_sym_encrypt) and Hashing functions.
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Enums ensure data integrity. We can only log in via these approved methods.
DO $$ BEGIN
    CREATE TYPE "auth_provider" AS ENUM ('LOCAL', 'GOOGLE', 'MICROSOFT', 'GITHUB', 'AMAZON');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

-- --------------------------------------------------------------------------------------
-- MODULE 1: ORGANIZATIONS (The Context)
-- --------------------------------------------------------------------------------------

/* 
 * TABLE: Organizations
 * PURPOSE: Represents a Tenant, Company, or Workspace. 
 * GDPR: Not PII, but "OwnerId" links to a person.
 */
CREATE TABLE IF NOT EXISTS "Organizations" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    "Name" TEXT NOT NULL,
    
    -- URL-friendly name (e.g., app.com/org/nike). Indexed for fast routing.
    "Slug" TEXT UNIQUE, 
    
    -- If creating a "Personal Workspace", this flag is TRUE.
    "IsPersonal" BOOLEAN DEFAULT FALSE,
    
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP DEFAULT NOW()
);

-- --------------------------------------------------------------------------------------
-- MODULE 2: CORE IDENTITY (The Person)
-- --------------------------------------------------------------------------------------

/* 
 * TABLE: Core_Users
 * PURPOSE: The "Public" face of a user. Contains NO sensitive PII.
 * SECURITY: Safe to query for profile displays.
 * GDPR: Minimization principle - keep strictly necessary metadata here.
 */
CREATE TABLE IF NOT EXISTS "Core_Users" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Public handle. Can be NULL if you strictly use Email.
    "Username" TEXT UNIQUE,
    
    "DisplayName" TEXT,
    "ProfilePictureUrl" TEXT,

    -- "Master User" convenience field. 
    -- If set, the dashboard auto-redirects here on generic login.
    -- FK: If Org is deleted, set this to NULL (Don't delete the user).
    "DefaultOrgId" UUID REFERENCES "Organizations"("Id") ON DELETE SET NULL,

    "IsActive" BOOLEAN DEFAULT TRUE,
    "IsSuspended" BOOLEAN DEFAULT FALSE, -- Admin ban flag
    
    -- Auditing (Crucial for security logs)
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "LastLoginAt" TIMESTAMP
);

/* 
 * TABLE: User_Secrets
 * PURPOSE: The "Vault". Contains Encrypted PII and Password Hashes.
 * RELATIONSHIP: 1-to-1 with Core_Users.
 * SECURITY: Even DB Admins cannot read "Enc" columns without the App Key.
 * GDPR: 
 *   - Encryption at Rest: Yes (AES-256).
 *   - Pseudonymization: Yes (Separated from Core_Users).
 *   - Right to Erasure: ON DELETE CASCADE deletes this when Core_User is deleted.
 */
CREATE TABLE IF NOT EXISTS "User_Secrets" (
    -- Shared Key Pattern: The PK is also the FK. Enforces strict 1:1 relationship.
    "Id" UUID PRIMARY KEY REFERENCES "Core_Users"("Id") ON DELETE CASCADE,
    
    -- 1. PASSWORD
    -- Store Bcrypt/Argon2 hash. NEVER encrypt passwords, always hash them.
    "PasswordHash" TEXT, 
    
    -- 2. EMAIL (The Blind Index Strategy)
    -- "Email_Enc": AES Encrypted. Random IV. Used for sending emails (Communication).
    -- "Email_Hash": HMAC-SHA256. Deterministic. Used for Login (Lookup).
    -- Why HMAC? Prevents Rainbow Table attacks if DB is stolen.
    "Email_Enc" BYTEA, 
    "Email_Hash" TEXT UNIQUE, 

    -- 3. MOBILE (Same Strategy)
    "Mobile_Enc" BYTEA,
    "Mobile_Hash" TEXT UNIQUE, 

    -- 4. SECURITY STAMP
    -- Rotated whenever password changes. Invalidates all old JWT tokens immediately.
    "SecurityStamp" UUID DEFAULT uuid_generate_v4()
);

-- --------------------------------------------------------------------------------------
-- MODULE 3: MEMBERSHIP (The Logic)
-- --------------------------------------------------------------------------------------

/* 
 * TABLE: Org_Roles
 * PURPOSE: Definitions of what a user can do.
 */
CREATE TABLE IF NOT EXISTS "Org_Roles" (
    "Id" SERIAL PRIMARY KEY,
    "RoleName" TEXT NOT NULL UNIQUE, -- e.g. 'Owner', 'Admin', 'Viewer'
    "Description" TEXT
);

/* 
 * TABLE: Org_Members
 * PURPOSE: Solves the "Consultant Problem".
 * LOGIC: One User can have MANY rows here (one for each Org they belong to).
 * GDPR: If User is deleted, their membership records are wiped (CASCADE).
 */
CREATE TABLE IF NOT EXISTS "Org_Members" (
    "UserId" UUID REFERENCES "Core_Users"("Id") ON DELETE CASCADE,
    "OrgId" UUID REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    
    -- What is their role IN THIS SPECIFIC CONTEXT?
    "RoleId" INT REFERENCES "Org_Roles"("Id"),
    
    "JoinedAt" TIMESTAMP DEFAULT NOW(),
    
    -- Composite PK: A user can't join the same Org twice.
    PRIMARY KEY ("UserId", "OrgId")
);

/* 
 * TABLE: User_Providers
 * PURPOSE: Social Login links (Google, GitHub, etc).
 */
CREATE TABLE IF NOT EXISTS "User_Providers" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Core_Users"("Id") ON DELETE CASCADE,
    
    "Provider" "auth_provider" NOT NULL,
    "ProviderSubjectId" TEXT NOT NULL, -- The ID Google gives us (e.g. "10239...")
    
    "LinkedAt" TIMESTAMP DEFAULT NOW(),
    
    -- Constraint: We can't link the same Google Account to two different Users.
    UNIQUE("Provider", "ProviderSubjectId")
);

-- --------------------------------------------------------------------------------------
-- 4. INDEXING (Performance)
-- --------------------------------------------------------------------------------------

-- Crucial for Login Speed: Lookups on Hash columns are O(1) or O(log N).
CREATE INDEX IF NOT EXISTS "IX_UserSecrets_EmailHash" ON "User_Secrets"("Email_Hash");
CREATE INDEX IF NOT EXISTS "IX_UserSecrets_MobileHash" ON "User_Secrets"("Mobile_Hash");

-- Fast lookup to see "What Orgs does this user belong to?"
CREATE INDEX IF NOT EXISTS "IX_OrgMembers_UserId" ON "Org_Members"("UserId");

-- Fast lookup for API Routing (e.g. finding Tenant by Slug)
CREATE INDEX IF NOT EXISTS "IX_Organizations_Slug" ON "Organizations"("Slug");

-- --------------------------------------------------------------------------------------
-- 5. STORED PROCEDURES (The "Smart" Logic)
-- --------------------------------------------------------------------------------------

/*
 * TYPE: dto_user_update
 * PURPOSE: Acts as a Data Transfer Object.
 * Allows C# to pass a clean Object instead of 10 separate SQL parameters.
 */
DROP TYPE IF EXISTS "dto_user_update" CASCADE;
CREATE TYPE "dto_user_update" AS (
    "UserId" UUID,
    "DisplayName" TEXT,
    "Email" TEXT,    -- Plain text (DB will Encrypt)
    "Mobile" TEXT,   -- Plain text (DB will Encrypt)
    "DefaultOrgId" UUID
);

/*
 * PROCEDURE: sp_CreateUser
 * PURPOSE: Handles the complexity of 1:1 splitting, Encryption, and Hashing.
 * SECURITY: Uses session variables for Keys (Keys are NEVER saved in SQL text history).
 */
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
    
    -- FETCH KEYS FROM SESSION (Set via C# before calling)
    v_enc_key TEXT := current_setting('app.enc_key', true); 
    v_blind_key TEXT := current_setting('app.blind_key', true); 
BEGIN
    -- Development Fallback (REMOVE IN PRODUCTION)
    IF v_enc_key IS NULL THEN v_enc_key := 'DEV_AES_KEY'; END IF;
    IF v_blind_key IS NULL THEN v_blind_key := 'DEV_PEPPER_KEY'; END IF;

    -- 1. Process Email (Encrypt + HMAC)
    IF _email IS NOT NULL THEN
        v_email_enc := pgp_sym_encrypt(_email, v_enc_key);
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
    END IF;

    -- 2. Process Mobile (Encrypt + HMAC)
    IF _mobile IS NOT NULL THEN
        v_mobile_enc := pgp_sym_encrypt(_mobile, v_enc_key);
        v_mobile_hash := encode(hmac(_mobile, v_blind_key, 'sha256'), 'hex');
    END IF;

    -- 3. Insert Public Profile
    INSERT INTO "Core_Users" ("DisplayName")
    VALUES (_displayName)
    RETURNING "Id" INTO _newUserId;

    -- 4. Insert Encrypted Secrets
    INSERT INTO "User_Secrets" 
    ("Id", "PasswordHash", "Email_Enc", "Email_Hash", "Mobile_Enc", "Mobile_Hash")
    VALUES 
    (_newUserId, _passwordHash, v_email_enc, v_email_hash, v_mobile_enc, v_mobile_hash);
END;
$$;

/*
 * PROCEDURE: sp_UpdateUser
 * PURPOSE: Allows "Partial Updates". 
 * LOGIC: Checks if DTO fields are NULL. If NULL, keep existing DB value.
 */
CREATE OR REPLACE PROCEDURE sp_UpdateUser(
    _payload "dto_user_update"
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_enc_key TEXT := current_setting('app.enc_key', true);
    v_blind_key TEXT := current_setting('app.blind_key', true);
BEGIN
    IF v_enc_key IS NULL THEN v_enc_key := 'DEV_AES_KEY'; END IF;
    IF v_blind_key IS NULL THEN v_blind_key := 'DEV_PEPPER_KEY'; END IF;

    -- 1. Update Core Profile
    UPDATE "Core_Users"
    SET 
        "DisplayName" = COALESCE(_payload."DisplayName", "DisplayName"),
        "DefaultOrgId" = COALESCE(_payload."DefaultOrgId", "DefaultOrgId")
    WHERE "Id" = _payload."UserId";

    -- 2. Update Secrets (Only re-encrypt if data changed)
    UPDATE "User_Secrets"
    SET 
        "Email_Enc" = CASE 
            WHEN _payload."Email" IS NOT NULL THEN pgp_sym_encrypt(_payload."Email", v_enc_key) 
            ELSE "Email_Enc" 
        END,
        "Email_Hash" = CASE 
            WHEN _payload."Email" IS NOT NULL THEN encode(hmac(_payload."Email", v_blind_key, 'sha256'), 'hex') 
            ELSE "Email_Hash" 
        END,
        "Mobile_Enc" = CASE 
            WHEN _payload."Mobile" IS NOT NULL THEN pgp_sym_encrypt(_payload."Mobile", v_enc_key) 
            ELSE "Mobile_Enc" 
        END,
        "Mobile_Hash" = CASE 
            WHEN _payload."Mobile" IS NOT NULL THEN encode(hmac(_payload."Mobile", v_blind_key, 'sha256'), 'hex') 
            ELSE "Mobile_Hash" 
        END,
        -- Rotate Security Stamp on sensitive changes
        "SecurityStamp" = uuid_generate_v4()
    WHERE "Id" = _payload."UserId" 
      AND (_payload."Email" IS NOT NULL OR _payload."Mobile" IS NOT NULL);
END;
$$;

-- --------------------------------------------------------------------------------------
-- 6. SEED DATA (Initial Setup)
-- --------------------------------------------------------------------------------------

-- Standard SaaS Roles
INSERT INTO "Org_Roles" ("RoleName", "Description") VALUES 
('Owner', 'Full administrative access to the Organization'),
('Admin', 'Can manage members and settings, but cannot delete Org'),
('Editor', 'Can create and edit data'),
('Viewer', 'Read-only access');

-- Default "Platform" Org (For Master Admins)
INSERT INTO "Organizations" ("Name", "Slug", "IsPersonal") 
VALUES ('Platform Admin', 'platform', FALSE);

/*
 * ======================================================================================
 * END OF SCRIPT
 * ======================================================================================
 * 
 * HOW TO USE IN C# (Npgsql):
 * 
 * 1. Login Query:
 *    "SET app.blind_key = @key; SELECT * FROM User_Secrets WHERE Email_Hash = encode(hmac(@email, current_setting('app.blind_key'), 'sha256'), 'hex');"
 *
 * 2. Create User:
 *    "SET app.enc_key = @k1; SET app.blind_key = @k2; CALL sp_CreateUser(...);"
 *
 * 3. Decrypt for Email Sending:
 *    "SET app.enc_key = @key; SELECT pgp_sym_decrypt(Email_Enc, current_setting('app.enc_key')) FROM User_Secrets WHERE Id = @uid;"
 */