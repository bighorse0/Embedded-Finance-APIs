using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Security;

public interface IMFAService
{
    Task<string> GenerateTOTPSecretAsync(string userId);
    Task<bool> ValidateTOTPAsync(string userId, string code);
    Task<string> GenerateSMSVerificationAsync(string phoneNumber);
    Task<bool> ValidateSMSVerificationAsync(string phoneNumber, string code);
    Task<bool> ValidateBackupCodeAsync(string userId, string code);
    Task<string[]> GenerateBackupCodesAsync(string userId);
}

public class MFAService : IMFAService
{
    private readonly ILogger<MFAService> _logger;
    private readonly Dictionary<string, string> _totpSecrets;
    private readonly Dictionary<string, string> _smsCodes;
    private readonly Dictionary<string, string[]> _backupCodes;

    public MFAService(ILogger<MFAService> logger)
    {
        _logger = logger;
        _totpSecrets = new Dictionary<string, string>();
        _smsCodes = new Dictionary<string, string>();
        _backupCodes = new Dictionary<string, string[]>();
    }

    public async Task<string> GenerateTOTPSecretAsync(string userId)
    {
        var secret = GenerateRandomSecret(32);
        _totpSecrets[userId] = secret;
        
        _logger.LogInformation("Generated TOTP secret for user: {UserId}", userId);
        return await Task.FromResult(secret);
    }

    public async Task<bool> ValidateTOTPAsync(string userId, string code)
    {
        if (!_totpSecrets.ContainsKey(userId))
        {
            _logger.LogWarning("No TOTP secret found for user: {UserId}", userId);
            return false;
        }

        var secret = _totpSecrets[userId];
        var expectedCode = GenerateTOTPCode(secret);
        
        var isValid = code == expectedCode;
        _logger.LogInformation("TOTP validation for user {UserId}: {IsValid}", userId, isValid);
        
        return await Task.FromResult(isValid);
    }

    public async Task<string> GenerateSMSVerificationAsync(string phoneNumber)
    {
        var code = GenerateRandomCode(6);
        _smsCodes[phoneNumber] = code;
        
        // TODO: Integrate with SMS provider (Twilio, etc.)
        _logger.LogInformation("Generated SMS code for {PhoneNumber}: {Code}", phoneNumber, code);
        
        return await Task.FromResult(code);
    }

    public async Task<bool> ValidateSMSVerificationAsync(string phoneNumber, string code)
    {
        if (!_smsCodes.ContainsKey(phoneNumber))
        {
            _logger.LogWarning("No SMS code found for phone: {PhoneNumber}", phoneNumber);
            return false;
        }

        var expectedCode = _smsCodes[phoneNumber];
        var isValid = code == expectedCode;
        
        if (isValid)
        {
            _smsCodes.Remove(phoneNumber); // One-time use
        }
        
        _logger.LogInformation("SMS validation for {PhoneNumber}: {IsValid}", phoneNumber, isValid);
        return await Task.FromResult(isValid);
    }

    public async Task<bool> ValidateBackupCodeAsync(string userId, string code)
    {
        if (!_backupCodes.ContainsKey(userId))
        {
            _logger.LogWarning("No backup codes found for user: {UserId}", userId);
            return false;
        }

        var codes = _backupCodes[userId];
        var isValid = codes.Contains(code);
        
        if (isValid)
        {
            // Remove used backup code
            var updatedCodes = codes.Where(c => c != code).ToArray();
            _backupCodes[userId] = updatedCodes;
        }
        
        _logger.LogInformation("Backup code validation for user {UserId}: {IsValid}", userId, isValid);
        return await Task.FromResult(isValid);
    }

    public async Task<string[]> GenerateBackupCodesAsync(string userId)
    {
        var codes = new string[10];
        for (int i = 0; i < 10; i++)
        {
            codes[i] = GenerateRandomCode(8);
        }
        
        _backupCodes[userId] = codes;
        
        _logger.LogInformation("Generated backup codes for user: {UserId}", userId);
        return await Task.FromResult(codes);
    }

    private string GenerateRandomSecret(int length)
    {
        var random = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(random);
        return Convert.ToBase64String(random);
    }

    private string GenerateRandomCode(int length)
    {
        var random = new Random();
        var code = new StringBuilder();
        for (int i = 0; i < length; i++)
        {
            code.Append(random.Next(0, 10));
        }
        return code.ToString();
    }

    private string GenerateTOTPCode(string secret)
    {
        // Simplified TOTP implementation
        // TODO: Implement proper TOTP algorithm (RFC 6238)
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var data = Encoding.UTF8.GetBytes(secret + timestamp);
        
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(data);
        var code = Math.Abs(BitConverter.ToInt32(hash, 0)) % 1000000;
        
        return code.ToString("D6");
    }
} 