using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.EncryptColumn.Attribute;

namespace SharedKernel;

public enum AccountType
{
    Checking,
    Savings,
    Business,
    Card
}

public class Account : Entity
{
    [Required]
    [EncryptColumn]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    public AccountType Type { get; set; }

    [Required]
    [EncryptColumn]
    public string OwnerName { get; set; } = string.Empty;

    [Required]
    [EncryptColumn]
    public string OwnerId { get; set; } = string.Empty; // KYC/AML link

    [Required]
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    public bool IsActive { get; set; } = true;

    public List<AccountAuditLog> AuditLogs { get; set; } = new();
}

public class AccountAuditLog : Entity
{
    public Guid AccountId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
} 