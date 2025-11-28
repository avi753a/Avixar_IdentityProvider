/*
 * SCRIPT: CleanAllUsers.sql
 * PURPOSE: Wipes all user data to start fresh.
 * WARNING: This deletes ALL users and linked providers!
 */
TRUNCATE TABLE "users" CASCADE;
-- Note: "user_providers" and "org_users" will be cleared automatically due to ON DELETE CASCADE.
