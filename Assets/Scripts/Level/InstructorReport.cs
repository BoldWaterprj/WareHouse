using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Warehouse.Levels
{
    public class InstructorGroup
    {
        public string levelName;
        public List<SessionRecord> sessions = new List<SessionRecord>();
        public float avgAccuracy;
        public float avgRed;
        public float avgGreen;
        public float avgBlue;
        public float avgTimePercent;
    }

    public class InstructorReport
    {
        public int found;
        public int kept;
        public int deleted;
        public float avgAccuracy;
        public float avgRed;
        public float avgGreen;
        public float avgBlue;
        public float avgTimePercent;
        public List<InstructorGroup> groups = new List<InstructorGroup>();
    }

    /// <summary>
    /// Reads the session files, keeps the 10 newest (deletes older ones) and
    /// aggregates them overall and per level for the instructor screen.
    /// </summary>
    public static class InstructorReportBuilder
    {
        public const int KeepCount = 10;

        public static InstructorReport Build()
        {
            InstructorReport report = new InstructorReport();
            LevelPaths.EnsureFolders();

            string[] files = LevelPaths.ListSessions();
            List<KeyValuePair<string, SessionRecord>> loaded = new List<KeyValuePair<string, SessionRecord>>();

            for (int i = 0; i < files.Length; i++)
            {
                string err;
                SessionRecord rec = LoadRecord(files[i], out err);
                if (rec != null)
                    loaded.Add(new KeyValuePair<string, SessionRecord>(files[i], rec));
                else
                    Debug.LogWarning("Instructor: skipped " + files[i] + " (" + err + ")");
            }

            report.found = loaded.Count;

            // newest first
            loaded.Sort((a, b) => string.CompareOrdinal(SortKey(b.Value, b.Key), SortKey(a.Value, a.Key)));

            // keep the 10 newest, delete everything older
            for (int i = KeepCount; i < loaded.Count; i++)
            {
                try
                {
                    File.Delete(loaded[i].Key);
                    report.deleted++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Instructor: could not delete " + loaded[i].Key + " (" + e.Message + ")");
                }
            }

            List<SessionRecord> kept = new List<SessionRecord>();
            int keep = Mathf.Min(KeepCount, loaded.Count);
            for (int i = 0; i < keep; i++)
                kept.Add(loaded[i].Value);

            report.kept = kept.Count;
            report.avgAccuracy = Avg(kept, r => r.averageAccuracy);
            report.avgRed = Avg(kept, r => r.averageRedPrecision);
            report.avgGreen = Avg(kept, r => r.averageGreenPrecision);
            report.avgBlue = Avg(kept, r => r.averageBluePrecision);
            report.avgTimePercent = Avg(kept, r => r.timePercent);

            Dictionary<string, InstructorGroup> map = new Dictionary<string, InstructorGroup>();
            for (int i = 0; i < kept.Count; i++)
            {
                SessionRecord r = kept[i];
                string key = string.IsNullOrEmpty(r.levelName) ? "(unnamed)" : r.levelName;
                InstructorGroup g;
                if (!map.TryGetValue(key, out g))
                {
                    g = new InstructorGroup();
                    g.levelName = key;
                    map[key] = g;
                }
                g.sessions.Add(r);
            }

            foreach (InstructorGroup g in map.Values)
            {
                // graphs read left (oldest) -> right (newest)
                g.sessions.Sort((a, b) => string.CompareOrdinal(SortKey(a, ""), SortKey(b, "")));
                g.avgAccuracy = Avg(g.sessions, r => r.averageAccuracy);
                g.avgRed = Avg(g.sessions, r => r.averageRedPrecision);
                g.avgGreen = Avg(g.sessions, r => r.averageGreenPrecision);
                g.avgBlue = Avg(g.sessions, r => r.averageBluePrecision);
                g.avgTimePercent = Avg(g.sessions, r => r.timePercent);
                report.groups.Add(g);
            }

            report.groups.Sort((a, b) => string.CompareOrdinal(a.levelName, b.levelName));
            return report;
        }

        private static string SortKey(SessionRecord r, string path)
        {
            if (r != null && !string.IsNullOrEmpty(r.finishedUtc))
                return r.finishedUtc;
            return path;
        }

        private static float Avg(List<SessionRecord> list, Func<SessionRecord, float> selector)
        {
            if (list == null || list.Count == 0)
                return 0f;
            float sum = 0f;
            for (int i = 0; i < list.Count; i++)
                sum += selector(list[i]);
            return sum / list.Count;
        }

        private static SessionRecord LoadRecord(string path, out string error)
        {
            error = null;
            try
            {
                SessionRecord r = JsonUtility.FromJson<SessionRecord>(File.ReadAllText(path));
                if (r == null)
                {
                    error = "parse failed";
                    return null;
                }
                if (r.boxes == null)
                    r.boxes = new List<SessionBoxEntry>();
                return r;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }
    }
}
