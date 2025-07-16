using Microsoft.ML;
using Microsoft.ML.Data;
using StackExchange.Redis;
using System.Text.Json;

namespace FraudDetection.Services;

public interface IFraudDetectionService
{
    Task<FraudScore> ScoreTransactionAsync(Transaction transaction);
    Task<double> GetCachedScoreAsync(Guid transactionId);
    Task CacheScoreAsync(Guid transactionId, double score);
    Task<FraudAlert> CreateAlertAsync(Transaction transaction, double riskScore);
    Task<List<FraudAlert>> GetActiveAlertsAsync();
}

public class FraudDetectionService : IFraudDetectionService
{
    private readonly MLContext _mlContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly IFeatureEngineeringService _featureService;
    private readonly ILogger<FraudDetectionService> _logger;
    private readonly IConfiguration _config;
    private ITransformer? _model;

    public FraudDetectionService(
        MLContext mlContext,
        IConnectionMultiplexer redis,
        IFeatureEngineeringService featureService,
        ILogger<FraudDetectionService> logger,
        IConfiguration config)
    {
        _mlContext = mlContext;
        _redis = redis;
        _featureService = featureService;
        _logger = logger;
        _config = config;
        LoadModel();
    }

    public async Task<FraudScore> ScoreTransactionAsync(Transaction transaction)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Check cache first
            var cachedScore = await GetCachedScoreAsync(transaction.Id);
            if (cachedScore > 0)
            {
                _logger.LogInformation("Cache hit for transaction {TransactionId}", transaction.Id);
                return new FraudScore
                {
                    TransactionId = transaction.Id,
                    Score = cachedScore,
                    RiskLevel = DetermineRiskLevel(cachedScore),
                    IsFraud = cachedScore > 0.8,
                    ModelVersion = "cached",
                    ScoredAt = DateTime.UtcNow
                };
            }

            // Feature engineering
            var features = await _featureService.ExtractFeaturesAsync(transaction);
            
            // Real-time scoring
            var score = await ScoreWithModelAsync(features);
            
            // Cache result
            await CacheScoreAsync(transaction.Id, score);
            
            stopwatch.Stop();
            _logger.LogInformation("Transaction {TransactionId} scored in {ElapsedMs}ms with score {Score}", 
                transaction.Id, stopwatch.ElapsedMilliseconds, score);

            return new FraudScore
            {
                TransactionId = transaction.Id,
                Score = score,
                RiskLevel = DetermineRiskLevel(score),
                IsFraud = score > 0.8,
                ModelVersion = "v1.0",
                ScoredAt = DateTime.UtcNow,
                Explanation = GenerateExplanation(features, score)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to score transaction {TransactionId}", transaction.Id);
            throw;
        }
    }

    public async Task<double> GetCachedScoreAsync(Guid transactionId)
    {
        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync($"fraud_score:{transactionId}");
        return cached.HasValue ? double.Parse(cached!) : 0;
    }

    public async Task CacheScoreAsync(Guid transactionId, double score)
    {
        var db = _redis.GetDatabase();
        var expiration = TimeSpan.FromMinutes(
            _config.GetValue<int>("FraudDetection:RealTimeScoring:CacheExpirationMinutes"));
        await db.StringSetAsync($"fraud_score:{transactionId}", score.ToString(), expiration);
    }

    public async Task<FraudAlert> CreateAlertAsync(Transaction transaction, double riskScore)
    {
        var alert = new FraudAlert
        {
            TransactionId = transaction.Id,
            AlertType = "High Risk Transaction",
            RiskScore = riskScore,
            RiskLevel = DetermineRiskLevel(riskScore),
            Description = $"Transaction {transaction.Id} flagged with risk score {riskScore:F2}",
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogWarning("Fraud alert created for transaction {TransactionId} with score {Score}", 
            transaction.Id, riskScore);

        return await Task.FromResult(alert);
    }

    public async Task<List<FraudAlert>> GetActiveAlertsAsync()
    {
        // TODO: Implement database query for active alerts
        return await Task.FromResult(new List<FraudAlert>());
    }

    private async Task<double> ScoreWithModelAsync(FraudFeatureSet features)
    {
        if (_model == null)
        {
            _logger.LogWarning("No model loaded, using fallback scoring");
            return await FallbackScoringAsync(features);
        }

        try
        {
            // Create prediction engine
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<FraudFeatureSet, FraudPrediction>(_model);
            
            // Make prediction
            var prediction = predictionEngine.Predict(features);
            
            return prediction.Score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model prediction failed, using fallback");
            return await FallbackScoringAsync(features);
        }
    }

    private async Task<double> FallbackScoringAsync(FraudFeatureSet features)
    {
        // Simple rule-based scoring as fallback
        var score = 0.3; // Base score
        
        // Amount-based risk
        if (features.Amount > 10000) score += 0.2;
        if (features.Amount > 50000) score += 0.3;
        
        // Velocity risk
        if (features.VelocityFrequency24h > 10) score += 0.2;
        if (features.VelocityAmount24h > 50000) score += 0.2;
        
        // Location risk
        if (features.Latitude == 0 && features.Longitude == 0) score += 0.1;
        
        // Time-based risk
        if (features.HourOfDay < 6 || features.HourOfDay > 22) score += 0.1;
        
        return await Task.FromResult(Math.Min(score, 1.0));
    }

    private void LoadModel()
    {
        try
        {
            var modelPath = _config.GetValue<string>("FraudDetection:ML:ModelPath");
            if (File.Exists(modelPath))
            {
                _model = _mlContext.Model.Load(modelPath, out var _);
                _logger.LogInformation("ML model loaded from {ModelPath}", modelPath);
            }
            else
            {
                _logger.LogWarning("Model file not found at {ModelPath}, using fallback scoring", modelPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ML model");
        }
    }

    private string DetermineRiskLevel(double score)
    {
        var highThreshold = _config.GetValue<double>("FraudDetection:RiskScoring:HighRiskThreshold");
        var mediumThreshold = _config.GetValue<double>("FraudDetection:RiskScoring:MediumRiskThreshold");
        
        return score switch
        {
            >= highThreshold => "High",
            >= mediumThreshold => "Medium",
            _ => "Low"
        };
    }

    private string GenerateExplanation(FraudFeatureSet features, double score)
    {
        var factors = new List<string>();
        
        if (features.Amount > 10000) factors.Add("High transaction amount");
        if (features.VelocityFrequency24h > 10) factors.Add("High transaction frequency");
        if (features.HourOfDay < 6 || features.HourOfDay > 22) factors.Add("Unusual transaction time");
        
        return factors.Any() ? $"Risk factors: {string.Join(", ", factors)}" : "No significant risk factors";
    }
}

// ML.NET data models
public class FraudPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
    
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }
    
    [ColumnName("Probability")]
    public float Probability { get; set; }
} 