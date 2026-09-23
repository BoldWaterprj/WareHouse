using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>Saving / loading / listing level JSON files.</summary>
    public static class LevelIO
    {
        public static bool Save(LevelData data, string path, out string error)
        {
            error = null;
            try
            {
                if (data == null)
                {
                    error = "No level data.";
                    return false;
                }

                data.createdUtc = DateTime.UtcNow.ToString("o");

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError("LevelIO.Save failed: " + e);
                return false;
            }
        }

        public static LevelData Load(string path, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    error = "File not found.";
                    return null;
                }

                string json = File.ReadAllText(path);
                LevelData data = JsonUtility.FromJson<LevelData>(json);
                if (data == null)
                {
                    error = "Could not parse level JSON.";
                    return null;
                }

                if (data.objects == null)
                    data.objects = new List<LevelObjectData>();
                if (data.playerSpawn == null)
                    data.playerSpawn = new Vec2();

                // Repair objects that were hand-edited.
                for (int i = 0; i < data.objects.Count; i++)
                {
                    LevelObjectData o = data.objects[i];
                    if (o == null)
                    {
                        data.objects[i] = new LevelObjectData();
                        o = data.objects[i];
                    }
                    if (o.position == null) o.position = new Vec2();
                    if (o.scale == null) o.scale = new Vec2(1f, 1f);
                    if (string.IsNullOrEmpty(o.id)) o.id = Guid.NewGuid().ToString("N").Substring(0, 8);
                    if (string.IsNullOrEmpty(o.type)) o.type = "Floor";
                }

                if (string.IsNullOrEmpty(data.levelName))
                    data.levelName = Path.GetFileNameWithoutExtension(path);

                return data;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError("LevelIO.Load failed: " + e);
                return null;
            }
        }
    }
}
