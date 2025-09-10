using FastSSH.Models;
using FastSSH.Services;
using Xunit;

namespace FastSSH.Tests;

public class ConfigurationServiceTests
{
    [Fact]
    public async Task ChangePassword_SaveAndLoadWithNewPassword_ShouldWork()
    {
        // Arrange
        var configService = new ConfigurationService();
        var config = new ServerConfigCollection();
        config.Servers.Add(new ServerConfig 
        { 
            Name = "test-server", 
            Host = "test.com", 
            Username = "testuser",
            Password = "testpass"
        });
        
        string originalPassword = "original_password";
        string newPassword = "new_password";
        
        // Save with original password
        await configService.SaveConfigurationAsync(config, originalPassword);
        
        // Load with original password
        var loadedConfig = await configService.LoadConfigurationAsync(originalPassword);
        
        // Act - Change password by saving with new password
        await configService.SaveConfigurationAsync(loadedConfig, newPassword);
        
        // Assert - Should be able to load with new password
        var configWithNewPassword = await configService.LoadConfigurationAsync(newPassword);
        Assert.NotNull(configWithNewPassword);
        Assert.Single(configWithNewPassword.Servers);
        Assert.Equal("test-server", configWithNewPassword.Servers[0].Name);
        
        // Should not be able to load with old password anymore
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            configService.LoadConfigurationAsync(originalPassword));
    }
    
    [Fact]
    public async Task LoadConfiguration_WrongPassword_ShouldThrowException()
    {
        // Arrange
        var configService = new ConfigurationService();
        var config = new ServerConfigCollection();
        config.Servers.Add(new ServerConfig 
        { 
            Name = "test-server", 
            Host = "test.com", 
            Username = "testuser"
        });
        
        string correctPassword = "correct_password";
        string wrongPassword = "wrong_password";
        
        // Save with correct password
        await configService.SaveConfigurationAsync(config, correctPassword);
        
        // Act & Assert - Should throw when using wrong password
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            configService.LoadConfigurationAsync(wrongPassword));
    }

    [Fact]
    public async Task StoreCentralizedKey_ValidKeyFile_ShouldCopyToKeysDirectory()
    {
        // Arrange
        var configService = new ConfigurationService();
        string tempKeyFile = Path.GetTempFileName();
        string keyContent = "-----BEGIN PRIVATE KEY-----\ntest key content\n-----END PRIVATE KEY-----";
        await File.WriteAllTextAsync(tempKeyFile, keyContent);
        
        try
        {
            // Act
            string centralizedPath = await configService.StoreCentralizedKeyAsync(tempKeyFile, "test-server");
            
            // Assert
            Assert.True(File.Exists(centralizedPath));
            Assert.Contains(configService.GetKeysDirectoryPath(), centralizedPath);
            Assert.Contains("test-server", centralizedPath);
            
            string copiedContent = await File.ReadAllTextAsync(centralizedPath);
            Assert.Equal(keyContent, copiedContent);
            
            // Cleanup
            configService.RemoveCentralizedKey(centralizedPath);
        }
        finally
        {
            if (File.Exists(tempKeyFile))
                File.Delete(tempKeyFile);
        }
    }

    [Fact]
    public async Task StoreCentralizedKey_NonExistentKeyFile_ShouldThrowException()
    {
        // Arrange
        var configService = new ConfigurationService();
        string nonExistentFile = "/path/to/nonexistent/keyfile";
        
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            configService.StoreCentralizedKeyAsync(nonExistentFile, "test-server"));
    }

    [Fact]
    public void IsCentralizedKey_KeyInKeysDirectory_ShouldReturnTrue()
    {
        // Arrange
        var configService = new ConfigurationService();
        string keysDir = configService.GetKeysDirectoryPath();
        string centralizedKeyPath = Path.Combine(keysDir, "test-server_id_rsa_20241201_120000");
        
        // Act & Assert
        Assert.True(configService.IsCentralizedKey(centralizedKeyPath));
    }

    [Fact]
    public void IsCentralizedKey_KeyOutsideKeysDirectory_ShouldReturnFalse()
    {
        // Arrange
        var configService = new ConfigurationService();
        string externalKeyPath = "/home/user/.ssh/id_rsa";
        
        // Act & Assert
        Assert.False(configService.IsCentralizedKey(externalKeyPath));
    }

    [Fact]
    public void RemoveCentralizedKey_ExistingCentralizedKey_ShouldDeleteFile()
    {
        // Arrange
        var configService = new ConfigurationService();
        string keysDir = configService.GetKeysDirectoryPath();
        Directory.CreateDirectory(keysDir);
        
        string testKeyPath = Path.Combine(keysDir, "test_key_file");
        File.WriteAllText(testKeyPath, "test key content");
        
        // Act
        configService.RemoveCentralizedKey(testKeyPath);
        
        // Assert
        Assert.False(File.Exists(testKeyPath));
    }

    [Fact]
    public void RemoveCentralizedKey_NonExistentFile_ShouldNotThrow()
    {
        // Arrange
        var configService = new ConfigurationService();
        string keysDir = configService.GetKeysDirectoryPath();
        string nonExistentPath = Path.Combine(keysDir, "nonexistent_key");
        
        // Act & Assert - Should not throw
        configService.RemoveCentralizedKey(nonExistentPath);
    }
}