using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using NBitcoin.DataEncoders;

namespace LNURL
{
    public class BoltCardHelper
    {
        private const int AES_BLOCK_SIZE = 16;

        /// <summary>
        /// Extracts BoltCard information from a given request URI.
        /// </summary>
        /// <param name="requestUri">The URI containing BoltCard data.</param>
        /// <param name="aesKey">The AES key for decryption.</param>
        /// <param name="error">Outputs an error string if extraction fails.</param>
        /// <returns>A tuple containing the UID and counter if successful; null otherwise.</returns>
        public static (string uid, uint counter)? ExtractBoltCardFromRequest(Uri requestUri, byte[] aesKey,
            out string error)
        {
            var query = requestUri.ParseQueryString();

            var pParam = query.Get("p");
            if (pParam is null)
            {
                error = "p parameter is missing";
                return null;
            }

            var cParam = query.Get("c");

            if (cParam is null)
            {
                error = "c parameter is missing";
                return null;
            }

            if (!HexEncoder.IsWellFormed(pParam))
            {
                error = "p parameter is not hex";
                return null;
            }

            if (!HexEncoder.IsWellFormed(cParam))
            {
                error = "c parameter is not hex";
                return null;
            }

            var pRaw = Convert.FromHexString(pParam);
            var cRaw = Convert.FromHexString(cParam);
            if (pRaw.Length != 16)
            {
                error = "p parameter length not valid";
                return null;
            }

            if (cRaw.Length != 8)
            {
                error = "c parameter length not valid";
                return null;
            }

            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = new byte[16]; // assuming IV is zeros. Adjust if needed.
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using var memoryStream = new System.IO.MemoryStream(pRaw);
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var reader = new System.IO.BinaryReader(cryptoStream);
            var decryptedPData = reader.ReadBytes(pRaw.Length);
            if (decryptedPData[0] != 0xC7)
            {
                error = "decrypted data not starting with 0xC7";
                return null;
            }

            var uid = decryptedPData[1..8];
            var ctr = decryptedPData[8..11];

            var c = (uint) (ctr[2] << 16 | ctr[1] << 8 | ctr[0]);
            var uidStr = BitConverter.ToString(uid).Replace("-", "").ToLower();
            error = null;
            return (uidStr, c);
        }

        private static byte[] AesEncrypt(byte[] key, byte[] iv, byte[] data)
        {
            using MemoryStream ms = new MemoryStream();
            using var aes = Aes.Create();

            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;

            using var cs = new CryptoStream(ms, aes.CreateEncryptor(key, iv), CryptoStreamMode.Write);
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();

            return ms.ToArray();
        }

        private static byte[] RotateLeft(byte[] b)
        {
            byte[] r = new byte[b.Length];
            byte carry = 0;

            for (int i = b.Length - 1; i >= 0; i--)
            {
                ushort u = (ushort) (b[i] << 1);
                r[i] = (byte) ((u & 0xff) + carry);
                carry = (byte) ((u & 0xff00) >> 8);
            }

            return r;
        }

        private static byte[] AesCmac(byte[] key, byte[] data)
        {
            // SubKey generation
            // step 1, AES-128 with key K is applied to an all-zero input block.
            byte[] L = AesEncrypt(key, new byte[16], new byte[16]);

            // step 2, K1 is derived through the following operation:
            byte[]
                FirstSubkey =
                    RotateLeft(L); //If the most significant bit of L is equal to 0, K1 is the left-shift of L by 1 bit.
            if ((L[0] & 0x80) == 0x80)
                FirstSubkey[15] ^=
                    0x87; // Otherwise, K1 is the exclusive-OR of const_Rb and the left-shift of L by 1 bit.

            // step 3, K2 is derived through the following operation:
            byte[]
                SecondSubkey =
                    RotateLeft(FirstSubkey); // If the most significant bit of K1 is equal to 0, K2 is the left-shift of K1 by 1 bit.
            if ((FirstSubkey[0] & 0x80) == 0x80)
                SecondSubkey[15] ^=
                    0x87; // Otherwise, K2 is the exclusive-OR of const_Rb and the left-shift of K1 by 1 bit.

            // MAC computing
            if (((data.Length != 0) && (data.Length % 16 == 0)) == true)
            {
                // If the size of the input message block is equal to a positive multiple of the block size (namely, 128 bits),
                // the last block shall be exclusive-OR'ed with K1 before processing
                for (int j = 0; j < FirstSubkey.Length; j++)
                    data[data.Length - 16 + j] ^= FirstSubkey[j];
            }
            else
            {
                // Otherwise, the last block shall be padded with 10^i
                byte[] padding = new byte[16 - data.Length % 16];
                padding[0] = 0x80;

                data = data.Concat(padding.AsEnumerable()).ToArray();

                // and exclusive-OR'ed with K2
                for (int j = 0; j < SecondSubkey.Length; j++)
                    data[data.Length - 16 + j] ^= SecondSubkey[j];
            }

            // The result of the previous process will be the input of the last encryption.
            byte[] encResult = AesEncrypt(key, new byte[16], data);

            byte[] HashValue = new byte[16];
            Array.Copy(encResult, encResult.Length - HashValue.Length, HashValue, 0, HashValue.Length);

            return HashValue;
        }

        /// <summary>
        /// Verifies the CMAC for given UID, counter, key, and CMAC data.
        /// </summary>
        /// <param name="uid">The user ID.</param>
        /// <param name="ctr">The counter data.</param>
        /// <param name="k2CmacKey">The CMAC key.</param>
        /// <param name="cmac">The CMAC data to verify against.</param>
        /// <param name="error">Outputs an error string if verification fails.</param>
        /// <returns>True if CMAC verification is successful, otherwise false.</returns>
        public static bool CheckCmac(byte[] uid, byte[] ctr, byte[] k2CmacKey, byte[] cmac, out string error)
        {
            if (uid.Length != 7 || ctr.Length != 3 || k2CmacKey.Length != AES_BLOCK_SIZE)
            {
                error = "Invalid input lengths.";
                return false;
            }

            byte[] sv2 = new byte[AES_BLOCK_SIZE]
            {
                0x3c, 0xc3, 0x00, 0x01, 0x00, 0x80,
                uid[0], uid[1], uid[2], uid[3], uid[4], uid[5], uid[6],
                ctr[0], ctr[1], ctr[2]
            };

            try
            {
                byte[] computedCmac = AesCmac(k2CmacKey, sv2);

                if (computedCmac.Length != cmac.Length)
                {
                    error = "Computed CMAC length mismatch.";
                    return false;
                }

                for (int i = 0; i < computedCmac.Length; i++)
                {
                    if (computedCmac[i] != cmac[i])
                    {
                        error = "CMAC verification failed.";
                        return false;
                    }
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}