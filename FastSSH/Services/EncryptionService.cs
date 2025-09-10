using System.Security.Cryptography;
using System.Text;

namespace FastSSH.Services;

public class EncryptionService
{
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("FastSSH_Salt_2024");
    
    public static string Encrypt(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        
        using var aes = Aes.Create();
        var key = new Rfc2898DeriveBytes(password, Salt, 10000, HashAlgorithmName.SHA256);
        aes.Key = key.GetBytes(32);
        aes.IV = key.GetBytes(16);
        
        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        cs.Write(plainBytes, 0, plainBytes.Length);
        cs.Close();
        
        return Convert.ToBase64String(ms.ToArray());
    }
    
    public static string Decrypt(string cipherText, string password)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        byte[] cipherBytes = Convert.FromBase64String(cipherText);
        
        using var aes = Aes.Create();
        var key = new Rfc2898DeriveBytes(password, Salt, 10000, HashAlgorithmName.SHA256);
        aes.Key = key.GetBytes(32);
        aes.IV = key.GetBytes(16);
        
        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        
        return reader.ReadToEnd();
    }
}