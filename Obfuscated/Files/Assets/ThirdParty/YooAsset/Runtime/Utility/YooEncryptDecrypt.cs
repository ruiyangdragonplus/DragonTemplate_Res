using System;
using System.Security.Cryptography;
using System.Text;

namespace YooAsset
{
    internal static class YooEncryptDecrypt
    {
        // 128位加密：16字节
        private const string Key = "dmansionstorykey";
        private static ICryptoTransform encryptor;
        private static ICryptoTransform decryptor;

        static void Init()
        {
            RijndaelManaged rm = new RijndaelManaged
            {
                Key = Encoding.UTF8.GetBytes(Key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
            encryptor = rm.CreateEncryptor();
            decryptor = rm.CreateDecryptor();
        }

        // 加密
        public static byte[] EncryptBytes(byte[] input)
        {
            if (input == null) return null;

            byte[] toEncryptArray = input;

            if (encryptor == null)
            {
                Init();
            }

            byte[] resultArray = encryptor.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

            return resultArray;

        }

        // 解密
        public static byte[] DecryptBytes(byte[] input)
        {
            if (input == null) return null;

            byte[] toEncryptArray = input;

            if (decryptor == null)
            {
                Init();
            }

            byte[] resultArray = decryptor.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

            return resultArray;
        }
    }
}