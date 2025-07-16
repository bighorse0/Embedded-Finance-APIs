using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.EncryptColumn.Attribute;

namespace SharedKernel;

public enum KYCStatus
{
    Pending,
    Approved,
    Rejected,
    UnderReview,
    Expired
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum ReportType
{
    CTR, // Currency Transaction Report
    SAR, // Suspicious Activity Report
    KYC,
    AML
}

public class KYCProfile : Entity
{
    [Required]
    [EncryptColumn]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    [EncryptColumn]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EncryptColumn]
    public string DateOfBirth { get; set; } = string.Empty;

    [Required]
    [EncryptColumn]
    public string SSN { get; set; } = string.Empty;

    [Required]
    [EncryptColumn]
    public string Address { get; set; } = string.Empty;

    [Required]
    [EncryptColumn]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [EncryptColumn]
    public string Email { get; set; } = string.Empty;

    public KYCStatus Status { get; set; } = KYCStatus.Pending;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public double RiskScore { get; set; } = 0.0;
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? RejectionReason { get; set; }
    public List<KYCAuditLog> AuditLogs { get; set; } = new();
}

public class KYCAuditLog : Entity
{
    public Guid KYCProfileId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}

public class AMLAlert : Entity
{
    [Required]
    public Guid TransactionId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public double RiskScore { get; set; } = 0.0;
    public bool IsResolved { get; set; } = false;
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public List<AMLAlertAuditLog> AuditLogs { get; set; } = new();
}

public class AMLAlertAuditLog : Entity
{
    public Guid AMLAlertId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}

public class RegulatoryReport : Entity
{
    [Required]
    public ReportType Type { get; set; }
    public string ReportId { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Draft";
    public string? Content { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public string? ExternalReference { get; set; }
    public List<RegulatoryReportAuditLog> AuditLogs { get; set; } = new();
}

public class RegulatoryReportAuditLog : Entity
{
    public Guid RegulatoryReportId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
} 