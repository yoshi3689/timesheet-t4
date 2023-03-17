using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TimesheetApp.Helpers
{
    /// <summary>
    /// This class is used to manage keys that are stored in the database. We can't just store the private key as plain text, so we password encrypt it. pass is text bytes and password to either encrypted or decrypt.
    /// </summary>
    public static class KeyHelper
    {
        private const int Keysize = 128;
        private const int DerivationIterations = 1000;

        public static byte[] Encrypt(byte[] plainText, string passPhrase)
        {
            var saltStringBytes = RandomBits();
            var ivStringBytes = RandomBits();
            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations, HashAlgorithmName.SHA256))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = Aes.Create())
                {
                    symmetricKey.BlockSize = Keysize;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(plainText, 0, plainText.Length);
                                cryptoStream.FlushFinalBlock();
                                var cipherTextBytes = saltStringBytes;
                                cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
                                cipherTextBytes = cipherTextBytes.Concat(memoryStream.ToArray()).ToArray();
                                memoryStream.Close();
                                cryptoStream.Close();
                                return cipherTextBytes;
                            }
                        }
                    }
                }
            }
        }

        public static byte[] Decrypt(byte[] cipherText, string passPhrase)
        {
            var saltStringBytes = cipherText.Take(Keysize / 8).ToArray();
            var ivStringBytes = cipherText.Skip(Keysize / 8).Take(Keysize / 8).ToArray();
            var cipherTextBytes = cipherText.Skip((Keysize / 8) * 2).Take(cipherText.Length - ((Keysize / 8) * 2)).ToArray();

            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations, HashAlgorithmName.SHA256))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = Aes.Create())
                {
                    symmetricKey.BlockSize = Keysize;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                byte[] buffer = new byte[4096];
                                using (var outputStream = new MemoryStream())
                                {
                                    int bytesRead;
                                    while ((bytesRead = cryptoStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        outputStream.Write(buffer, 0, bytesRead);
                                    }
                                    return outputStream.ToArray();
                                }
                            }
                        }
                    }
                }
            }
        }

        private static byte[] RandomBits()
        {
            var randomBytes = new byte[16];
            using (var rngCsp = RandomNumberGenerator.Create())
            {
                rngCsp.GetBytes(randomBytes);
            }
            return randomBytes;
        }
    }
}