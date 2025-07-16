using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.EncryptColumn.Attribute;

namespace SharedKernel;

public enum TransactionType
{
    Deposit,
    Withdrawal,
    Transfer,
    ACH,
    Wire,
    Card,
    FX
}

public enum TransactionStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled,
    Reversed,
    UnderReview
}

public class Transaction : Entity
{
    [Required]
    public TransactionType Type { get; set; }

    [Required]
    public Guid SourceAccountId { get; set; }

    public Guid? DestinationAccountId { get; set; }

    [Required]
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public string? Reference { get; set; }

    public string? ExternalId { get; set; } // For ACH/Wire/Card

    public List<TransactionAuditLog> AuditLogs { get; set; } = new();
}

public class TransactionAuditLog : Entity
{
    public Guid TransactionId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
} 