using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace CrsterCommand.Services;

public static class SecurityService
{
    public static string GetMacAddress()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        var mac = interfaces
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => nic.GetPhysicalAddress().ToString())
            .FirstOrDefault(s => !string.IsNullOrEmpty(s));

        return mac ?? "DEFAULT_KEY_123456"; // Fallback if no network interface found
    }

    public static string Encrypt(string plainText, string key)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        
        byte[] iv = new byte[16];
        byte[] array;

        using (Aes aes = Aes.Create())
        {
            aes.Key = DeriveKey(key);
            aes.IV = iv;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                    {
                        streamWriter.Write(plainText);
                    }

                    array = memoryStream.ToArray();
                }
            }
        }

        return Convert.ToBase64String(array);
    }

    public static string Decrypt(string cipherText, string key)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        
        byte[] iv = new byte[16];
        byte[] buffer = Convert.FromBase64String(cipherText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = DeriveKey(key);
            aes.IV = iv;
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using (MemoryStream memoryStream = new MemoryStream(buffer))
            {
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader streamReader = new StreamReader(cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }
    }

    private static byte[] DeriveKey(string key)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        }
    }
}
