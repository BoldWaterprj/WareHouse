using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
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

                File.WriteAllText(path, ToJson(data));
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

        /// <summary>
        /// Hand-written writer so that fields that are not relevant for an object
        /// (e.g. destinationId on walls) are not written at all.
        /// JsonUtility is still used for reading (it ignores missing fields).
        /// </summary>
        private static string ToJson(LevelData d)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"formatVersion\": ").Append(d.formatVersion).Append(",\n");
            sb.Append("  \"levelName\": \"").Append(Escape(d.levelName)).Append("\",\n");
            sb.Append("  \"author\": \"").Append(Escape(d.author)).Append("\",\n");
            sb.Append("  \"createdUtc\": \"").Append(Escape(d.createdUtc)).Append("\",\n");

            Vec2 spawn = d.playerSpawn ?? new Vec2();
            sb.Append("  \"playerSpawn\": { \"x\": ").Append(Num(spawn.x))
              .Append(", \"y\": ").Append(Num(spawn.y)).Append(" },\n");

            sb.Append("  \"objects\": [");
            bool first = true;
            if (d.objects != null)
            {
                for (int i = 0; i < d.objects.Count; i++)
                {
                    LevelObjectData o = d.objects[i];
                    if (o == null)
                        continue;

                    if (!first)
                        sb.Append(",");
                    first = false;

                    Vec2 p = o.position ?? new Vec2();
                    Vec2 s = o.scale ?? new Vec2(1f, 1f);

                    sb.Append("\n    {");
                    sb.Append("\"type\": \"").Append(Escape(o.type)).Append("\", ");
                    sb.Append("\"id\": \"").Append(Escape(o.id)).Append("\"");
                    if (!string.IsNullOrEmpty(o.destinationId))
                        sb.Append(", \"destinationId\": \"").Append(Escape(o.destinationId)).Append("\"");
                    sb.Append(", \"position\": { \"x\": ").Append(Num(p.x))
                      .Append(", \"y\": ").Append(Num(p.y)).Append(" }");
                    sb.Append(", \"rotation\": ").Append(Num(o.rotation));
                    sb.Append(", \"scale\": { \"x\": ").Append(Num(s.x))
                      .Append(", \"y\": ").Append(Num(s.y)).Append(" }");
                    sb.Append("}");
                }
            }
            if (!first)
                sb.Append("\n  ");
            sb.Append("]\n}\n");
            return sb.ToString();
        }

        private static string Num(float v)
        {
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            StringBuilder sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
