using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Warehouse.UI;

namespace Warehouse.Levels
{
    /// <summary>Lists exported ("Active levels") levels and lets the player pick one.</summary>
    public class LevelSelectUI : MonoBehaviour
    {
        private RectTransform _listContent;

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            LevelPaths.EnsureFolders();
            BuildUI();
            Refresh();
        }

        private void BuildUI()
        {
            Canvas canvas = UIFactory.CreateCanvas("LevelSelectCanvas", 10);

            UIFactory.CreatePanel(canvas.transform, "BG", new Color(0.07f, 0.08f, 0.11f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text title = UIFactory.CreateText(canvas.transform, "CHOOSE A LEVEL", 46, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 70f));

            UIFactory.CreateScrollView(canvas.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(-450f, 110f), new Vector2(450f, -130f), out _listContent);

            Button back = UIFactory.CreateButton(canvas.transform, "Back", () => SceneManager.LoadScene("MainMenu"), 50f);
            UIFactory.SetAnchored(back.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(220f, 50f));

            Button refresh = UIFactory.CreateButton(canvas.transform, "Refresh", Refresh, 50f);
            UIFactory.SetAnchored(refresh.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(220f, 50f));
        }

        private void Refresh()
        {
            foreach (Transform child in _listContent)
                Destroy(child.gameObject);

            string[] files = LevelPaths.ListActive();
            if (files.Length == 0)
            {
                Text empty = UIFactory.CreateText(_listContent,
                    "No active levels.\nOpen the Level Editor, build a level and press \"Export Playable\".",
                    22, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
                LayoutElement le = empty.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 80f;
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string captured = files[i];
                string label = Path.GetFileNameWithoutExtension(captured);
                UIFactory.CreateButton(_listContent, label, () => Play(captured), 60f);
            }
        }

        private void Play(string path)
        {
            LevelSessionState.SelectedLevelPath = path;
            LevelSessionState.SelectedLevelName = Path.GetFileNameWithoutExtension(path);
            SceneManager.LoadScene("LevelRuntime");
        }
    }
}
