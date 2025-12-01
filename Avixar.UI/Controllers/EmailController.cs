using Avixar.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Avixar.UI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(EmailService emailService, ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Send a simple email with subject and body
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Body))
                {
                    return BadRequest(new { message = "To, Subject, and Body are required" });
                }

                _logger.LogInformation("Email send request from {User} to {To}", User.Identity?.Name, request.To);

                await _emailService.SendEmailAsync(request.To, request.Subject, request.Body);
                return Ok(new { message = "Email sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email");
                return StatusCode(500, new { message = "An error occurred while sending email" });
            }
        }
    }

    public class SendEmailRequest
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}
