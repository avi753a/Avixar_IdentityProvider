namespace Avixar.Data;

public static class SqlQueries
{
    // User Queries
    public const string GetUser = @"
        SELECT u.""Id"", u.""DisplayName"", u.""ProfilePictureUrl"", u.""first_name"", u.""last_name"",
               pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key')) as Email
        FROM ""users"" u
        JOIN ""user_secrets"" s ON u.""Id"" = s.""Id""
        WHERE u.""Id"" = @uid;";

    public const string GetUserByEmail = @"
        SELECT u.""Id"", u.""DisplayName"", u.""ProfilePictureUrl"", u.""first_name"", u.""last_name"",
               s.""PasswordHash"",
               pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key')) as Email
        FROM ""users"" u
        JOIN ""user_secrets"" s ON u.""Id"" = s.""Id""
        WHERE s.""Email_Hash"" = encode(hmac(@email, current_setting('app.blind_key'), 'sha256'), 'hex');";

    public const string LoginLocal = @"
        SELECT u.""Id"", u.""DisplayName"", s.""PasswordHash"", 
               pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key')) as Email,
               u.""ProfilePictureUrl""
        FROM ""user_secrets"" s
        JOIN ""users"" u ON u.""Id"" = s.""Id""
        WHERE s.""Email_Hash"" = encode(hmac(@email, current_setting('app.blind_key'), 'sha256'), 'hex')
        AND s.""PasswordHash"" IS NOT NULL;";

    public const string UpdateUser = @"
        UPDATE ""users"" 
        SET ""first_name"" = @FirstName, 
            ""last_name"" = @LastName, 
            ""DisplayName"" = @DisplayName,
            ""ProfilePictureUrl"" = @ProfilePictureUrl
        WHERE ""Id"" = @Id";

    public const string UpdateUserPassword = @"
        UPDATE ""user_secrets"" 
        SET ""PasswordHash"" = @PasswordHash
        WHERE ""Id"" = @Id";

    // Address Queries
    public const string GetUserAddresses = @"
        SELECT * FROM ""user_addresses"" WHERE ""user_id"" = @UserId";

    public const string AddUserAddress = @"
        INSERT INTO ""user_addresses"" 
        (""id"", ""user_id"", ""label"", ""address_line_1"", ""address_line_2"", ""city"", ""postal_code"", ""created_at"")
        VALUES 
        (@Id, @UserId, @Label, @AddressLine1, @AddressLine2, @City, @PostalCode, @CreatedAt)";

    public const string UpdateUserAddress = @"
        UPDATE ""user_addresses"" 
        SET ""label"" = @Label, 
            ""address_line_1"" = @AddressLine1, 
            ""address_line_2"" = @AddressLine2, 
            ""city"" = @City, 
            ""postal_code"" = @PostalCode
        WHERE ""id"" = @Id AND ""user_id"" = @UserId";

    public const string DeleteUserAddress = @"
        DELETE FROM ""user_addresses"" WHERE ""id"" = @Id AND ""user_id"" = @UserId";

    // Client Queries
    public const string GetClient = @"
        SELECT client_id, client_name, client_secret, allowed_redirect_uris, allowed_logout_uris 
        FROM clients WHERE client_id = @Id";

    // User Settings Queries
    public const string GetUserSettings = @"
        SELECT ""user_id"", ""two_factor_enabled"", ""email_verified"", ""email_verified_at"", 
               ""email_notifications"", ""created_at"", ""updated_at""
        FROM ""user_settings"" 
        WHERE ""user_id"" = @UserId";

    public const string UpsertUserSettings = @"
        INSERT INTO ""user_settings"" 
        (""user_id"", ""two_factor_enabled"", ""email_verified"", ""email_verified_at"", ""email_notifications"")
        VALUES (@UserId, @TwoFactorEnabled, @EmailVerified, @EmailVerifiedAt, @EmailNotifications)
        ON CONFLICT (""user_id"") 
        DO UPDATE SET 
            ""two_factor_enabled"" = @TwoFactorEnabled,
            ""email_verified"" = @EmailVerified,
            ""email_verified_at"" = @EmailVerifiedAt,
            ""email_notifications"" = @EmailNotifications,
            ""updated_at"" = NOW()";

    public const string UpdateEmailVerified = @"
        UPDATE ""user_settings"" 
        SET ""email_verified"" = TRUE, 
            ""email_verified_at"" = NOW(),
            ""updated_at"" = NOW()
        WHERE ""user_id"" = @UserId";
}
