using System;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using HMS.API.Domain.Common;
using HMS.API.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Security;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SubscriptionsController : ControllerBase
    {
        private readonly AuthDbContext _authDb;
        private readonly ITenantSubscriptionService _subsService;
        private readonly IBillingWebhookService _webhookService;
        private readonly IConfiguration _config;

        public SubscriptionsController(AuthDbContext authDb, ITenantSubscriptionService subsService, IBillingWebhookService webhookService, IConfiguration config)
        {
            _authDb = authDb;
            _subsService = subsService;
            _webhookService = webhookService;
            _config = config;
        }

        [HttpGet("{tenantId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Get(Guid tenantId)
        {
            var s = await _authDb.Set<TenantSubscription>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted);
            if (s == null) return NotFound();
            return Ok(s);
        }

        [HttpPost("{tenantId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Create(Guid tenantId, [FromBody] CreateSubscriptionRequest req)
        {
            if (await _authDb.Set<TenantSubscription>().AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted)) return BadRequest(new { error = "Subscription already exists" });
            var sub = new TenantSubscription { TenantId = tenantId, Plan = req.Plan, Status = SubscriptionStatus.Active, StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddMonths(req.Months ?? 1) };
            _authDb.Set<TenantSubscription>().Add(sub);
            await _authDb.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { tenantId = tenantId }, sub);
        }

        [HttpPut("{tenantId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Update(Guid tenantId, [FromBody] UpdateSubscriptionRequest req)
        {
            var sub = await _authDb.Set<TenantSubscription>().SingleOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted);
            if (sub == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(req.Plan)) sub.Plan = req.Plan;
            if (req.Status.HasValue) sub.Status = req.Status.Value;
            if (req.EndAt.HasValue) sub.EndAt = req.EndAt;
            await _authDb.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{tenantId}/cancel")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Cancel(Guid tenantId)
        {
            var sub = await _authDb.Set<TenantSubscription>().SingleOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted);
            if (sub == null) return NotFound();
            sub.Status = SubscriptionStatus.Cancelled;
            sub.EndAt = DateTimeOffset.UtcNow;
            await _authDb.SaveChangesAsync();
            return NoContent();
        }

        // Simulated webhook receiver for billing provider events (in-memory)
        [HttpPost("webhook")]
        public IActionResult Webhook([FromBody] BillingWebhookDto dto)
        {
            if (dto == null) return BadRequest();

            // verify HMAC signature computed over the serialized JSON body
            try
            {
                var sigHeader = Request.Headers["Billing-Signature"].ToString();
                if (string.IsNullOrEmpty(sigHeader)) return Unauthorized();

                var secretBase64 = _config["Billing:WebhookSecret"];
                if (string.IsNullOrEmpty(secretBase64)) return StatusCode(500);

                byte[] secret;
                try
                {
                    secret = Convert.FromBase64String(secretBase64);
                }
                catch
                {
                    return StatusCode(500);
                }

                var serialized = JsonSerializer.Serialize(dto);
                using var hmac = new HMACSHA256(secret);
                var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(serialized));
                var expected = Convert.ToBase64String(expectedBytes);

                // compare in fixed time
                var provided = sigHeader;
                if (string.IsNullOrEmpty(provided)) return Unauthorized();

                var providedBytes = Convert.FromBase64String(provided);
                if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes)) return Unauthorized();
            }
            catch
            {
                return StatusCode(500);
            }

            var ev = new BillingWebhookEvent(dto.EventType, dto.Payload, DateTimeOffset.UtcNow);
            _webhookService.AddEvent(ev);

            try
            {
                var doc = JsonDocument.Parse(dto.Payload);
                if (doc.RootElement.TryGetProperty("tenantId", out var t) && Guid.TryParse(t.GetString(), out var tid))
                {
                    if (doc.RootElement.TryGetProperty("status", out var st))
                    {
                        if (Enum.TryParse<SubscriptionStatus>(st.GetString(), true, out var status))
                        {
                            var sub = _authDb.Set<TenantSubscription>().SingleOrDefault(s => s.TenantId == tid && !s.IsDeleted);
                            if (sub != null)
                            {
                                sub.Status = status;
                                if (doc.RootElement.TryGetProperty("endAt", out var endAt) && DateTimeOffset.TryParse(endAt.GetString(), out var ea)) sub.EndAt = ea;
                                _authDb.SaveChanges();
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore payload parse errors
            }

            return Accepted();
        }
    }

    public class CreateSubscriptionRequest
    {
        public string Plan { get; set; } = "basic";
        public int? Months { get; set; } = 1;
    }

    public class UpdateSubscriptionRequest
    {
        public string? Plan { get; set; }
        public SubscriptionStatus? Status { get; set; }
        public DateTimeOffset? EndAt { get; set; }
    }

    public class BillingWebhookDto
    {
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty; // JSON payload
    }
}
