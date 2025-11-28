/*
 * ======================================================================================
 * 🔐 SYSTEM KEY STORE (ROOT ACCESS ONLY)
 * ======================================================================================
 * PURPOSE: 
 *   Stores encryption keys inside the DB for manual testing/debugging.
 *   Strictly restricted to the 'postgres' superuser.
 * ======================================================================================
 */

-- 1. CREATE THE VAULT TABLE
-- We use an underscore prefix to denote internal system tables
CREATE TABLE IF NOT EXISTS "keystore" (
    "KeyName" TEXT PRIMARY KEY,
    "KeyValue" TEXT NOT NULL,
    "Description" TEXT,
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

-- 2. LOCK IT DOWN (Crucial Step)
-- Revoke access from EVERYONE (including the 'public' group)
REVOKE ALL ON "keystore" FROM PUBLIC;

-- Grant access ONLY to the specific superuser (usually 'postgres')
-- Change 'postgres' to your actual root username if different.
GRANT ALL ON "keystore" TO postgres;

-- 3. INSERT YOUR KEYS (Run this ONCE)
-- ⚠️ WARNING: In a real Prod Env, these should be in Azure KeyVault/AWS KMS.
INSERT INTO "keystore" ("KeyName", "KeyValue", "Description")
VALUES 
    ('app.enc_key',   'YOUR_STRONG_AES_KEY_32_CHARS_LONG!!', 'Used for AES Encryption (users)'),
    ('app.blind_key', 'YOUR_STRONG_PEPPER_KEY_FOR_HASHING',    'Used for HMAC Blind Indexing')
ON CONFLICT ("KeyName") 
DO UPDATE SET "KeyValue" = EXCLUDED."KeyValue"; -- Allow updating keys

/*
 * ======================================================================================
 * 🛠️ HELPER FUNCTION: LOAD KEYS (For Manual Testing)
 * ======================================================================================
 * Usage: Call this function before running manual queries in pgAdmin/DBeaver.
 * It loads the keys from the table into your current Session Memory.
 */
CREATE OR REPLACE FUNCTION debug_LoadKeys()
RETURNS TEXT
LANGUAGE plpgsql
SECURITY DEFINER -- This runs with the privileges of the CREATOR (Root), not the caller
AS $$
DECLARE
    v_enc TEXT;
    v_blind TEXT;
BEGIN
    -- Check if the caller is actually a superuser (Double Security)
    IF NOT (SELECT usesuper FROM pg_user WHERE usename = CURRENT_USER) THEN
        RAISE EXCEPTION 'ACCESS DENIED: Only Superusers can load debug keys.';
    END IF;

    -- Fetch Keys
    SELECT "KeyValue" INTO v_enc FROM "keystore" WHERE "KeyName" = 'app.enc_key';
    SELECT "KeyValue" INTO v_blind FROM "keystore" WHERE "KeyName" = 'app.blind_key';

    -- Load into Session Variables (Memory)
    -- These disappear when you close the connection.
    PERFORM set_config('app.enc_key', v_enc, false);
    PERFORM set_config('app.blind_key', v_blind, false);

    RETURN 'Keys loaded into Session memory. You can now query encrypted data.';
END;
$$;

/*
 * ======================================================================================
 * 🚀 THE REFINED STORED PROCEDURE (Does not touch the table directly)
 * ======================================================================================
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
    
    -- 1. FETCH KEYS FROM SESSION (Manual Injection)
    -- The App (C#) sends these. 
    -- Or 'debug_LoadKeys()' sends these during testing.
    v_enc_key TEXT := current_setting('app.enc_key', true); 
    v_blind_key TEXT := current_setting('app.blind_key', true); 
BEGIN
    -- Security Check: Ensure keys exist before proceeding
    IF v_enc_key IS NULL OR v_blind_key IS NULL THEN
        RAISE EXCEPTION 'SECURITY ERROR: Encryption keys not found in session. ' 
                        'If testing manually, run SELECT debug_LoadKeys(); first.';
    END IF;

    -- 2. Process Email (Encrypt + HMAC)
    IF _email IS NOT NULL THEN
        v_email_enc := pgp_sym_encrypt(_email, v_enc_key);
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
    END IF;

    -- 3. Process Mobile
    IF _mobile IS NOT NULL THEN
        v_mobile_enc := pgp_sym_encrypt(_mobile, v_enc_key);
        v_mobile_hash := encode(hmac(_mobile, v_blind_key, 'sha256'), 'hex');
    END IF;

    -- 4. Insert Public Profile
    INSERT INTO "users" ("DisplayName")
    VALUES (_displayName)
    RETURNING "Id" INTO _newUserId;

    -- 5. Insert Secrets
    INSERT INTO "users" 
    ("Id", "PasswordHash", "Email_Enc", "Email_Hash", "Mobile_Enc", "Mobile_Hash")
    VALUES 
    (_newUserId, _passwordHash, v_email_enc, v_email_hash, v_mobile_enc, v_mobile_hash);
END;
$$;