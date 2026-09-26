using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Warehouse.Levels
{
    [Serializable]
    public class SessionBoxEntry
    {
        public string id;
        public string boxColor;
        public string shelfColor;
        public bool placed;
        public bool matched;
        public float accuracy;        // 0..1 overall closeness (0 when farther than MaxDistance)
        public float redPrecision;    // 0..1 per channel
        public float greenPrecision;
        public float bluePrecision;
    }

    [Serializable]
    public class SessionRecord
    {
        public string levelName;
        public string author;
        public string playerName;
        public string finishedUtc;

        public float levelTimerSeconds;
        public float elapsedSeconds;
        public float timePercent;     // % of the level time the player needed (100 = full timer)

        public int score;
        public int boxScore;
        public int timeBonus;         // +remaining seconds, or -overtime seconds

        public int boxesTotal;
        public int boxesPlaced;
        public int boxesNoMatch;

        public float averageAccuracy;
        public float averageRedPrecision;
        public float averageGreenPrecision;
        public float averageBluePrecision;

        public List<SessionBoxEntry> boxes = new List<SessionBoxEntry>();
    }

    /// <summary>Builds and writes the per-session report an instructor can read.</summary>
    public static class SessionLogger
    {
        public static SessionRecord Build(LevelData level, float elapsed, IEnumerable<LevelObjectMarker> markers)
        {
            SessionRecord rec = new SessionRecord();
            rec.levelName = level != null ? level.levelName : "Unknown";
            rec.author = level != null ? level.author : "";
            rec.playerName = WorkerProfile.LoadName();
            rec.finishedUtc = DateTime.UtcNow.ToString("o");
            rec.levelTimerSeconds = level != null ? level.timer : 0f;
            rec.elapsedSeconds = elapsed;
            rec.timePercent = (level != null && level.timer > 0f) ? (elapsed / level.timer) * 100f : 0f;

            float sumAcc = 0f, sumR = 0f, sumG = 0f, sumB = 0f;
            int total = 0, placed = 0, noMatch = 0;
            int score = 0;

            if (markers != null)
            {
                foreach (LevelObjectMarker m in markers)
                {
                    if (m == null || !m.isPickup)
                        continue;

                    total++;
                    SessionBoxEntry e = new SessionBoxEntry();
                    e.id = m.id;
                    e.boxColor = ColorUtility.ToHtmlStringRGB(m.color);
                    e.placed = m.placed;

                    if (m.placed)
                    {
                        placed++;
                        e.shelfColor = ColorUtility.ToHtmlStringRGB(m.shelfColor);

                        e.accuracy = ColorScore.Closeness(m.color, m.shelfColor);
                        e.matched = e.accuracy > 0f;
                        if (!e.matched)
                            noMatch++;

                        float r, g, b;
                        ColorScore.Precision(m.color, m.shelfColor, out r, out g, out b);
                        e.redPrecision = r;
                        e.greenPrecision = g;
                        e.bluePrecision = b;

                        score += Mathf.RoundToInt(100f * e.accuracy);
                    }
                    else
                    {
                        e.shelfColor = "";
                        e.matched = false;
                        e.accuracy = 0f;
                    }

                    sumAcc += e.accuracy;
                    sumR += e.redPrecision;
                    sumG += e.greenPrecision;
                    sumB += e.bluePrecision;
                    rec.boxes.Add(e);
                }
            }

            rec.boxScore = score;

            int timeBonus = 0;
            if (level != null && level.timer > 0f)
                timeBonus = Mathf.FloorToInt(level.timer - elapsed); // +remaining / -overtime

            rec.timeBonus = timeBonus;
            rec.score = score + timeBonus;

            rec.boxesTotal = total;
            rec.boxesPlaced = placed;
            rec.boxesNoMatch = noMatch;

            if (total > 0)
            {
                rec.averageAccuracy = sumAcc / total;
                rec.averageRedPrecision = sumR / total;
                rec.averageGreenPrecision = sumG / total;
                rec.averageBluePrecision = sumB / total;
            }

            return rec;
        }

        public static bool Write(SessionRecord rec, out string path, out string error)
        {
            error = null;
            path = "";
            try
            {
                LevelPaths.EnsureFolders();
                string safe = LevelPaths.SanitizeFileName(rec.levelName);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                path = Path.Combine(LevelPaths.SessionsDir, "session_" + stamp + "_" + safe + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(rec, true));
                Debug.Log("Session saved: " + path);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError("SessionLogger.Write failed: " + e);
                return false;
            }
        }
    }
}
