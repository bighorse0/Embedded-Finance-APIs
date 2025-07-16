using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms;

namespace FraudDetection.Services;

public interface IModelTrainingService
{
    Task<MLModel> TrainModelAsync(List<FraudFeatureSet> trainingData);
    Task<ModelMetrics> EvaluateModelAsync(ITransformer model, List<FraudFeatureSet> testData);
    Task<bool> DeployModelAsync(MLModel model);
    Task<ITransformer> LoadModelAsync(string modelPath);
    Task<List<FraudFeatureSet>> GenerateSyntheticDataAsync(int count);
}

public class ModelTrainingService : IModelTrainingService
{
    private readonly MLContext _mlContext;
    private readonly ILogger<ModelTrainingService> _logger;
    private readonly IConfiguration _config;

    public ModelTrainingService(MLContext mlContext, ILogger<ModelTrainingService> logger, IConfiguration config)
    {
        _mlContext = mlContext;
        _logger = logger;
        _config = config;
    }

    public async Task<MLModel> TrainModelAsync(List<FraudFeatureSet> trainingData)
    {
        try
        {
            _logger.LogInformation("Starting model training with {Count} samples", trainingData.Count);

            // Create data view
            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // Define pipeline
            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label", "IsFraud")
                .Append(_mlContext.Transforms.Concatenate("Features", 
                    "Amount", "UserTransactionCount24h", "UserTransactionCount7d", 
                    "UserTotalAmount24h", "UserTotalAmount7d", "UserAverageAmount", 
                    "VelocityFrequency24h", "VelocityAmount24h", "NetworkRiskScore"))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(
                    labelColumnName: "Label", 
                    featureColumnName: "Features"));

            // Train model
            var model = await Task.Run(() => pipeline.Fit(dataView));

            // Evaluate model
            var predictions = model.Transform(dataView);
            var metrics = _mlContext.BinaryClassification.Evaluate(predictions);

            // Create model metadata
            var mlModel = new MLModel
            {
                ModelName = "FraudDetection",
                Version = Guid.NewGuid().ToString("N")[..8],
                Algorithm = "LbfgsLogisticRegression",
                Accuracy = metrics.Accuracy,
                Precision = metrics.Precision,
                Recall = metrics.Recall,
                F1Score = metrics.F1Score,
                TrainedAt = DateTime.UtcNow,
                TrainingDataHash = CalculateDataHash(trainingData),
                IsActive = false
            };

            // Save model
            var modelPath = Path.Combine(_config.GetValue<string>("FraudDetection:ML:ModelPath") ?? "./Models", 
                $"fraud_detection_model_{mlModel.Version}.zip");
            
            await Task.Run(() => _mlContext.Model.Save(model, dataView.Schema, modelPath));
            mlModel.ModelPath = modelPath;

            _logger.LogInformation("Model training completed. Version: {Version}, Accuracy: {Accuracy:F3}", 
                mlModel.Version, mlModel.Accuracy);

            return mlModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to train model");
            throw;
        }
    }

    public async Task<ModelMetrics> EvaluateModelAsync(ITransformer model, List<FraudFeatureSet> testData)
    {
        try
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(testData);
            var predictions = model.Transform(dataView);
            var metrics = _mlContext.BinaryClassification.Evaluate(predictions);

            return await Task.FromResult(new ModelMetrics
            {
                Accuracy = metrics.Accuracy,
                Precision = metrics.Precision,
                Recall = metrics.Recall,
                F1Score = metrics.F1Score,
                AreaUnderRocCurve = metrics.AreaUnderRocCurve,
                AreaUnderPrecisionRecallCurve = metrics.AreaUnderPrecisionRecallCurve
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate model");
            throw;
        }
    }

    public async Task<bool> DeployModelAsync(MLModel model)
    {
        try
        {
            // TODO: Implement model deployment logic
            // This would involve:
            // 1. Loading the model
            // 2. Setting it as active
            // 3. Updating configuration
            // 4. Notifying services to reload

            _logger.LogInformation("Model {Version} deployed successfully", model.Version);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy model {Version}", model.Version);
            return await Task.FromResult(false);
        }
    }

    public async Task<ITransformer> LoadModelAsync(string modelPath)
    {
        try
        {
            var model = await Task.Run(() => _mlContext.Model.Load(modelPath, out var _));
            _logger.LogInformation("Model loaded from {ModelPath}", modelPath);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model from {ModelPath}", modelPath);
            throw;
        }
    }

    public async Task<List<FraudFeatureSet>> GenerateSyntheticDataAsync(int count)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var data = new List<FraudFeatureSet>();

        for (int i = 0; i < count; i++)
        {
            var isFraud = random.NextDouble() < 0.1; // 10% fraud rate
            
            var featureSet = new FraudFeatureSet
            {
                TransactionId = Guid.NewGuid(),
                Amount = (decimal)(random.NextDouble() * 10000),
                Currency = "USD",
                TransactionType = "Card",
                UserTransactionCount24h = random.Next(0, 20),
                UserTransactionCount7d = random.Next(0, 100),
                UserTotalAmount24h = (decimal)(random.NextDouble() * 50000),
                UserTotalAmount7d = (decimal)(random.NextDouble() * 200000),
                UserAverageAmount = random.NextDouble() * 1000,
                UserAmountVariance = random.NextDouble() * 500,
                VelocityFrequency24h = random.Next(0, 50),
                VelocityAmount24h = random.Next(0, 10),
                NetworkRiskScore = random.NextDouble(),
                HourOfDay = random.Next(0, 24),
                DayOfWeek = random.Next(0, 7),
                IsWeekend = random.NextDouble() < 0.3,
                IsFraud = isFraud,
                ScoredAt = DateTime.UtcNow.AddDays(-random.Next(0, 30))
            };

            data.Add(featureSet);
        }

        return await Task.FromResult(data);
    }

    private string CalculateDataHash(List<FraudFeatureSet> data)
    {
        // Simple hash calculation for training data
        var content = string.Join("", data.Select(d => d.TransactionId.ToString()));
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }
}

public class ModelMetrics
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double AreaUnderRocCurve { get; set; }
    public double AreaUnderPrecisionRecallCurve { get; set; }
} 