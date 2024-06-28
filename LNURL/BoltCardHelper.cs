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


        public static (string uid, uint counter, byte[] rawUid, byte[] rawCtr)? ExtractUidAndCounterFromP(string pHex,
            byte[] aesKey, out string? error)
        {
            if (!HexEncoder.IsWellFormed(pHex))
            {
                error = "p parameter is not hex";
                return null;
            }

            return ExtractUidAndCounterFromP(Convert.FromHexString(pHex), aesKey, out error);
        }

        public static (string uid, uint counter, byte[] rawUid, byte[] rawCtr)? ExtractUidAndCounterFromP(byte[] p,
            byte[] aesKey, out string? error)
        {
            if (p.Length != 16)
            {
                error = "p parameter length not valid";
                return null;
            }

            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = new byte[16]; // assuming IV is zeros. Adjust if needed.
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using var memoryStream = new System.IO.MemoryStream(p);
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var reader = new System.IO.BinaryReader(cryptoStream);
            var decryptedPData = reader.ReadBytes(p.Length);
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

            return (uidStr, c, uid, ctr);
        }

        /// <summary>
        /// Extracts BoltCard information from a given request URI.
        /// </summary>
        /// <param name="requestUri">The URI containing BoltCard data.</param>
        /// <param name="aesKey">The AES key for decryption.</param>
        /// <param name="error">Outputs an error string if extraction fails.</param>
        /// <returns>A tuple containing the UID and counter if successful; null otherwise.</returns>
        public static (string uid, uint counter, byte[] rawUid, byte[] rawCtr, byte[] c)? ExtractBoltCardFromRequest(
            Uri requestUri, byte[] aesKey,
            out string error)
        {
            var query = requestUri.ParseQueryString();

            var pParam = query.Get("p");
            if (pParam is null)
            {
                error = "p parameter is missing";
                return null;
            }

            var pResult = ExtractUidAndCounterFromP(pParam, aesKey, out error);
            if (error is not null || pResult is null)
            {
                return null;
            }

            var cParam = query.Get("c");

            if (cParam is null)
            {
                error = "c parameter is missing";
                return null;
            }


            if (!HexEncoder.IsWellFormed(cParam))
            {
                error = "c parameter is not hex";
                return null;
            }

            var cRaw = Convert.FromHexString(cParam);
            if (cRaw.Length != 8)
            {
                error = "c parameter length not valid";
                return null;
            }


            return (pResult.Value.uid, pResult.Value.counter, pResult.Value.rawUid, pResult.Value.rawCtr, cRaw);
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

        private static byte[] GetSunMac(byte[] key, byte[] sv2)
        {
            var cmac1 = AesCmac(key, sv2);
            var cmac2 = AesCmac(cmac1, Array.Empty<byte>());

            var halfMac = new byte[cmac2.Length / 2];
            for (var i = 1; i < cmac2.Length; i += 2)
            {
                halfMac[i >> 1] = cmac2[i];
            }

            return halfMac;
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

            byte[] sv2 = new byte[]
            {
                0x3c, 0xc3, 0x00, 0x01, 0x00, 0x80,
                uid[0], uid[1], uid[2], uid[3], uid[4], uid[5], uid[6],
                ctr[0], ctr[1], ctr[2]
            };

            try
            {
                byte[] computedCmac = GetSunMac(k2CmacKey, sv2);

                if (computedCmac.Length != cmac.Length)
                {
                    error = "Computed CMAC length mismatch.";
                    return false;
                }

                if (!computedCmac.SequenceEqual(cmac))
                {
                    error = "CMAC verification failed.";
                    return false;
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

        public static byte[] CreateCValue(string uid, uint counter, byte[] k2CmacKey)
        {
            var ctr = new byte[3];
            ctr[2] = (byte) (counter >> 16);
            ctr[1] = (byte) (counter >> 8);
            ctr[0] = (byte) (counter);

            var uidBytes = Convert.FromHexString(uid);
            return CreateCValue(uidBytes, ctr, k2CmacKey);
        }

        public static byte[] CreateCValue(byte[] uid, byte[] counter, byte[] k2CmacKey)
        {
            if (uid.Length != 7 || counter.Length != 3 || k2CmacKey.Length != AES_BLOCK_SIZE)
            {
                throw new ArgumentException("Invalid input lengths.");
            }

            byte[] sv2 =
            {
                0x3c, 0xc3, 0x00, 0x01, 0x00, 0x80,
                uid[0], uid[1], uid[2], uid[3], uid[4], uid[5], uid[6],
                counter[0], counter[1], counter[2]
            };

            var computedCmac = GetSunMac(k2CmacKey, sv2);

            return computedCmac;
        }

        public static byte[] CreatePValue(byte[] aesKey, uint counter, string uid)
        {
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = new byte[16]; // assuming IV is zeros. Adjust if needed.
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;

            // Constructing the 16-byte array to be encrypted
            byte[] toEncrypt = new byte[16];
            toEncrypt[0] = 0xC7; // First byte is 0xC7

            var uidBytes = Convert.FromHexString(uid);
            Array.Copy(uidBytes, 0, toEncrypt, 1, uidBytes.Length);

            // Counter
            toEncrypt[8] = (byte) (counter & 0xFF); // least-significant byte
            toEncrypt[9] = (byte) ((counter >> 8) & 0xFF);
            toEncrypt[10] = (byte) ((counter >> 16) & 0xFF);

            // Encryption
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] encryptedData;
            using var memoryStream = new System.IO.MemoryStream();
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            {
                cryptoStream.Write(toEncrypt, 0, toEncrypt.Length);
            }

            encryptedData = memoryStream.ToArray();

            var result = ExtractUidAndCounterFromP(encryptedData, aesKey, out var error);
            
            return encryptedData;
        }
    }
}