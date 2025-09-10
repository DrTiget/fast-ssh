using FastSSH.Models;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace FastSSH.Services;

public class ConfigurationService
{
    private const string ConfigFileName = "fast-ssh.conf";
    private const string KeysDirectoryName = "keys";
    private readonly string _configPath;
    private readonly string _configDir;
    private readonly string _keysDir;

    public ConfigurationService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastSSH");
        }
        else
        {
            _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "fast-ssh");
        }

        Directory.CreateDirectory(_configDir);
        _configPath = Path.Combine(_configDir, ConfigFileName);
        _keysDir = Path.Combine(_configDir, KeysDirectoryName);
    }

    public async Task<ServerConfigCollection> LoadConfigurationAsync(string password)
    {
        if (!File.Exists(_configPath))
        {
            return new ServerConfigCollection();
        }

        try
        {
            string encryptedContent = await File.ReadAllTextAsync(_configPath);
            string decryptedContent = EncryptionService.Decrypt(encryptedContent, password);
            
            var config = JsonConvert.DeserializeObject<ServerConfigCollection>(decryptedContent);
            return config ?? new ServerConfigCollection();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration: {ex.Message}", ex);
        }
    }

    public async Task SaveConfigurationAsync(ServerConfigCollection config, string password)
    {
        try
        {
            string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            string encryptedContent = EncryptionService.Encrypt(jsonContent, password);
            
            await File.WriteAllTextAsync(_configPath, encryptedContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    public bool ConfigurationExists()
    {
        return File.Exists(_configPath);
    }

    public string GetConfigurationPath()
    {
        return _configPath;
    }

    public string GetKeysDirectoryPath()
    {
        return _keysDir;
    }

    /// <summary>
    /// Copies an SSH key file to the centralized keys storage and returns the new path
    /// </summary>
    /// <param name="originalKeyPath">Path to the original key file</param>
    /// <param name="serverName">Name of the server this key belongs to</param>
    /// <returns>Path to the copied key in centralized storage</returns>
    public async Task<string> StoreCentralizedKeyAsync(string originalKeyPath, string serverName)
    {
        if (!File.Exists(originalKeyPath))
        {
            throw new FileNotFoundException($"SSH key file not found: {originalKeyPath}");
        }

        // Create keys directory if it doesn't exist
        Directory.CreateDirectory(_keysDir);

        // Generate unique filename for the centralized key
        string originalFileName = Path.GetFileName(originalKeyPath);
        string extension = Path.GetExtension(originalFileName);
        string baseName = Path.GetFileNameWithoutExtension(originalFileName);
        
        // Use server name and timestamp to ensure uniqueness
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string centralizedFileName = $"{serverName}_{baseName}_{timestamp}{extension}";
        string centralizedPath = Path.Combine(_keysDir, centralizedFileName);

        try
        {
            // Copy the key file to centralized storage
            await File.ReadAllBytesAsync(originalKeyPath)
                .ContinueWith(async task => 
                {
                    byte[] keyData = await task;
                    await File.WriteAllBytesAsync(centralizedPath, keyData);
                }).Unwrap();

            // Preserve file permissions on Unix systems
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Set restrictive permissions (600 - owner read/write only)
                File.SetUnixFileMode(centralizedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            return centralizedPath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to copy SSH key to centralized storage: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Removes a centralized key file
    /// </summary>
    /// <param name="centralizedKeyPath">Path to the centralized key file</param>
    public void RemoveCentralizedKey(string centralizedKeyPath)
    {
        if (File.Exists(centralizedKeyPath) && centralizedKeyPath.StartsWith(_keysDir))
        {
            try
            {
                File.Delete(centralizedKeyPath);
            }
            catch (Exception ex)
            {
                // Log warning but don't throw - missing key files shouldn't break operations
                Console.WriteLine($"Warning: Could not delete centralized key file {centralizedKeyPath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Checks if a path points to a centralized key
    /// </summary>
    /// <param name="keyPath">Path to check</param>
    /// <returns>True if the path is in centralized storage</returns>
    public bool IsCentralizedKey(string keyPath)
    {
        return !string.IsNullOrEmpty(keyPath) && keyPath.StartsWith(_keysDir);
    }
}