using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Compliance;

namespace Compliance.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KYCController : ControllerBase
{
    private readonly ComplianceDbContext _db;
    private readonly ILogger<KYCController> _logger;
    private readonly IConfiguration _config;

    public KYCController(ComplianceDbContext db, ILogger<KYCController> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _config = config;
    }

    [HttpPost]
    public async Task<ActionResult<KYCProfile>> CreateKYCProfile(KYCProfile profile)
    {
        profile.Id = Guid.NewGuid();
        profile.CreatedAt = DateTime.UtcNow;
        profile.Status = KYCStatus.Pending;
        
        // Automated risk scoring
        profile.RiskScore = CalculateRiskScore(profile);
        profile.RiskLevel = DetermineRiskLevel(profile.RiskScore);
        
        // Auto-approval logic
        var autoApprovalThreshold = _config.GetValue<double>("Compliance:KYC:AutoApprovalThreshold");
        if (profile.RiskScore >= autoApprovalThreshold)
        {
            profile.Status = KYCStatus.Approved;
            profile.ApprovedAt = DateTime.UtcNow;
            profile.ApprovedBy = "System";
        }
        
        _db.KYCProfiles.Add(profile);
        await _db.SaveChangesAsync();
        
        // Audit log
        _db.KYCAuditLogs.Add(new KYCAuditLog
        {
            KYCProfileId = profile.Id,
            Action = "Created",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"KYC profile created with risk score {profile.RiskScore}"
        });
        await _db.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetKYCProfile), new { id = profile.Id }, profile);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<KYCProfile>> GetKYCProfile(Guid id)
    {
        var profile = await _db.KYCProfiles.FindAsync(id);
        if (profile == null) return NotFound();
        return profile;
    }

    [HttpPut("{id}/approve")]
    public async Task<IActionResult> ApproveKYC(Guid id)
    {
        var profile = await _db.KYCProfiles.FindAsync(id);
        if (profile == null) return NotFound();
        
        profile.Status = KYCStatus.Approved;
        profile.ApprovedAt = DateTime.UtcNow;
        profile.ApprovedBy = User.Identity?.Name ?? "system";
        profile.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        // Audit log
        _db.KYCAuditLogs.Add(new KYCAuditLog
        {
            KYCProfileId = profile.Id,
            Action = "Approved",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = "KYC profile manually approved"
        });
        await _db.SaveChangesAsync();
        
        return NoContent();
    }

    [HttpPut("{id}/reject")]
    public async Task<IActionResult> RejectKYC(Guid id, [FromBody] string reason)
    {
        var profile = await _db.KYCProfiles.FindAsync(id);
        if (profile == null) return NotFound();
        
        profile.Status = KYCStatus.Rejected;
        profile.RejectionReason = reason;
        profile.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        // Audit log
        _db.KYCAuditLogs.Add(new KYCAuditLog
        {
            KYCProfileId = profile.Id,
            Action = "Rejected",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"KYC profile rejected: {reason}"
        });
        await _db.SaveChangesAsync();
        
        return NoContent();
    }

    private double CalculateRiskScore(KYCProfile profile)
    {
        // Placeholder for sophisticated risk scoring algorithm
        // TODO: Integrate with external KYC providers and risk assessment services
        var score = 0.5; // Base score
        
        // Simple risk factors (placeholder)
        if (string.IsNullOrEmpty(profile.Email)) score += 0.1;
        if (string.IsNullOrEmpty(profile.Phone)) score += 0.1;
        if (profile.FullName.Length < 5) score += 0.2;
        
        return Math.Min(score, 1.0);
    }

    private RiskLevel DetermineRiskLevel(double riskScore)
    {
        return riskScore switch
        {
            < 0.3 => RiskLevel.Low,
            < 0.6 => RiskLevel.Medium,
            < 0.8 => RiskLevel.High,
            _ => RiskLevel.Critical
        };
    }
} 