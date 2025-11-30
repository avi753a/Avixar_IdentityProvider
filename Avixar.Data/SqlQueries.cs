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

    public const string LoginLocal = @"
        SELECT u.""Id"", u.""DisplayName"", s.""PasswordHash"", 
               pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key')) as Email
        FROM ""user_secrets"" s
        JOIN ""users"" u ON u.""Id"" = s.""Id""
        WHERE s.""Email_Hash"" = encode(hmac(@email, current_setting('app.blind_key'), 'sha256'), 'hex')
        AND s.""PasswordHash"" IS NOT NULL;";

    public const string UpdateUser = @"
        UPDATE ""users"" 
        SET ""first_name"" = @FirstName, 
            ""last_name"" = @LastName, 
            ""Displayname"" = @DisplayName,
            ""ProfilePictureUrl"" = @ProfilePictureUrl
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
}
