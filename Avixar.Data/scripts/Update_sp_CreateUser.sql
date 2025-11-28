/*
 * PROCEDURE: sp_CreateUser (UPDATED)
 * PURPOSE: Handles User Creation with "Upsert" logic.
 * CHANGE: If user exists by Email, update PasswordHash instead of creating duplicate.
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
    
    -- FETCH KEYS FROM SESSION
    v_enc_key TEXT := current_setting('app.enc_key', true); 
    v_blind_key TEXT := current_setting('app.blind_key', true); 
    
    v_existing_id UUID;
BEGIN
    -- Development Fallback
    IF v_enc_key IS NULL THEN v_enc_key := 'DEV_AES_KEY'; END IF;
    IF v_blind_key IS NULL THEN v_blind_key := 'DEV_PEPPER_KEY'; END IF;

    -- 1. Process Email (Encrypt + HMAC)
    IF _email IS NOT NULL THEN
        v_email_enc := pgp_sym_encrypt(_email, v_enc_key);
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
        
        -- CHECK IF USER EXISTS
        SELECT "Id" INTO v_existing_id 
        FROM "users" 
        WHERE "Email_Hash" = v_email_hash;
    END IF;

    -- 2. If User Exists, UPDATE Password and Return ID
    IF v_existing_id IS NOT NULL THEN
        UPDATE "users" 
        SET "PasswordHash" = _passwordHash
        WHERE "Id" = v_existing_id;
        
        _newUserId := v_existing_id;
        RETURN;
    END IF;

    -- 3. Process Mobile (Encrypt + HMAC)
    IF _mobile IS NOT NULL THEN
        v_mobile_enc := pgp_sym_encrypt(_mobile, v_enc_key);
        v_mobile_hash := encode(hmac(_mobile, v_blind_key, 'sha256'), 'hex');
    END IF;

    -- 4. Insert Public Profile
    INSERT INTO "users" ("DisplayName")
    VALUES (_displayName)
    RETURNING "Id" INTO _newUserId;

    -- 5. Insert Encrypted Secrets
    INSERT INTO "users" 
    ("Id", "PasswordHash", "Email_Enc", "Email_Hash", "Mobile_Enc", "Mobile_Hash")
    VALUES 
    (_newUserId, _passwordHash, v_email_enc, v_email_hash, v_mobile_enc, v_mobile_hash);
END;
$$;
