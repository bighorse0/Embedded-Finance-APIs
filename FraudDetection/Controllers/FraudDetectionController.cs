using Microsoft.AspNetCore.Mvc;
using FraudDetection.Services;
using SharedKernel;

namespace FraudDetection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FraudDetectionController : ControllerBase
{
    private readonly IFraudDetectionService _fraudService;
    private readonly IFeatureEngineeringService _featureService;
    private readonly IModelTrainingService _modelService;
    private readonly ILogger<FraudDetectionController> _logger;

    public FraudDetectionController(
        IFraudDetectionService fraudService,
        IFeatureEngineeringService featureService,
        IModelTrainingService modelService,
        ILogger<FraudDetectionController> logger)
    {
        _fraudService = fraudService;
        _featureService = featureService;
        _modelService = modelService;
        _logger = logger;
    }

    [HttpPost("score")]
    public async Task<ActionResult<FraudScore>> ScoreTransaction([FromBody] Transaction transaction)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var score = await _fraudService.ScoreTransactionAsync(transaction);
            stopwatch.Stop();

            _logger.LogInformation("Transaction {TransactionId} scored in {ElapsedMs}ms with score {Score}", 
                transaction.Id, stopwatch.ElapsedMilliseconds, score.Score);

            // Create alert if high risk
            if (score.Score > 0.8)
            {
                var alert = await _fraudService.CreateAlertAsync(transaction, score.Score);
                _logger.LogWarning("High-risk transaction detected: {TransactionId} with score {Score}", 
                    transaction.Id, score.Score);
            }

            return Ok(score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to score transaction {TransactionId}", transaction.Id);
            return StatusCode(500, new { Error = "Failed to score transaction" });
        }
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<IEnumerable<FraudAlert>>> GetAlerts()
    {
        try
        {
            var alerts = await _fraudService.GetActiveAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get fraud alerts");
            return StatusCode(500, new { Error = "Failed to get alerts" });
        }
    }

    [HttpPost("train")]
    public async Task<ActionResult<MLModel>> TrainModel([FromBody] TrainingRequest request)
    {
        try
        {
            _logger.LogInformation("Starting model training with {Count} samples", request.DataCount);

            // Generate synthetic data for training
            var trainingData = await _modelService.GenerateSyntheticDataAsync(request.DataCount);
            
            // Train model
            var model = await _modelService.TrainModelAsync(trainingData);

            // Deploy if requested
            if (request.DeployAfterTraining)
            {
                var deployed = await _modelService.DeployModelAsync(model);
                if (deployed)
                {
                    model.IsActive = true;
                }
            }

            return Ok(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to train model");
            return StatusCode(500, new { Error = "Failed to train model" });
        }
    }

    [HttpPost("evaluate")]
    public async Task<ActionResult<ModelMetrics>> EvaluateModel([FromBody] EvaluationRequest request)
    {
        try
        {
            // Load model
            var model = await _modelService.LoadModelAsync(request.ModelPath);
            
            // Generate test data
            var testData = await _modelService.GenerateSyntheticDataAsync(request.TestDataCount);
            
            // Evaluate model
            var metrics = await _modelService.EvaluateModelAsync(model, testData);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate model");
            return StatusCode(500, new { Error = "Failed to evaluate model" });
        }
    }

    [HttpPost("features")]
    public async Task<ActionResult<FraudFeatureSet>> ExtractFeatures([FromBody] Transaction transaction)
    {
        try
        {
            var features = await _featureService.ExtractFeaturesAsync(transaction);
            return Ok(features);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract features for transaction {TransactionId}", transaction.Id);
            return StatusCode(500, new { Error = "Failed to extract features" });
        }
    }

    [HttpGet("health")]
    public ActionResult<object> Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Services = new
            {
                FraudDetection = "Running",
                FeatureEngineering = "Running",
                ModelTraining = "Running"
            }
        });
    }
}

public class TrainingRequest
{
    public int DataCount { get; set; } = 10000;
    public bool DeployAfterTraining { get; set; } = false;
}

public class EvaluationRequest
{
    public string ModelPath { get; set; } = string.Empty;
    public int TestDataCount { get; set; } = 1000;
} 