using System;
using System.IO;
using UnityEngine;

namespace Hollowmere.Core
{
    /// <summary>
    /// JSON persistence under Application.persistentDataPath. Writes go to a temp file
    /// and are then moved into place, so a crash mid-write cannot corrupt an existing save.
    /// </summary>
    public static class SaveSystem
    {
        static string PathFor(string fileName) => Path.Combine(Application.persistentDataPath, fileName);

        public static void Save<T>(string fileName, T data) where T : class
        {
            try
            {
                string path = PathFor(fileName);
                string tmp = path + ".tmp";
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hollowmere] Save failed for {fileName}: {e.Message}");
            }
        }

        public static T Load<T>(string fileName) where T : class
        {
            try
            {
                string path = PathFor(fileName);
                if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hollowmere] Load failed for {fileName}: {e.Message}");
                return null;
            }
        }

        public static void Delete(string fileName)
        {
            try
            {
                string path = PathFor(fileName);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hollowmere] Delete failed for {fileName}: {e.Message}");
            }
        }
    }
}
