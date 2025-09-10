using FastSSH.Services;
using Xunit;

namespace FastSSH.Tests;

public class EncryptionServiceTests
{
    [Fact]
    public void Encrypt_Decrypt_RoundTrip_ShouldReturnOriginalText()
    {
        // Arrange
        string originalText = "This is a test message with special characters: !@#$%^&*()";
        string password = "test_password_123";

        // Act
        string encrypted = EncryptionService.Encrypt(originalText, password);
        string decrypted = EncryptionService.Decrypt(encrypted, password);

        // Assert
        Assert.NotEqual(originalText, encrypted);
        Assert.Equal(originalText, decrypted);
    }

    [Fact]
    public void Encrypt_EmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        string originalText = "";
        string password = "test_password";

        // Act
        string encrypted = EncryptionService.Encrypt(originalText, password);

        // Assert
        Assert.Equal("", encrypted);
    }

    [Fact]
    public void Decrypt_EmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        string encryptedText = "";
        string password = "test_password";

        // Act
        string decrypted = EncryptionService.Decrypt(encryptedText, password);

        // Assert
        Assert.Equal("", decrypted);
    }

    [Fact]
    public void Encrypt_SameTextDifferentPasswords_ShouldProduceDifferentResults()
    {
        // Arrange
        string originalText = "Same message";
        string password1 = "password1";
        string password2 = "password2";

        // Act
        string encrypted1 = EncryptionService.Encrypt(originalText, password1);
        string encrypted2 = EncryptionService.Encrypt(originalText, password2);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Decrypt_WrongPassword_ShouldThrowException()
    {
        // Arrange
        string originalText = "Test message";
        string correctPassword = "correct_password";
        string wrongPassword = "wrong_password";

        string encrypted = EncryptionService.Encrypt(originalText, correctPassword);

        // Act & Assert
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
        {
            EncryptionService.Decrypt(encrypted, wrongPassword);
        });
    }
}