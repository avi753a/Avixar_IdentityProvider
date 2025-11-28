/*
 * PROCEDURE: sp_SocialLogin
 * PURPOSE: Atomic Login/Register/Link for Social Providers (Google, Microsoft, etc).
 * LOGIC:
 *   1. Try to find user by Provider + SubjectId (Fastest).
 *   2. If not found, try to find by Email (Blind Index Lookup).
 *   3. If found by Email, LINK the provider to existing user.
 *   4. If neither, CREATE a new user and link provider.
 */
CREATE OR REPLACE PROCEDURE sp_SocialLogin(
    _provider TEXT,       -- 'GOOGLE', 'MICROSOFT'
    _subjectId TEXT,      -- Unique ID from Provider
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
    -- 0. Security Check
    IF v_enc_key IS NULL OR v_blind_key IS NULL THEN
        RAISE EXCEPTION 'SECURITY ERROR: Encryption keys not found in session.';
    END IF;

    -- 1. Try Find by Provider Link (Fastest)
    SELECT "UserId" INTO _userId
    FROM "user_providers"
    WHERE "Provider" = _provider::auth_provider AND "ProviderSubjectId" = _subjectId;

    IF _userId IS NOT NULL THEN
        -- User exists and is linked. Update profile if needed (optional)
        RETURN;
    END IF;

    -- 2. Not Linked. Calculate Email Hash for Lookup.
    IF _email IS NOT NULL THEN
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
        
        -- Try Find by Email
        SELECT "Id" INTO v_existing_user_id
        FROM "users"
        WHERE "Email_Hash" = v_email_hash;
    END IF;

    IF v_existing_user_id IS NOT NULL THEN
        -- 3. User exists but not linked. LINK THEM.
        INSERT INTO "user_providers" ("UserId", "Provider", "ProviderSubjectId")
        VALUES (v_existing_user_id, _provider::auth_provider, _subjectId);
        
        _userId := v_existing_user_id;
        RETURN;
    END IF;

    -- 4. User does not exist. CREATE NEW USER.
    -- Encrypt Email
    v_email_enc := pgp_sym_encrypt(_email, v_enc_key);

    -- Insert Core User
    INSERT INTO "users" ("DisplayName", "ProfilePictureUrl")
    VALUES (_displayName, _pictureUrl)
    RETURNING "Id" INTO _userId;

    -- Insert Secrets (No password for social user initially)
    INSERT INTO "users" 
    ("Id", "Email_Enc", "Email_Hash")
    VALUES 
    (_userId, v_email_enc, v_email_hash);

    -- Link Provider
    INSERT INTO "user_providers" ("UserId", "Provider", "ProviderSubjectId")
    VALUES (_userId, _provider::auth_provider, _subjectId);

END;
$$;
