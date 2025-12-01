--
-- PostgreSQL database dump
--

\restrict K6cE9AUCwtXZTP86ccyFKJAf2nItkCmFzZ7xcKjCZbpIfjmL5Z02Vb0H9ATyidt

-- Dumped from database version 18.1
-- Dumped by pg_dump version 18.1

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: pgcrypto; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;


--
-- Name: EXTENSION pgcrypto; Type: COMMENT; Schema: -; Owner: 
--

COMMENT ON EXTENSION pgcrypto IS 'cryptographic functions';


--
-- Name: uuid-ossp; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA public;


--
-- Name: EXTENSION "uuid-ossp"; Type: COMMENT; Schema: -; Owner: 
--

COMMENT ON EXTENSION "uuid-ossp" IS 'generate universally unique identifiers (UUIDs)';


--
-- Name: auth_provider; Type: TYPE; Schema: public; Owner: appuser
--

CREATE TYPE public.auth_provider AS ENUM (
    'LOCAL',
    'GOOGLE',
    'MICROSOFT',
    'GITHUB',
    'AMAZON'
);


ALTER TYPE public.auth_provider OWNER TO appuser;

--
-- Name: log_user_settings_changes(); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.log_user_settings_changes() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO "user_settings_history" (
        "user_id",
        "two_factor_enabled",
        "email_verified",
        "email_verified_at",
        "email_notifications",
        "changed_at"
    ) VALUES (
        OLD."user_id",
        OLD."two_factor_enabled",
        OLD."email_verified",
        OLD."email_verified_at",
        OLD."email_notifications",
        NOW()
    );
    
    NEW."updated_at" = NOW();
    RETURN NEW;
END;
$$;


ALTER FUNCTION public.log_user_settings_changes() OWNER TO postgres;

--
-- Name: sp_createuser(text, text, text, text, uuid); Type: PROCEDURE; Schema: public; Owner: appuser
--

CREATE PROCEDURE public.sp_createuser(IN _displayname text, IN _email text, IN _mobile text, IN _passwordhash text, INOUT _newuserid uuid)
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
        
        -- Check existence in SECRETS table
        SELECT "Id" INTO v_existing_id FROM "user_secrets" WHERE "Email_Hash" = v_email_hash;
    END IF;

    -- Upsert Logic
    IF v_existing_id IS NOT NULL THEN
        UPDATE "user_secrets" 
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

    -- 1. Insert Public Profile
    INSERT INTO "users" ("DisplayName")
    VALUES (_displayName)
    RETURNING "Id" INTO _newUserId;

    -- 2. Insert Secrets
    INSERT INTO "user_secrets" (
        "Id", "PasswordHash", 
        "Email_Enc", "Email_Hash", 
        "Mobile_Enc", "Mobile_Hash"
    )
    VALUES (
        _newUserId, _passwordHash, 
        v_email_enc, v_email_hash, 
        v_mobile_enc, v_mobile_hash
    );
END;
$$;


ALTER PROCEDURE public.sp_createuser(IN _displayname text, IN _email text, IN _mobile text, IN _passwordhash text, INOUT _newuserid uuid) OWNER TO appuser;

--
-- Name: sp_registeruser(text, text, text, text, uuid); Type: PROCEDURE; Schema: public; Owner: postgres
--

CREATE PROCEDURE public.sp_registeruser(IN _displayname text, IN _email text, IN _mobile text, IN _passwordhash text, INOUT _newuserid uuid)
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_enc_key TEXT; v_blind_key TEXT;
    v_email_hash TEXT; v_email_enc BYTEA;
    v_mobile_hash TEXT; v_mobile_enc BYTEA;
BEGIN
    -- Fetch Keys
    v_enc_key := current_setting('app.enc_key', true);
    v_blind_key := current_setting('app.blind_key', true);

    IF v_enc_key IS NULL THEN SELECT "KeyValue" INTO v_enc_key FROM keystore WHERE "KeyName" = 'app.enc_key'; END IF;
    IF v_blind_key IS NULL THEN SELECT "KeyValue" INTO v_blind_key FROM keystore WHERE "KeyName" = 'app.blind_key'; END IF;

    -- Logic
    IF _email IS NOT NULL THEN
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
        
        -- CHECK DUPLICATE (New Table Name)
        IF EXISTS (SELECT 1 FROM user_secrets WHERE "Email_Hash" = v_email_hash) THEN
            RAISE EXCEPTION 'USER_EXISTS: An account with this email already exists.';
        END IF;

        v_email_enc := pgp_sym_encrypt(_email, v_enc_key);
    ELSE
        RAISE EXCEPTION 'Email is required.';
    END IF;

    IF _mobile IS NOT NULL THEN
        v_mobile_hash := encode(hmac(_mobile, v_blind_key, 'sha256'), 'hex');
        v_mobile_enc := pgp_sym_encrypt(_mobile, v_enc_key);
    END IF;

    -- INSERT (New Table Names)
    INSERT INTO users ("DisplayName", "LastLoginAt") VALUES (_displayName, NOW()) RETURNING "Id" INTO _newUserId;
    INSERT INTO user_secrets ("Id", "PasswordHash", "Email_Enc", "Email_Hash", "Mobile_Enc", "Mobile_Hash")
    VALUES (_newUserId, _passwordHash, v_email_enc, v_email_hash, v_mobile_enc, v_mobile_hash);
END;
$$;


ALTER PROCEDURE public.sp_registeruser(IN _displayname text, IN _email text, IN _mobile text, IN _passwordhash text, INOUT _newuserid uuid) OWNER TO postgres;

--
-- Name: sp_sociallogin(text, text, text, text, text, uuid); Type: PROCEDURE; Schema: public; Owner: appuser
--

CREATE PROCEDURE public.sp_sociallogin(IN _provider text, IN _subjectid text, IN _email text, IN _displayname text, IN _pictureurl text, INOUT _userid uuid)
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

    -- 2. Try Find by Email (in SECRETS table)
    IF _email IS NOT NULL THEN
        v_email_hash := encode(hmac(_email, v_blind_key, 'sha256'), 'hex');
        SELECT "Id" INTO v_existing_user_id FROM "user_secrets" WHERE "Email_Hash" = v_email_hash;
    END IF;

    IF v_existing_user_id IS NOT NULL THEN
        -- Link existing
        INSERT INTO "user_providers" ("UserId", "Provider", "ProviderSubjectId")
        VALUES (v_existing_user_id, _provider::auth_provider, _subjectId);
        _userId := v_existing_user_id;
        RETURN;
    END IF;

    -- 3. Create New User (Split Insert)
    v_email_enc := pgp_sym_encrypt(_email, v_enc_key);

    -- Insert Public
    INSERT INTO "users" ("DisplayName", "ProfilePictureUrl")
    VALUES (_displayName, _pictureUrl)
    RETURNING "Id" INTO _userId;

    -- Insert Secrets
    INSERT INTO "user_secrets" ("Id", "Email_Enc", "Email_Hash")
    VALUES (_userId, v_email_enc, v_email_hash);

    -- Link Provider
    INSERT INTO "user_providers" ("UserId", "Provider", "ProviderSubjectId")
    VALUES (_userId, _provider::auth_provider, _subjectId);
END;
$$;


ALTER PROCEDURE public.sp_sociallogin(IN _provider text, IN _subjectid text, IN _email text, IN _displayname text, IN _pictureurl text, INOUT _userid uuid) OWNER TO appuser;

--
-- Name: trg_fn_log_history(); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.trg_fn_log_history() RETURNS trigger
    LANGUAGE plpgsql
    AS $_$
DECLARE
    hist_table text := TG_TABLE_NAME || '_history';
    col_list text;
    select_list text;
BEGIN
    -- We map columns explicitly to ignore the 'history_id' serial column
    -- Ordering ensures data lines up perfectly.
    SELECT 
        string_agg(quote_ident(column_name), ', ' ORDER BY ordinal_position),
        string_agg('$1.' || quote_ident(column_name), ', ' ORDER BY ordinal_position)
    INTO col_list, select_list
    FROM information_schema.columns
    WHERE table_name = TG_TABLE_NAME
      AND table_schema = TG_TABLE_SCHEMA;

    EXECUTE format(
        'INSERT INTO public.%I (%s, history_event, history_created_at) 
         SELECT %s, %L, NOW()',
        hist_table,
        col_list,
        select_list,
        TG_OP
    )
    USING OLD;

    RETURN OLD;
END;
$_$;


ALTER FUNCTION public.trg_fn_log_history() OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: clients; Type: TABLE; Schema: public; Owner: appuser
--

CREATE TABLE public.clients (
    client_id text NOT NULL,
    client_name text NOT NULL,
    client_secret text,
    allowed_redirect_uris text[],
    allowed_logout_uris text[],
    is_active boolean DEFAULT true,
    created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.clients OWNER TO appuser;

--
-- Name: clients_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.clients_history (
    client_id text CONSTRAINT clients_client_id_not_null NOT NULL,
    client_name text CONSTRAINT clients_client_name_not_null NOT NULL,
    client_secret text,
    allowed_redirect_uris text[],
    allowed_logout_uris text[],
    is_active boolean,
    created_at timestamp without time zone,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.clients_history OWNER TO postgres;

--
-- Name: clients_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.clients_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.clients_history_history_id_seq OWNER TO postgres;

--
-- Name: clients_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.clients_history_history_id_seq OWNED BY public.clients_history.history_id;


--
-- Name: keystore; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.keystore (
    "KeyName" text CONSTRAINT "_System_KeyStore_Keyname_not_null" NOT NULL,
    "KeyValue" text CONSTRAINT "_System_KeyStore_KeyValue_not_null" NOT NULL,
    "Description" text,
    "CreatedAt" timestamp without time zone DEFAULT now()
);


ALTER TABLE public.keystore OWNER TO postgres;

--
-- Name: keystore_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.keystore_history (
    "KeyName" text CONSTRAINT "_System_KeyStore_Keyname_not_null" NOT NULL,
    "KeyValue" text CONSTRAINT "_System_KeyStore_KeyValue_not_null" NOT NULL,
    "Description" text,
    "CreatedAt" timestamp without time zone,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.keystore_history OWNER TO postgres;

--
-- Name: keystore_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.keystore_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.keystore_history_history_id_seq OWNER TO postgres;

--
-- Name: keystore_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.keystore_history_history_id_seq OWNED BY public.keystore_history.history_id;


--
-- Name: org_roles; Type: TABLE; Schema: public; Owner: appuser
--

CREATE TABLE public.org_roles (
    "Id" integer NOT NULL,
    "RoleName" text NOT NULL,
    "Description" text
);


ALTER TABLE public.org_roles OWNER TO appuser;

--
-- Name: org_roles_Id_seq; Type: SEQUENCE; Schema: public; Owner: appuser
--

CREATE SEQUENCE public."org_roles_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."org_roles_Id_seq" OWNER TO appuser;

--
-- Name: org_roles_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: appuser
--

ALTER SEQUENCE public."org_roles_Id_seq" OWNED BY public.org_roles."Id";


--
-- Name: org_roles_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.org_roles_history (
    "Id" integer CONSTRAINT "org_roles_Id_not_null" NOT NULL,
    "RoleName" text CONSTRAINT "org_roles_RoleName_not_null" NOT NULL,
    "Description" text,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.org_roles_history OWNER TO postgres;

--
-- Name: org_roles_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.org_roles_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.org_roles_history_history_id_seq OWNER TO postgres;

--
-- Name: org_roles_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.org_roles_history_history_id_seq OWNED BY public.org_roles_history.history_id;


--
-- Name: org_users; Type: TABLE; Schema: public; Owner: appuser
--

CREATE TABLE public.org_users (
    "UserId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "RoleId" integer,
    "JoinedAt" timestamp without time zone DEFAULT now()
);


ALTER TABLE public.org_users OWNER TO appuser;

--
-- Name: org_users_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.org_users_history (
    "UserId" uuid CONSTRAINT "org_users_UserId_not_null" NOT NULL,
    "OrgId" uuid CONSTRAINT "org_users_OrgId_not_null" NOT NULL,
    "RoleId" integer,
    "JoinedAt" timestamp without time zone,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.org_users_history OWNER TO postgres;

--
-- Name: org_users_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.org_users_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.org_users_history_history_id_seq OWNER TO postgres;

--
-- Name: org_users_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.org_users_history_history_id_seq OWNED BY public.org_users_history.history_id;


--
-- Name: orgs; Type: TABLE; Schema: public; Owner: appuser
--

CREATE TABLE public.orgs (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "Name" text NOT NULL,
    "Slug" text,
    "IsPersonal" boolean DEFAULT false,
    "CreatedAt" timestamp without time zone DEFAULT now(),
    "UpdatedAt" timestamp without time zone DEFAULT now()
);


ALTER TABLE public.orgs OWNER TO appuser;

--
-- Name: orgs_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.orgs_history (
    "Id" uuid CONSTRAINT "orgs_Id_not_null" NOT NULL,
    "Name" text CONSTRAINT "orgs_Name_not_null" NOT NULL,
    "Slug" text,
    "IsPersonal" boolean,
    "CreatedAt" timestamp without time zone,
    "UpdatedAt" timestamp without time zone,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.orgs_history OWNER TO postgres;

--
-- Name: orgs_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.orgs_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.orgs_history_history_id_seq OWNER TO postgres;

--
-- Name: orgs_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.orgs_history_history_id_seq OWNED BY public.orgs_history.history_id;


--
-- Name: user_addresses; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_addresses (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    label text,
    address_line_1 text NOT NULL,
    address_line_2 text,
    city text NOT NULL,
    postal_code text NOT NULL,
    created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.user_addresses OWNER TO postgres;

--
-- Name: user_addresses_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_addresses_history (
    id uuid CONSTRAINT user_addresses_id_not_null NOT NULL,
    user_id uuid CONSTRAINT user_addresses_user_id_not_null NOT NULL,
    label text,
    address_line_1 text CONSTRAINT user_addresses_address_line_1_not_null NOT NULL,
    address_line_2 text,
    city text CONSTRAINT user_addresses_city_not_null NOT NULL,
    postal_code text CONSTRAINT user_addresses_postal_code_not_null NOT NULL,
    created_at timestamp without time zone,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.user_addresses_history OWNER TO postgres;

--
-- Name: user_addresses_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_addresses_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_addresses_history_history_id_seq OWNER TO postgres;

--
-- Name: user_addresses_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_addresses_history_history_id_seq OWNED BY public.user_addresses_history.history_id;


--
-- Name: user_providers; Type: TABLE; Schema: public; Owner: appuser
--

CREATE TABLE public.user_providers (
    "Id" integer NOT NULL,
    "UserId" uuid NOT NULL,
    "Provider" public.auth_provider NOT NULL,
    "ProviderSubjectId" text NOT NULL,
    "LinkedAt" timestamp without time zone DEFAULT now()
);


ALTER TABLE public.user_providers OWNER TO appuser;

--
-- Name: user_providers_Id_seq; Type: SEQUENCE; Schema: public; Owner: appuser
--

CREATE SEQUENCE public."user_providers_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."user_providers_Id_seq" OWNER TO appuser;

--
-- Name: user_providers_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: appuser
--

ALTER SEQUENCE public."user_providers_Id_seq" OWNED BY public.user_providers."Id";


--
-- Name: user_providers_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_providers_history (
    "Id" integer CONSTRAINT "user_providers_Id_not_null" NOT NULL,
    "UserId" uuid CONSTRAINT "user_providers_UserId_not_null" NOT NULL,
    "Provider" public.auth_provider CONSTRAINT "user_providers_Provider_not_null" NOT NULL,
    "ProviderSubjectId" text CONSTRAINT "user_providers_ProviderSubjectId_not_null" NOT NULL,
    "LinkedAt" timestamp without time zone,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.user_providers_history OWNER TO postgres;

--
-- Name: user_providers_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_providers_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_providers_history_history_id_seq OWNER TO postgres;

--
-- Name: user_providers_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_providers_history_history_id_seq OWNED BY public.user_providers_history.history_id;


--
-- Name: user_secrets; Type: TABLE; Schema: public; Owner: appuser
--

CREATE TABLE public.user_secrets (
    "Id" uuid NOT NULL,
    "PasswordHash" text,
    "Email_Enc" bytea,
    "Email_Hash" text,
    "Mobile_Enc" bytea,
    "Mobile_Hash" text,
    "SecurityStamp" uuid DEFAULT public.uuid_generate_v4()
);


ALTER TABLE public.user_secrets OWNER TO appuser;

--
-- Name: user_secrets_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_secrets_history (
    "Id" uuid CONSTRAINT "user_secrets_Id_not_null" NOT NULL,
    "PasswordHash" text,
    "Email_Enc" bytea,
    "Email_Hash" text,
    "Mobile_Enc" bytea,
    "Mobile_Hash" text,
    "SecurityStamp" uuid,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.user_secrets_history OWNER TO postgres;

--
-- Name: user_secrets_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_secrets_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_secrets_history_history_id_seq OWNER TO postgres;

--
-- Name: user_secrets_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_secrets_history_history_id_seq OWNED BY public.user_secrets_history.history_id;


--
-- Name: user_settings; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_settings (
    user_id uuid NOT NULL,
    two_factor_enabled boolean DEFAULT false,
    email_verified boolean DEFAULT false,
    email_verified_at timestamp without time zone,
    email_notifications boolean DEFAULT true,
    created_at timestamp without time zone DEFAULT now(),
    updated_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.user_settings OWNER TO postgres;

--
-- Name: user_settings_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_settings_history (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    two_factor_enabled boolean,
    email_verified boolean,
    email_verified_at timestamp without time zone,
    email_notifications boolean,
    changed_at timestamp without time zone DEFAULT now(),
    changed_by text
);


ALTER TABLE public.user_settings_history OWNER TO postgres;

--
-- Name: TABLE user_settings_history; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public.user_settings_history IS 'Audit trail for user_settings changes';


--
-- Name: users; Type: TABLE; Schema: public; Owner: appuser
--

CREATE TABLE public.users (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "Username" text,
    "DisplayName" text,
    "ProfilePictureUrl" text,
    "DefaultOrgId" uuid,
    "IsActive" boolean DEFAULT true,
    "IsSuspended" boolean DEFAULT false,
    "CreatedAt" timestamp without time zone DEFAULT now(),
    "LastLoginAt" timestamp without time zone,
    first_name text,
    last_name text,
    CONSTRAINT first_name_len CHECK ((length(first_name) < 300)),
    CONSTRAINT last_name_len CHECK ((length(last_name) < 300))
);


ALTER TABLE public.users OWNER TO appuser;

--
-- Name: users_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users_history (
    "Id" uuid CONSTRAINT "users_Id_not_null" NOT NULL,
    "Username" text,
    "DisplayName" text,
    "ProfilePictureUrl" text,
    "DefaultOrgId" uuid,
    "IsActive" boolean,
    "IsSuspended" boolean,
    "CreatedAt" timestamp without time zone,
    "LastLoginAt" timestamp without time zone,
    first_name text,
    last_name text,
    history_id integer NOT NULL,
    history_event text,
    history_created_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.users_history OWNER TO postgres;

--
-- Name: users_history_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.users_history_history_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.users_history_history_id_seq OWNER TO postgres;

--
-- Name: users_history_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.users_history_history_id_seq OWNED BY public.users_history.history_id;


--
-- Name: clients_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.clients_history ALTER COLUMN history_id SET DEFAULT nextval('public.clients_history_history_id_seq'::regclass);


--
-- Name: keystore_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.keystore_history ALTER COLUMN history_id SET DEFAULT nextval('public.keystore_history_history_id_seq'::regclass);


--
-- Name: org_roles Id; Type: DEFAULT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.org_roles ALTER COLUMN "Id" SET DEFAULT nextval('public."org_roles_Id_seq"'::regclass);


--
-- Name: org_roles_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.org_roles_history ALTER COLUMN history_id SET DEFAULT nextval('public.org_roles_history_history_id_seq'::regclass);


--
-- Name: org_users_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.org_users_history ALTER COLUMN history_id SET DEFAULT nextval('public.org_users_history_history_id_seq'::regclass);


--
-- Name: orgs_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.orgs_history ALTER COLUMN history_id SET DEFAULT nextval('public.orgs_history_history_id_seq'::regclass);


--
-- Name: user_addresses_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_addresses_history ALTER COLUMN history_id SET DEFAULT nextval('public.user_addresses_history_history_id_seq'::regclass);


--
-- Name: user_providers Id; Type: DEFAULT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.user_providers ALTER COLUMN "Id" SET DEFAULT nextval('public."user_providers_Id_seq"'::regclass);


--
-- Name: user_providers_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_providers_history ALTER COLUMN history_id SET DEFAULT nextval('public.user_providers_history_history_id_seq'::regclass);


--
-- Name: user_secrets_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_secrets_history ALTER COLUMN history_id SET DEFAULT nextval('public.user_secrets_history_history_id_seq'::regclass);


--
-- Name: users_history history_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users_history ALTER COLUMN history_id SET DEFAULT nextval('public.users_history_history_id_seq'::regclass);


--
-- Name: keystore _System_KeyStore_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.keystore
    ADD CONSTRAINT "_System_KeyStore_pkey" PRIMARY KEY ("KeyName");


--
-- Name: clients_history clients_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.clients_history
    ADD CONSTRAINT clients_history_pkey PRIMARY KEY (history_id);


--
-- Name: clients clients_pkey; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.clients
    ADD CONSTRAINT clients_pkey PRIMARY KEY (client_id);


--
-- Name: keystore_history keystore_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.keystore_history
    ADD CONSTRAINT keystore_history_pkey PRIMARY KEY (history_id);


--
-- Name: org_roles org_roles_RoleName_key; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.org_roles
    ADD CONSTRAINT "org_roles_RoleName_key" UNIQUE ("RoleName");


--
-- Name: org_roles_history org_roles_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.org_roles_history
    ADD CONSTRAINT org_roles_history_pkey PRIMARY KEY (history_id);


--
-- Name: org_roles org_roles_pkey; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.org_roles
    ADD CONSTRAINT org_roles_pkey PRIMARY KEY ("Id");


--
-- Name: org_users_history org_users_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.org_users_history
    ADD CONSTRAINT org_users_history_pkey PRIMARY KEY (history_id);


--
-- Name: org_users org_users_pkey; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.org_users
    ADD CONSTRAINT org_users_pkey PRIMARY KEY ("UserId", "OrgId");


--
-- Name: orgs orgs_Slug_key; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.orgs
    ADD CONSTRAINT "orgs_Slug_key" UNIQUE ("Slug");


--
-- Name: orgs_history orgs_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.orgs_history
    ADD CONSTRAINT orgs_history_pkey PRIMARY KEY (history_id);


--
-- Name: orgs orgs_pkey; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.orgs
    ADD CONSTRAINT orgs_pkey PRIMARY KEY ("Id");


--
-- Name: user_addresses_history user_addresses_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_addresses_history
    ADD CONSTRAINT user_addresses_history_pkey PRIMARY KEY (history_id);


--
-- Name: user_addresses user_addresses_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_addresses
    ADD CONSTRAINT user_addresses_pkey PRIMARY KEY (id);


--
-- Name: user_providers user_providers_Provider_ProviderSubjectId_key; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.user_providers
    ADD CONSTRAINT "user_providers_Provider_ProviderSubjectId_key" UNIQUE ("Provider", "ProviderSubjectId");


--
-- Name: user_providers_history user_providers_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_providers_history
    ADD CONSTRAINT user_providers_history_pkey PRIMARY KEY (history_id);


--
-- Name: user_providers user_providers_pkey; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.user_providers
    ADD CONSTRAINT user_providers_pkey PRIMARY KEY ("Id");


--
-- Name: user_secrets user_secrets_Email_Hash_key; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.user_secrets
    ADD CONSTRAINT "user_secrets_Email_Hash_key" UNIQUE ("Email_Hash");


--
-- Name: user_secrets user_secrets_Mobile_Hash_key; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.user_secrets
    ADD CONSTRAINT "user_secrets_Mobile_Hash_key" UNIQUE ("Mobile_Hash");


--
-- Name: user_secrets_history user_secrets_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_secrets_history
    ADD CONSTRAINT user_secrets_history_pkey PRIMARY KEY (history_id);


--
-- Name: user_secrets user_secrets_pkey; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.user_secrets
    ADD CONSTRAINT user_secrets_pkey PRIMARY KEY ("Id");


--
-- Name: user_settings_history user_settings_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_settings_history
    ADD CONSTRAINT user_settings_history_pkey PRIMARY KEY (id);


--
-- Name: user_settings user_settings_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_settings
    ADD CONSTRAINT user_settings_pkey PRIMARY KEY (user_id);


--
-- Name: users users_Username_key; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "users_Username_key" UNIQUE ("Username");


--
-- Name: users_history users_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users_history
    ADD CONSTRAINT users_history_pkey PRIMARY KEY (history_id);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY ("Id");


--
-- Name: IX_OrgMembers_UserId; Type: INDEX; Schema: public; Owner: appuser
--

CREATE INDEX "IX_OrgMembers_UserId" ON public.org_users USING btree ("UserId");


--
-- Name: IX_Organizations_Slug; Type: INDEX; Schema: public; Owner: appuser
--

CREATE INDEX "IX_Organizations_Slug" ON public.orgs USING btree ("Slug");


--
-- Name: IX_UserSecrets_EmailHash; Type: INDEX; Schema: public; Owner: appuser
--

CREATE INDEX "IX_UserSecrets_EmailHash" ON public.user_secrets USING btree ("Email_Hash");


--
-- Name: IX_UserSecrets_MobileHash; Type: INDEX; Schema: public; Owner: appuser
--

CREATE INDEX "IX_UserSecrets_MobileHash" ON public.user_secrets USING btree ("Mobile_Hash");


--
-- Name: idx_user_settings_history_user; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_user_settings_history_user ON public.user_settings_history USING btree (user_id);


--
-- Name: clients trg_clients_audit; Type: TRIGGER; Schema: public; Owner: appuser
--

CREATE TRIGGER trg_clients_audit AFTER DELETE OR UPDATE ON public.clients FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: keystore trg_keystore_audit; Type: TRIGGER; Schema: public; Owner: postgres
--

CREATE TRIGGER trg_keystore_audit AFTER DELETE OR UPDATE ON public.keystore FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: org_roles trg_org_roles_audit; Type: TRIGGER; Schema: public; Owner: appuser
--

CREATE TRIGGER trg_org_roles_audit AFTER DELETE OR UPDATE ON public.org_roles FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: org_users trg_org_users_audit; Type: TRIGGER; Schema: public; Owner: appuser
--

CREATE TRIGGER trg_org_users_audit AFTER DELETE OR UPDATE ON public.org_users FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: orgs trg_orgs_audit; Type: TRIGGER; Schema: public; Owner: appuser
--

CREATE TRIGGER trg_orgs_audit AFTER DELETE OR UPDATE ON public.orgs FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: user_addresses trg_user_addresses_audit; Type: TRIGGER; Schema: public; Owner: postgres
--

CREATE TRIGGER trg_user_addresses_audit AFTER DELETE OR UPDATE ON public.user_addresses FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: user_providers trg_user_providers_audit; Type: TRIGGER; Schema: public; Owner: appuser
--

CREATE TRIGGER trg_user_providers_audit AFTER DELETE OR UPDATE ON public.user_providers FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: user_secrets trg_user_secrets_audit; Type: TRIGGER; Schema: public; Owner: appuser
--

CREATE TRIGGER trg_user_secrets_audit AFTER DELETE OR UPDATE ON public.user_secrets FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: users trg_users_audit; Type: TRIGGER; Schema: public; Owner: appuser
--

CREATE TRIGGER trg_users_audit AFTER DELETE OR UPDATE ON public.users FOR EACH ROW EXECUTE FUNCTION public.trg_fn_log_history();


--
-- Name: org_users org_users_OrgId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.org_users
    ADD CONSTRAINT "org_users_OrgId_fkey" FOREIGN KEY ("OrgId") REFERENCES public.orgs("Id") ON DELETE CASCADE;


--
-- Name: org_users org_users_RoleId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.org_users
    ADD CONSTRAINT "org_users_RoleId_fkey" FOREIGN KEY ("RoleId") REFERENCES public.org_roles("Id");


--
-- Name: org_users org_users_UserId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.org_users
    ADD CONSTRAINT "org_users_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: user_providers user_providers_UserId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.user_providers
    ADD CONSTRAINT "user_providers_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: user_secrets user_secrets_Id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.user_secrets
    ADD CONSTRAINT "user_secrets_Id_fkey" FOREIGN KEY ("Id") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: user_settings user_settings_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_settings
    ADD CONSTRAINT user_settings_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: users users_DefaultOrgId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: appuser
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "users_DefaultOrgId_fkey" FOREIGN KEY ("DefaultOrgId") REFERENCES public.orgs("Id") ON DELETE SET NULL;


--
-- PostgreSQL database dump complete
--

\unrestrict K6cE9AUCwtXZTP86ccyFKJAf2nItkCmFzZ7xcKjCZbpIfjmL5Z02Vb0H9ATyidt

