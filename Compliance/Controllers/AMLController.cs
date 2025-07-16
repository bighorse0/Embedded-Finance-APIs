using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Compliance;

namespace Compliance.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AMLController : ControllerBase
{
    private readonly ComplianceDbContext _db;
    private readonly ILogger<AMLController> _logger;
    private readonly IConfiguration _config;

    public AMLController(ComplianceDbContext db, ILogger<AMLController> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _config = config;
    }

    [HttpPost("monitor")]
    public async Task<IActionResult> MonitorTransaction([FromBody] Transaction transaction)
    {
        var alert = new AMLAlert
        {
            TransactionId = transaction.Id,
            AlertType = "Transaction Monitoring",
            RiskScore = CalculateTransactionRiskScore(transaction),
            RiskLevel = DetermineRiskLevel(transaction.Amount),
            CreatedAt = DateTime.UtcNow
        };

        var threshold = _config.GetValue<decimal>("Compliance:AML:SuspiciousActivityThreshold");
        
        if (transaction.Amount >= threshold || alert.RiskScore > 0.7)
        {
            _db.AMLAlerts.Add(alert);
            await _db.SaveChangesAsync();
            
            // Audit log
            _db.AMLAlertAuditLogs.Add(new AMLAlertAuditLog
            {
                AMLAlertId = alert.Id,
                Action = "Alert Created",
                PerformedBy = "System",
                Timestamp = DateTime.UtcNow,
                Details = $"AML alert created for transaction {transaction.Id} with risk score {alert.RiskScore}"
            });
            await _db.SaveChangesAsync();
            
            return Ok(new { AlertId = alert.Id, RiskScore = alert.RiskScore });
        }
        
        return Ok(new { Message = "Transaction passed monitoring" });
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<IEnumerable<AMLAlert>>> GetAlerts()
    {
        return await _db.AMLAlerts.AsNoTracking().ToListAsync();
    }

    [HttpGet("alerts/{id}")]
    public async Task<ActionResult<AMLAlert>> GetAlert(Guid id)
    {
        var alert = await _db.AMLAlerts.FindAsync(id);
        if (alert == null) return NotFound();
        return alert;
    }

    [HttpPut("alerts/{id}/resolve")]
    public async Task<IActionResult> ResolveAlert(Guid id, [FromBody] string resolutionNotes)
    {
        var alert = await _db.AMLAlerts.FindAsync(id);
        if (alert == null) return NotFound();
        
        alert.IsResolved = true;
        alert.ResolutionNotes = resolutionNotes;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolvedBy = User.Identity?.Name ?? "system";
        alert.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        // Audit log
        _db.AMLAlertAuditLogs.Add(new AMLAlertAuditLog
        {
            AMLAlertId = alert.Id,
            Action = "Resolved",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"Alert resolved: {resolutionNotes}"
        });
        await _db.SaveChangesAsync();
        
        return NoContent();
    }

    [HttpPost("reports/ctr")]
    public async Task<IActionResult> GenerateCTR([FromBody] DateTime reportDate)
    {
        var ctrThreshold = _config.GetValue<decimal>("Compliance:Regulatory:CTRThreshold");
        
        // TODO: Query transactions above CTR threshold for the date
        var report = new RegulatoryReport
        {
            Type = ReportType.CTR,
            ReportId = $"CTR-{reportDate:yyyyMMdd}-{Guid.NewGuid():N}",
            ReportDate = reportDate,
            Status = "Draft",
            Content = $"CTR Report for {reportDate:yyyy-MM-dd}",
            CreatedAt = DateTime.UtcNow
        };
        
        _db.RegulatoryReports.Add(report);
        await _db.SaveChangesAsync();
        
        // Audit log
        _db.RegulatoryReportAuditLogs.Add(new RegulatoryReportAuditLog
        {
            RegulatoryReportId = report.Id,
            Action = "Generated",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"CTR report generated for {reportDate:yyyy-MM-dd}"
        });
        await _db.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetReport), new { id = report.Id }, report);
    }

    [HttpGet("reports/{id}")]
    public async Task<ActionResult<RegulatoryReport>> GetReport(Guid id)
    {
        var report = await _db.RegulatoryReports.FindAsync(id);
        if (report == null) return NotFound();
        return report;
    }

    private double CalculateTransactionRiskScore(Transaction transaction)
    {
        // Placeholder for sophisticated AML risk scoring
        // TODO: Integrate with external AML monitoring services
        var score = 0.3; // Base score
        
        // Simple risk factors (placeholder)
        if (transaction.Amount > 10000) score += 0.3;
        if (transaction.Type == TransactionType.Wire) score += 0.2;
        if (transaction.Type == TransactionType.FX) score += 0.2;
        
        return Math.Min(score, 1.0);
    }

    private RiskLevel DetermineRiskLevel(decimal amount)
    {
        return amount switch
        {
            < 1000 => RiskLevel.Low,
            < 10000 => RiskLevel.Medium,
            < 50000 => RiskLevel.High,
            _ => RiskLevel.Critical
        };
    }
} 