using System;
using System.IO;
using System.Security.Cryptography;

namespace PKWeb;

public static class EncryptUtil
{
    // 返回加密后的数据
    public static byte[] Encrypt(byte[] Key, byte[] plain)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();

        using var enc = aes.CreateEncryptor();
        byte[] body = enc.TransformFinalBlock(plain, 0, plain.Length);

        byte[] result = new byte[aes.IV.Length + body.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(body, 0, result, aes.IV.Length, body.Length);
        return result;
    }

    // 返回解密后的数据
    public static byte[] Decrypt(byte[] Key, byte[] cipher)
    {
        if (cipher.Length < 16) throw new InvalidDataException("密文过短");

        byte[] iv = new byte[16];
        Buffer.BlockCopy(cipher, 0, iv, 0, 16);

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = iv;

        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, 16, cipher.Length - 16);
    }

    public static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}