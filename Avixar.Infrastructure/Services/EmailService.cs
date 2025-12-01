using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Avixar.Infrastructure.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                _logger.LogInformation("Sending email to {Email}", toEmail);

                var email = new MimeMessage();

                // 1. Setup Sender
                email.From.Add(new MailboxAddress(
                    _config["EmailSettings:SenderName"] ?? "Metropolis",
                    _config["EmailSettings:SenderEmail"]));

                // 2. Setup Receiver
                email.To.Add(MailboxAddress.Parse(toEmail));

                // 3. Setup Content
                email.Subject = subject;
                email.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

                // 4. Send using MailKit
                using (var client = new SmtpClient())
                {
                    // Connect to Gmail using StartTLS on port 587
                    await client.ConnectAsync(
                        _config["EmailSettings:SmtpServer"],
                        int.Parse(_config["EmailSettings:SmtpPort"] ?? "587"),
                        SecureSocketOptions.StartTls);

                    // Authenticate with App Password
                    await client.AuthenticateAsync(
                        _config["EmailSettings:SenderEmail"],
                        _config["EmailSettings:SenderPassword"]);

                    await client.SendAsync(email);
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw;
            }
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            var subject = "Verify Your Email - Metropolis";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px;'>
                        <h2 style='color: #2563EB;'>Welcome to Metropolis!</h2>
                        <p>Thank you for registering. Please verify your email address by clicking the button below:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{verificationLink}' 
                               style='background-color: #2563EB; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Verify Email Address
                            </a>
                        </div>
                        <p style='color: #666; font-size: 12px;'>If you didn't create an account, you can safely ignore this email.</p>
                        <p style='color: #666; font-size: 12px;'>This link will expire in 24 hours.</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendOtpEmailAsync(string toEmail, string otp, string purpose)
        {
            var subject = $"{purpose} - Verification Code";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px;'>
                        <h2 style='color: #2563EB;'>{purpose}</h2>
                        <p>Your verification code is:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <div style='background-color: #f0f0f0; padding: 20px; border-radius: 5px; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #2563EB;'>
                                {otp}
                            </div>
                        </div>
                        <p style='color: #666;'>This code will expire in 5 minutes.</p>
                        <p style='color: #666; font-size: 12px;'>If you didn't request this code, please ignore this email.</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}
