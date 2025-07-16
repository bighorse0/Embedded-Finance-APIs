using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.EncryptColumn.Attribute;

namespace SharedKernel;

public class FraudFeatureSet : Entity
{
    [Required]
    public Guid TransactionId { get; set; }
    
    // Transaction features
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string TransactionType { get; set; } = string.Empty;
    public string MerchantCategory { get; set; } = string.Empty;
    public string MerchantCountry { get; set; } = string.Empty;
    
    // User behavior features
    public int UserTransactionCount24h { get; set; }
    public int UserTransactionCount7d { get; set; }
    public decimal UserTotalAmount24h { get; set; }
    public decimal UserTotalAmount7d { get; set; }
    public double UserAverageAmount { get; set; }
    public double UserAmountVariance { get; set; }
    
    // Device and location features
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LocationCountry { get; set; } = string.Empty;
    public string LocationCity { get; set; } = string.Empty;
    
    // Time-based features
    public int HourOfDay { get; set; }
    public int DayOfWeek { get; set; }
    public int DayOfMonth { get; set; }
    public int Month { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsHoliday { get; set; }
    
    // Velocity features
    public int VelocityAmount24h { get; set; }
    public int VelocityFrequency24h { get; set; }
    public int VelocityUniqueMerchants24h { get; set; }
    public int VelocityUniqueCountries24h { get; set; }
    
    // Network features
    public double NetworkRiskScore { get; set; }
    public int NetworkAssociatedFraudCount { get; set; }
    public double NetworkAverageRiskScore { get; set; }
    
    // Model features
    public double RiskScore { get; set; }
    public string RiskLevel { get; set; } = "Low";
    public bool IsFraud { get; set; } = false;
    public DateTime ScoredAt { get; set; } = DateTime.UtcNow;
}

public class FraudScore : Entity
{
    [Required]
    public Guid TransactionId { get; set; }
    public double Score { get; set; }
    public string RiskLevel { get; set; } = "Low";
    public bool IsFraud { get; set; } = false;
    public string ModelVersion { get; set; } = string.Empty;
    public DateTime ScoredAt { get; set; } = DateTime.UtcNow;
    public string? Explanation { get; set; }
    public Dictionary<string, double> FeatureImportance { get; set; } = new();
}

public class BehavioralPattern : Entity
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    public string PatternType { get; set; } = string.Empty;
    public string PatternData { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class FraudAlert : Entity
{
    [Required]
    public Guid TransactionId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public string RiskLevel { get; set; } = "Medium";
    public string Description { get; set; } = string.Empty;
    public bool IsResolved { get; set; } = false;
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public List<FraudAlertAuditLog> AuditLogs { get; set; } = new();
}

public class FraudAlertAuditLog : Entity
{
    public Guid FraudAlertId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}

public class MLModel : Entity
{
    [Required]
    public string ModelName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public bool IsActive { get; set; } = false;
    public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
    public string TrainingDataHash { get; set; } = string.Empty;
    public Dictionary<string, double> FeatureImportance { get; set; } = new();
} 