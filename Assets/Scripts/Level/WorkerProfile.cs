using System;
using System.IO;
using UnityEngine;

namespace Warehouse.Levels
{
    [Serializable]
    public class WorkerData
    {
        public string name = "Worker";
    }

    /// <summary>
    /// Reads the player's name from &lt;root&gt;/Worker/worker.json.
    /// If the file does not exist it is created with a default name.
    /// </summary>
    public static class WorkerProfile
    {
        private static string _cached;

        public static string LoadName()
        {
            if (!string.IsNullOrEmpty(_cached))
                return _cached;

            LevelPaths.EnsureFolders();
            string path = LevelPaths.WorkerFile;

            try
            {
                if (File.Exists(path))
                {
                    WorkerData d = JsonUtility.FromJson<WorkerData>(File.ReadAllText(path));
                    if (d != null && !string.IsNullOrWhiteSpace(d.name))
                    {
                        _cached = d.name.Trim();
                        return _cached;
                    }
                }

                WorkerData fresh = new WorkerData();
                File.WriteAllText(path, JsonUtility.ToJson(fresh, true));
                _cached = fresh.name;
                return _cached;
            }
            catch (Exception e)
            {
                Debug.LogWarning("WorkerProfile.LoadName: " + e.Message);
                _cached = "Worker";
                return _cached;
            }
        }

        public static string Path { get { return LevelPaths.WorkerFile; } }
    }
}
