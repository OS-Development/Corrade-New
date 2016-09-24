///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace wasSharpNET.Cryptography
{
    public class AES
    {
        private static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        private const int AES_BLOCK_SIZE = 128;
        private const CipherMode AES_CIPHER_MODE = CipherMode.CBC;
        private const PaddingMode AES_PADDING_MODE = PaddingMode.PKCS7;
        private const int AES_KEY_SALT_BYTES = 16;

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Encrypts a string given a key and initialization vector.
        /// </summary>
        /// <param name="data">the string to encrypt</param>
        /// <param name="key">the encryption key</param>
        /// <param name="separator">the separator to use between the cyphertext and the IV</param>
        /// <returns>Base64 encoded encrypted data</returns>
        public string wasAESEncrypt(string data, string key, string separator = ":")
        {
            using (var rijdanelManaged = new RijndaelManaged())
            {
                //  FIPS-197 / CBC
                rijdanelManaged.BlockSize = AES_BLOCK_SIZE;
                rijdanelManaged.Mode = AES_CIPHER_MODE;
                rijdanelManaged.Padding = AES_PADDING_MODE;

                // Compute the salt and the IV from the key.
                var salt = new byte[AES_KEY_SALT_BYTES];
                rng.GetBytes(salt);
                var derivedKey = new Rfc2898DeriveBytes(key, salt);
                rijdanelManaged.Key = derivedKey.GetBytes(rijdanelManaged.KeySize/8);
                rijdanelManaged.IV = derivedKey.GetBytes(rijdanelManaged.BlockSize/8);

                byte[] encryptedData;
                using (var encryptor = rijdanelManaged.CreateEncryptor(rijdanelManaged.Key, rijdanelManaged.IV))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                        {
                            using (var streamWriter = new StreamWriter(cryptoStream))
                            {
                                streamWriter.Write(data);
                            }
                        }
                        encryptedData = memoryStream.ToArray();
                    }
                }
                return string.Join(separator, Convert.ToBase64String(salt), Convert.ToBase64String(encryptedData));
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Decrypts a Base64 encoded string using AES with a given key and initialization vector.
        /// </summary>
        /// <param name="data">
        ///     a string consisting of the cyphertext to decrypt in Base64 and the IV in Base64 separated by the
        ///     separator
        /// </param>
        /// <param name="key">the encryption key</param>
        /// <param name="separator">the separator to use between the cyphertext and the IV</param>
        /// <returns>the decrypted data</returns>
        public string wasAESDecrypt(string data, string key, string separator = ":")
        {
            // retrieve the salt from the data.
            var segments = new List<string>(data.Split(new[] {separator}, StringSplitOptions.None));
            if (!segments.Count.Equals(2))
                throw new ArgumentException("Invalid data.");

            string plaintext;
            using (var rijdanelManaged = new RijndaelManaged())
            {
                //  FIPS-197 / CBC
                rijdanelManaged.BlockSize = AES_BLOCK_SIZE;
                rijdanelManaged.Mode = AES_CIPHER_MODE;
                rijdanelManaged.Padding = AES_PADDING_MODE;

                // Retrieve the key and the IV from the salt.
                var derivedKey = new Rfc2898DeriveBytes(key, Convert.FromBase64String(segments.First().Trim()));
                rijdanelManaged.Key = derivedKey.GetBytes(rijdanelManaged.KeySize/8);
                rijdanelManaged.IV = derivedKey.GetBytes(rijdanelManaged.BlockSize/8);

                using (var decryptor = rijdanelManaged.CreateDecryptor(rijdanelManaged.Key, rijdanelManaged.IV))
                {
                    using (var memoryStream = new MemoryStream(Convert.FromBase64String(segments.Last().Trim())))
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (var streamReader = new StreamReader(cryptoStream))
                            {
                                plaintext = streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            return plaintext;
        }
    }
}