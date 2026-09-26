using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Warehouse.UI;

namespace Warehouse.Levels
{
    /// <summary>
    /// Instructor screen: compiles the last 10 sessions, shows average total and
    /// per-spectrum precision and average time, and graphs every attempt.
    /// Data is grouped per level.
    /// </summary>
    public class InstructorController : MonoBehaviour
    {
        private const float ChartHeight = 170f;

        private RectTransform _content;
        private Text _header;

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            BuildUI();
            Refresh();
        }

        private void BuildUI()
        {
            Canvas canvas = UIFactory.CreateCanvas("InstructorCanvas", 10);

            UIFactory.CreatePanel(canvas.transform, "BG", new Color(0.07f, 0.08f, 0.11f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text title = UIFactory.CreateText(canvas.transform, "INSTRUCTOR", 56, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(900f, 64f));

            _header = UIFactory.CreateText(canvas.transform, "", 26, new Color(0.8f, 0.85f, 0.95f, 1f),
                TextAnchor.MiddleCenter);
            UIFactory.SetAnchored(_header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(1800f, 34f));

            UIFactory.CreateScrollView(canvas.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(-900f, 96f), new Vector2(900f, -152f), out _content, 30);

            Button back = UIFactory.CreateButton(canvas.transform, "Back", () => SceneManager.LoadScene("MainMenu"), 50f);
            UIFactory.SetAnchored(back.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(220f, 50f));

            Button refresh = UIFactory.CreateButton(canvas.transform, "Refresh", Refresh, 50f);
            UIFactory.SetAnchored(refresh.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(220f, 50f));
        }

        private void Refresh()
        {
            foreach (Transform child in _content)
                Destroy(child.gameObject);

            InstructorReport report = InstructorReportBuilder.Build();

            _header.text = "Player: " + WorkerProfile.LoadName() +
                           "     Sessions kept: " + report.kept + " / " + report.found +
                           (report.deleted > 0 ? "   (" + report.deleted + " older deleted)" : "");

            if (report.kept == 0)
            {
                AddLabel("No sessions recorded yet.\nPlay a level and press End Level.", 30, TextAnchor.MiddleCenter, 100f);
                return;
            }

            AddLabel("OVERALL", 34, TextAnchor.MiddleLeft, 48f);
            AddSummary(report.avgAccuracy, report.avgRed, report.avgGreen, report.avgBlue, report.avgTimePercent, 40f);

            for (int i = 0; i < report.groups.Count; i++)
                AddGroup(report.groups[i]);
        }

        private void AddGroup(InstructorGroup g)
        {
            AddSpacer(20f);
            AddLabel("LEVEL: " + g.levelName + "     (" + g.sessions.Count + " session" +
                     (g.sessions.Count == 1 ? "" : "s") + ")", 34, TextAnchor.MiddleLeft, 48f);
            AddSummary(g.avgAccuracy, g.avgRed, g.avgGreen, g.avgBlue, g.avgTimePercent, 40f);

            List<float> accuracy = new List<float>();
            List<float> time = new List<float>();
            List<float> r = new List<float>();
            List<float> gr = new List<float>();
            List<float> b = new List<float>();

            for (int i = 0; i < g.sessions.Count; i++)
            {
                SessionRecord s = g.sessions[i];
                accuracy.Add(s.averageAccuracy);
                time.Add(s.timePercent);
                r.Add(s.averageRedPrecision);
                gr.Add(s.averageGreenPrecision);
                b.Add(s.averageBluePrecision);
            }

            CreateBarChart("Colour accuracy per attempt",
                accuracy, 1f, new Color(0.35f, 0.75f, 1f, 0.95f), v => (v * 100f).ToString("0") + "%");

            float timeMax = 100f;
            for (int i = 0; i < time.Count; i++)
                timeMax = Mathf.Max(timeMax, time[i]);
            CreateBarChart("Time used per attempt (% of level time)",
                time, timeMax, new Color(1f, 0.65f, 0.25f, 0.95f), v => v.ToString("0") + "%");

            CreateGroupedChart("R / G / B precision per attempt  (red / green / blue)", r, gr, b, 1f);
        }

        private void AddSummary(float acc, float r, float g, float b, float time, float height)
        {
            AddLabel("Average accuracy: " + (acc * 100f).ToString("0.0") + "%" +
                     "     R: " + (r * 100f).ToString("0.0") + "%" +
                     "     G: " + (g * 100f).ToString("0.0") + "%" +
                     "     B: " + (b * 100f).ToString("0.0") + "%" +
                     "     Average time: " + time.ToString("0.0") + "%",
                26, TextAnchor.MiddleLeft, height);
        }

        // ------------------------------------------------------------------ chart helpers

        private RectTransform NewChartContainer(string title)
        {
            GameObject go = new GameObject("Chart", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(_content, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            go.GetComponent<LayoutElement>().preferredHeight = ChartHeight + 46f;

            Text t = UIFactory.CreateText(rt, title, 25, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.SetAnchored(t.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(-16f, 36f));
            return rt;
        }

        private void CreateBarChart(string title, List<float> values, float max, Color color, Func<float, string> fmt)
        {
            float avg = Avg(values);
            RectTransform chart = NewChartContainer(title + "     (avg " + fmt(avg) + ")");

            RectTransform area = Solid(chart, "Area", new Color(1f, 1f, 1f, 0.06f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 28f), new Vector2(-14f, -44f));
            RectTransform labels = Solid(chart, "Labels", new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 4f), new Vector2(-14f, 26f));

            if (values.Count == 0)
                return;

            float safeMax = Mathf.Max(0.0001f, max);
            int n = values.Count;

            for (int i = 0; i < n; i++)
            {
                float fmin = (i + 0.25f) / n;
                float fmax = (i + 0.75f) / n;
                float h = Mathf.Clamp01(values[i] / safeMax);
                Solid(area, "Bar" + i, color, new Vector2(fmin, 0f), new Vector2(fmax, h), Vector2.zero, Vector2.zero);
                AddAttemptLabel(labels, i, n);
            }

            float af = Mathf.Clamp01(avg / safeMax);
            Solid(area, "AvgLine", new Color(1f, 1f, 1f, 0.7f),
                new Vector2(0f, af), new Vector2(1f, af), new Vector2(0f, -1f), new Vector2(0f, 1f));
        }

        private void CreateGroupedChart(string title, List<float> r, List<float> g, List<float> b, float max)
        {
            RectTransform chart = NewChartContainer(title);

            RectTransform area = Solid(chart, "Area", new Color(1f, 1f, 1f, 0.06f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 28f), new Vector2(-14f, -44f));
            RectTransform labels = Solid(chart, "Labels", new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 4f), new Vector2(-14f, 26f));

            int n = r.Count;
            if (n == 0)
                return;

            float safeMax = Mathf.Max(0.0001f, max);
            Color[] cols =
            {
                new Color(0.90f, 0.35f, 0.35f, 0.95f),
                new Color(0.35f, 0.85f, 0.40f, 0.95f),
                new Color(0.40f, 0.55f, 0.95f, 0.95f)
            };

            for (int i = 0; i < n; i++)
            {
                float slot = 1f / n;
                float pad = slot * 0.12f;
                float inner = slot - 2f * pad;
                float bw = inner / 3f;
                float[] vals = { r[i], g[i], b[i] };

                for (int j = 0; j < 3; j++)
                {
                    float x0 = i * slot + pad + j * bw;
                    float x1 = x0 + bw * 0.82f;
                    float h = Mathf.Clamp01(vals[j] / safeMax);
                    Solid(area, "Bar" + i + "_" + j, cols[j],
                        new Vector2(x0, 0f), new Vector2(x1, h), Vector2.zero, Vector2.zero);
                }

                AddAttemptLabel(labels, i, n);
            }
        }

        private void AddAttemptLabel(RectTransform labels, int i, int n)
        {
            Text t = UIFactory.CreateText(labels, (i + 1).ToString(), 17, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
            UIFactory.SetAnchored(t.rectTransform, new Vector2((float)i / n, 0f), new Vector2((float)(i + 1) / n, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private RectTransform Solid(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rt = UIFactory.CreatePanel(parent, name, color, anchorMin, anchorMax, offsetMin, offsetMax);
            Image img = rt.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = false;
            return rt;
        }

        private void AddLabel(string text, int size, TextAnchor anchor, float height)
        {
            Text t = UIFactory.CreateText(_content, text, size, Color.white, anchor,
                size >= 30 ? FontStyle.Bold : FontStyle.Normal);
            LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        private void AddSpacer(float height)
        {
            GameObject go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(_content, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private static float Avg(List<float> values)
        {
            if (values == null || values.Count == 0)
                return 0f;
            float sum = 0f;
            for (int i = 0; i < values.Count; i++)
                sum += values[i];
            return sum / values.Count;
        }
    }
}
