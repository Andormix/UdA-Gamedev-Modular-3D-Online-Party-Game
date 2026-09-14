using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class EconomySaveSystem
{
#if UNITY_EDITOR
    private const string SaveFileName = "economy_save_EDITOR.json";
    private const string HashFileName = "economy_save_EDITOR.hash";
#else
    private const string SaveFileName = "economy_save_BUILD.json";
    private const string HashFileName = "economy_save_BUILD.hash";
#endif

    // Simple app-local secret for tamper check (requeriment no funcional - offline).
    private const string HashSecret = "EL_PAS_DE_LA_CAIXA_LOCAL_SECRET_V1";

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    private static string HashPath => Path.Combine(Application.persistentDataPath, HashFileName);

    public static EconomySaveData LoadOrCreate()
    {
        EconomySaveData data = null;

        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);

                // Tamper check
                if (!VerifyHash(json))
                {
                    Debug.LogWarning("[Economy] Save hash mismatch detected. Creating fresh economy save.");
                    data = new EconomySaveData();
                    Save(data);
                    return data;
                }

                data = JsonUtility.FromJson<EconomySaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Economy] Load failed, creating new save. {e.Message}");
            }
        }

        if (data == null)
            data = new EconomySaveData();

        Save(data);
        return data;
    }

    public static void Save(EconomySaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);

            string hash = ComputeHash(json);
            File.WriteAllText(HashPath, hash);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Economy] Save failed: {e.Message}");
        }
    }

    private static bool VerifyHash(string json)
    {
        if (!File.Exists(HashPath))
            return false;

        string storedHash = File.ReadAllText(HashPath);
        string currentHash = ComputeHash(json);
        return string.Equals(storedHash, currentHash, StringComparison.Ordinal);
    }

    private static string ComputeHash(string payload)
    {
        string mixed = payload + "|" + HashSecret;
        byte[] bytes = Encoding.UTF8.GetBytes(mixed);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}