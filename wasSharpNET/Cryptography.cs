///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Security.Cryptography;

namespace wasSharpNET
{
    public class Cryptography
    {
        /// <summary>
        ///     Encrypts a string given a key and initialization vector.
        /// </summary>
        /// <param name="data">the string to encrypt</param>
        /// <param name="Key">the key</param>
        /// <param name="IV">the initialization bector</param>
        /// <returns>Base64 encoded encrypted data</returns>
        public static string wasAESEncrypt(string data, byte[] Key, byte[] IV)
        {
            byte[] encryptedData;
            using (var rijdanelManaged = new RijndaelManaged())
            {
                //  FIPS-197 / CBC
                rijdanelManaged.BlockSize = 128;
                rijdanelManaged.Mode = CipherMode.CBC;

                rijdanelManaged.Key = Key;
                rijdanelManaged.IV = IV;

                var encryptor = rijdanelManaged.CreateEncryptor(rijdanelManaged.Key, rijdanelManaged.IV);

                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write)
                        )
                    {
                        using (var streamWriter = new StreamWriter(cryptoStream))
                        {
                            streamWriter.Write(data);
                            streamWriter.Flush();
                        }
                        encryptedData = memoryStream.ToArray();
                    }
                }
            }
            return Convert.ToBase64String(encryptedData);
        }

        /// <summary>
        ///     Decrypts a Base64 encoded string using AES with a given key and initialization vector.
        /// </summary>
        /// <param name="data">a Base64 encoded string of the data to decrypt</param>
        /// <param name="Key">the key</param>
        /// <param name="IV">the initialization vector</param>
        /// <returns>the decrypted data</returns>
        public static string wasAESDecrypt(string data, byte[] Key, byte[] IV)
        {
            string plaintext;
            using (var rijdanelManaged = new RijndaelManaged())
            {
                //  FIPS-197 / CBC
                rijdanelManaged.BlockSize = 128;
                rijdanelManaged.Mode = CipherMode.CBC;

                rijdanelManaged.Key = Key;
                rijdanelManaged.IV = IV;

                var decryptor = rijdanelManaged.CreateDecryptor(rijdanelManaged.Key, rijdanelManaged.IV);

                using (var memoryStream = new MemoryStream(Convert.FromBase64String(data)))
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
            return plaintext;
        }
    }
}