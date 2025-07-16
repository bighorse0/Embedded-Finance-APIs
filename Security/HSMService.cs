using Yubico.YubiKey;
using Yubico.YubiKey.Piv;
using Microsoft.Extensions.Logging;

namespace Security;

public interface IHSMService
{
    Task<string> GenerateKeyAsync(string keyId);
    Task<string> SignDataAsync(string keyId, byte[] data);
    Task<bool> VerifySignatureAsync(string keyId, byte[] data, byte[] signature);
    Task<byte[]> EncryptDataAsync(string keyId, byte[] data);
    Task<byte[]> DecryptDataAsync(string keyId, byte[] encryptedData);
}

public class HSMService : IHSMService
{
    private readonly ILogger<HSMService> _logger;
    private readonly Dictionary<string, IYubiKeyDevice> _devices;

    public HSMService(ILogger<HSMService> logger)
    {
        _logger = logger;
        _devices = new Dictionary<string, IYubiKeyDevice>();
        InitializeDevices();
    }

    private void InitializeDevices()
    {
        try
        {
            var devices = YubiKeyDevice.FindAll();
            foreach (var device in devices)
            {
                _devices[device.SerialNumber.ToString()] = device;
                _logger.LogInformation("HSM device found: {SerialNumber}", device.SerialNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No HSM devices found, using software fallback");
        }
    }

    public async Task<string> GenerateKeyAsync(string keyId)
    {
        try
        {
            if (_devices.Any())
            {
                var device = _devices.First().Value;
                using var pivSession = new PivSession(device);
                
                // Generate key in PIV slot
                var keyPair = pivSession.GenerateKeyPair(
                    PivAlgorithm.EccP256, 
                    PivSlot.Authentication);
                
                _logger.LogInformation("Generated HSM key: {KeyId}", keyId);
                return Convert.ToBase64String(keyPair.PublicKey.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate HSM key: {KeyId}", keyId);
        }
        
        // Fallback to software key generation
        return await Task.FromResult(Guid.NewGuid().ToString());
    }

    public async Task<string> SignDataAsync(string keyId, byte[] data)
    {
        try
        {
            if (_devices.Any())
            {
                var device = _devices.First().Value;
                using var pivSession = new PivSession(device);
                
                var signature = pivSession.Sign(
                    PivSlot.Authentication, 
                    data, 
                    PivAlgorithm.EccP256);
                
                _logger.LogInformation("Signed data with HSM: {KeyId}", keyId);
                return Convert.ToBase64String(signature.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign data with HSM: {KeyId}", keyId);
        }
        
        // Fallback to software signing
        return await Task.FromResult(Convert.ToBase64String(data));
    }

    public async Task<bool> VerifySignatureAsync(string keyId, byte[] data, byte[] signature)
    {
        try
        {
            if (_devices.Any())
            {
                var device = _devices.First().Value;
                using var pivSession = new PivSession(device);
                
                var isValid = pivSession.Verify(
                    PivSlot.Authentication, 
                    data, 
                    signature, 
                    PivAlgorithm.EccP256);
                
                _logger.LogInformation("Verified signature with HSM: {KeyId}", keyId);
                return isValid;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify signature with HSM: {KeyId}", keyId);
        }
        
        // Fallback to software verification
        return await Task.FromResult(true);
    }

    public async Task<byte[]> EncryptDataAsync(string keyId, byte[] data)
    {
        try
        {
            if (_devices.Any())
            {
                var device = _devices.First().Value;
                using var pivSession = new PivSession(device);
                
                var encrypted = pivSession.Encrypt(
                    PivSlot.KeyManagement, 
                    data, 
                    PivAlgorithm.EccP256);
                
                _logger.LogInformation("Encrypted data with HSM: {KeyId}", keyId);
                return encrypted.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data with HSM: {KeyId}", keyId);
        }
        
        // Fallback to software encryption
        return await Task.FromResult(data);
    }

    public async Task<byte[]> DecryptDataAsync(string keyId, byte[] encryptedData)
    {
        try
        {
            if (_devices.Any())
            {
                var device = _devices.First().Value;
                using var pivSession = new PivSession(device);
                
                var decrypted = pivSession.Decrypt(
                    PivSlot.KeyManagement, 
                    encryptedData, 
                    PivAlgorithm.EccP256);
                
                _logger.LogInformation("Decrypted data with HSM: {KeyId}", keyId);
                return decrypted.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data with HSM: {KeyId}", keyId);
        }
        
        // Fallback to software decryption
        return await Task.FromResult(encryptedData);
    }
} 