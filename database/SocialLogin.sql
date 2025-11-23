/*
 * ======================================================================================
 * PROCEDURE: sp_SocialLogin
 * ======================================================================================
 * PURPOSE: Handles "Sign in with Google/GitHub/Microsoft".
 * LOGIC:
 *   1. Check if Provider Link exists (e.g., Google ID "123"). If yes, Login.
 *   2. If no, check if Email exists (via Blind Index). If yes, Link & Login.
 *   3. If no, Register new User (Public + Secrets + Provider Link).
 * 
 * SECURITY: Requires 'app.enc_key' and 'app.blind_key' in Session.
 */

CREATE OR REPLACE PROCEDURE sp_SocialLogin(
    _provider "auth_provider",   -- Enum: 'GOOGLE', 'MICROSOFT', etc.
    _providerSubjectId TEXT,     -- The ID from Google (e.g. "10839...")
    _email TEXT,                 -- Email from Google (Can be NULL)
    _displayName TEXT,           -- Name from Google
    _profilePicUrl TEXT,         -- Avatar from Google
    INOUT _userId UUID           -- OUTPUT: The User's ID
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_enc_key TEXT := current_setting('app.enc_key', true);
    v_blind_key TEXT := current_setting('app.blind_key', true);
    v_email_hash TEXT;
    v_email_enc BYTEA;
    v_existing_user_id UUID;
BEGIN
    -- 0. Security Check
    IF v_enc_key IS NULL OR v_blind_key IS NULL THEN
        RAISE EXCEPTION 'SECURITY ERROR: Encryption keys missing in session.';
    END IF;

    -- ----------------------------------------------------------------------
    -- STEP 1: CHECK PROVIDER TABLE (Fastest Path)
    -- ----------------------------------------------------------------------
    SELECT "UserId" INTO _userId
    FROM "User_Providers"
    WHERE "Provider" = _provider 
      AND "ProviderSubjectId" = _providerSubjectId;

    -- If found, we are done! Return the ID.
    IF _userId IS NOT NULL THEN
        -- Optional: Update LastLoginAt
        UPDATE "Core_Users" SET "LastLoginAt" = NOW() WHERE "Id" = _userId;
        RETURN;
    END IF;

    -- ----------------------------------------------------------------------
    -- STEP 2: CHECK EMAIL BLIND INDEX (Link Existing Account)
    -- ----------------------------------------------------------------------
    -- If we didn't find the Google ID, maybe they registered via Email/Pass before?
    
    IF _email IS NOT NULL THEN
        -- Calculate Hash to search
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');

        SELECT "Id" INTO v_existing_user_id
        FROM "User_Secrets"
        WHERE "Email_Hash" = v_email_hash;

        IF v_existing_user_id IS NOT NULL THEN
            -- FOUND! Link this Google account to the existing user.
            INSERT INTO "User_Providers" ("UserId", "Provider", "ProviderSubjectId")
            VALUES (v_existing_user_id, _provider, _providerSubjectId);
            
            -- Update profile pic if they didn't have one
            UPDATE "Core_Users" 
            SET "ProfilePictureUrl" = COALESCE("ProfilePictureUrl", _profilePicUrl),
                "LastLoginAt" = NOW()
            WHERE "Id" = v_existing_user_id;

            _userId := v_existing_user_id;
            RETURN;
        END IF;
    END IF;

    -- ----------------------------------------------------------------------
    -- STEP 3: REGISTER NEW USER (The "Fresh" Path)
    -- ----------------------------------------------------------------------
    -- No provider link, no existing email. Create everything.

    -- A. Encrypt Email (If provided)
    IF _email IS NOT NULL THEN
        v_email_enc := pgp_sym_encrypt(_email, v_enc_key);
        -- v_email_hash is already calculated in Step 2
    END IF;

    -- B. Insert Core User
    INSERT INTO "Core_Users" ("DisplayName", "ProfilePictureUrl", "LastLoginAt")
    VALUES (_displayName, _profilePicUrl, NOW())
    RETURNING "Id" INTO _userId;

    -- C. Insert Secrets (Password is NULL for Social Login)
    INSERT INTO "User_Secrets" 
    ("Id", "PasswordHash", "Email_Enc", "Email_Hash", "Mobile_Enc", "Mobile_Hash")
    VALUES 
    (_userId, NULL, v_email_enc, v_email_hash, NULL, NULL);

    -- D. Link Provider
    INSERT INTO "User_Providers" ("UserId", "Provider", "ProviderSubjectId")
    VALUES (_userId, _provider, _providerSubjectId);

    -- Done! _userId is already set.
END;
$$;
