using StackExchange.Redis;

namespace FraudDetection.Services;

public interface IFeatureEngineeringService
{
    Task<FraudFeatureSet> ExtractFeaturesAsync(Transaction transaction);
    Task<Dictionary<string, double>> CalculateUserBehaviorFeaturesAsync(string userId);
    Task<Dictionary<string, double>> CalculateVelocityFeaturesAsync(string userId);
    Task<Dictionary<string, double>> CalculateNetworkFeaturesAsync(string userId);
}

public class FeatureEngineeringService : IFeatureEngineeringService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<FeatureEngineeringService> _logger;

    public FeatureEngineeringService(IConnectionMultiplexer redis, ILogger<FeatureEngineeringService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<FraudFeatureSet> ExtractFeaturesAsync(Transaction transaction)
    {
        var features = new FraudFeatureSet
        {
            TransactionId = transaction.Id,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            TransactionType = transaction.Type.ToString(),
            ScoredAt = DateTime.UtcNow
        };

        // Extract time-based features
        var transactionTime = transaction.CreatedAt;
        features.HourOfDay = transactionTime.Hour;
        features.DayOfWeek = (int)transactionTime.DayOfWeek;
        features.DayOfMonth = transactionTime.Day;
        features.Month = transactionTime.Month;
        features.IsWeekend = transactionTime.DayOfWeek == DayOfWeek.Saturday || transactionTime.DayOfWeek == DayOfWeek.Sunday;
        features.IsHoliday = IsHoliday(transactionTime);

        // Extract user behavior features
        var userFeatures = await CalculateUserBehaviorFeaturesAsync(transaction.SourceAccountId.ToString());
        features.UserTransactionCount24h = (int)userFeatures.GetValueOrDefault("TransactionCount24h", 0);
        features.UserTransactionCount7d = (int)userFeatures.GetValueOrDefault("TransactionCount7d", 0);
        features.UserTotalAmount24h = (decimal)userFeatures.GetValueOrDefault("TotalAmount24h", 0);
        features.UserTotalAmount7d = (decimal)userFeatures.GetValueOrDefault("TotalAmount7d", 0);
        features.UserAverageAmount = userFeatures.GetValueOrDefault("AverageAmount", 0);
        features.UserAmountVariance = userFeatures.GetValueOrDefault("AmountVariance", 0);

        // Extract velocity features
        var velocityFeatures = await CalculateVelocityFeaturesAsync(transaction.SourceAccountId.ToString());
        features.VelocityAmount24h = (int)velocityFeatures.GetValueOrDefault("VelocityAmount24h", 0);
        features.VelocityFrequency24h = (int)velocityFeatures.GetValueOrDefault("VelocityFrequency24h", 0);
        features.VelocityUniqueMerchants24h = (int)velocityFeatures.GetValueOrDefault("VelocityUniqueMerchants24h", 0);
        features.VelocityUniqueCountries24h = (int)velocityFeatures.GetValueOrDefault("VelocityUniqueCountries24h", 0);

        // Extract network features
        var networkFeatures = await CalculateNetworkFeaturesAsync(transaction.SourceAccountId.ToString());
        features.NetworkRiskScore = networkFeatures.GetValueOrDefault("NetworkRiskScore", 0);
        features.NetworkAssociatedFraudCount = (int)networkFeatures.GetValueOrDefault("NetworkAssociatedFraudCount", 0);
        features.NetworkAverageRiskScore = networkFeatures.GetValueOrDefault("NetworkAverageRiskScore", 0);

        // TODO: Extract device and location features from transaction metadata
        features.DeviceFingerprint = "placeholder";
        features.IPAddress = "127.0.0.1";
        features.UserAgent = "placeholder";
        features.Latitude = 0;
        features.Longitude = 0;
        features.LocationCountry = "US";
        features.LocationCity = "Unknown";

        return features;
    }

    public async Task<Dictionary<string, double>> CalculateUserBehaviorFeaturesAsync(string userId)
    {
        var db = _redis.GetDatabase();
        var features = new Dictionary<string, double>();

        try
        {
            // Get transaction history from Redis
            var now = DateTime.UtcNow;
            var yesterday = now.AddDays(-1);
            var weekAgo = now.AddDays(-7);

            // Count transactions in last 24 hours
            var transactions24h = await GetTransactionCountAsync(db, userId, yesterday, now);
            features["TransactionCount24h"] = transactions24h;

            // Count transactions in last 7 days
            var transactions7d = await GetTransactionCountAsync(db, userId, weekAgo, now);
            features["TransactionCount7d"] = transactions7d;

            // Calculate total amounts
            var totalAmount24h = await GetTotalAmountAsync(db, userId, yesterday, now);
            features["TotalAmount24h"] = (double)totalAmount24h;

            var totalAmount7d = await GetTotalAmountAsync(db, userId, weekAgo, now);
            features["TotalAmount7d"] = (double)totalAmount7d;

            // Calculate average and variance
            if (transactions7d > 0)
            {
                features["AverageAmount"] = totalAmount7d / transactions7d;
                features["AmountVariance"] = await CalculateAmountVarianceAsync(db, userId, weekAgo, now);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate user behavior features for {UserId}", userId);
        }

        return features;
    }

    public async Task<Dictionary<string, double>> CalculateVelocityFeaturesAsync(string userId)
    {
        var db = _redis.GetDatabase();
        var features = new Dictionary<string, double>();

        try
        {
            var now = DateTime.UtcNow;
            var yesterday = now.AddDays(-1);

            // Velocity by amount
            features["VelocityAmount24h"] = await GetVelocityByAmountAsync(db, userId, yesterday, now);

            // Velocity by frequency
            features["VelocityFrequency24h"] = await GetVelocityByFrequencyAsync(db, userId, yesterday, now);

            // Velocity by unique merchants
            features["VelocityUniqueMerchants24h"] = await GetUniqueMerchantsAsync(db, userId, yesterday, now);

            // Velocity by unique countries
            features["VelocityUniqueCountries24h"] = await GetUniqueCountriesAsync(db, userId, yesterday, now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate velocity features for {UserId}", userId);
        }

        return features;
    }

    public async Task<Dictionary<string, double>> CalculateNetworkFeaturesAsync(string userId)
    {
        var features = new Dictionary<string, double>();

        try
        {
            // TODO: Implement network analysis
            // This would analyze connections between users, shared devices, IP addresses, etc.
            features["NetworkRiskScore"] = 0.1; // Placeholder
            features["NetworkAssociatedFraudCount"] = 0; // Placeholder
            features["NetworkAverageRiskScore"] = 0.1; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate network features for {UserId}", userId);
        }

        return features;
    }

    private async Task<int> GetTransactionCountAsync(IDatabase db, string userId, DateTime start, DateTime end)
    {
        // TODO: Implement Redis query for transaction count
        return await Task.FromResult(new Random().Next(0, 10));
    }

    private async Task<decimal> GetTotalAmountAsync(IDatabase db, string userId, DateTime start, DateTime end)
    {
        // TODO: Implement Redis query for total amount
        return await Task.FromResult((decimal)(new Random().NextDouble() * 10000));
    }

    private async Task<double> CalculateAmountVarianceAsync(IDatabase db, string userId, DateTime start, DateTime end)
    {
        // TODO: Implement variance calculation
        return await Task.FromResult(new Random().NextDouble() * 1000);
    }

    private async Task<int> GetVelocityByAmountAsync(IDatabase db, string userId, DateTime start, DateTime end)
    {
        // TODO: Implement velocity calculation by amount
        return await Task.FromResult(new Random().Next(0, 5));
    }

    private async Task<int> GetVelocityByFrequencyAsync(IDatabase db, string userId, DateTime start, DateTime end)
    {
        // TODO: Implement velocity calculation by frequency
        return await Task.FromResult(new Random().Next(0, 20));
    }

    private async Task<int> GetUniqueMerchantsAsync(IDatabase db, string userId, DateTime start, DateTime end)
    {
        // TODO: Implement unique merchants calculation
        return await Task.FromResult(new Random().Next(0, 10));
    }

    private async Task<int> GetUniqueCountriesAsync(IDatabase db, string userId, DateTime start, DateTime end)
    {
        // TODO: Implement unique countries calculation
        return await Task.FromResult(new Random().Next(0, 3));
    }

    private bool IsHoliday(DateTime date)
    {
        // TODO: Implement holiday detection
        return false;
    }
} 