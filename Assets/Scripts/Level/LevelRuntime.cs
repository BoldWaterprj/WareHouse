using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Warehouse.UI;

namespace Warehouse.Levels
{
    /// <summary>
    /// Plays a level exported to the "Active levels" folder.
    ///   Player -> Move + Inventory
    ///   Box    -> Pick
    ///   Shelf  -> ShelfController (holds 2 boxes, colour scored)
    /// Ends only when the player chooses "End Level"; the timer never stops play.
    /// </summary>
    public class LevelRuntime : MonoBehaviour
    {
        private LevelData _data;
        private Camera _cam;
        private Transform _player;
        private Transform _holdPoint;
        private Canvas _uiCanvas;

        private float _elapsed;
        private bool _paused;
        private bool _resultsShown;
        private Text _timerText;
        private GameObject _pausePanel;
        private GameObject _resultsPanel;

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            SetupCamera();
            _uiCanvas = UIFactory.CreateCanvas("RuntimeCanvas", 10);
            LoadSelectedLevel();
            BuildHUD();
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                GameObject camGO = new GameObject("Main Camera", typeof(Camera));
                camGO.tag = "MainCamera";
                _cam = camGO.GetComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = 7f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f, 1f);
            _cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void LoadSelectedLevel()
        {
            string path = LevelSessionState.SelectedLevelPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                string[] files = LevelPaths.ListActive();
                if (files.Length > 0)
                    path = files[0];
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("LevelRuntime: no active level to load.");
                return;
            }

            string err;
            _data = LevelIO.Load(path, out err);
            if (_data == null)
            {
                Debug.LogError("LevelRuntime: " + err);
                return;
            }

            BuildLevel();
        }

        private void BuildLevel()
        {
            Transform root = new GameObject("Level").transform;

            SpawnPlayer(_data.playerSpawn.ToVector2());

            for (int i = 0; i < _data.objects.Count; i++)
            {
                LevelObjectData o = _data.objects[i];
                if (o == null)
                    continue;
                SpawnObject(o, root);
            }
        }

        // ------------------------------------------------------------------ player

        private void SpawnPlayer(Vector2 pos)
        {
            GameObject p = PlaceableCatalog.CreateVisual(PlaceableType.PlayerSpawn, pos, 0f,
                new Vector2(1f, 1f), null);
            p.name = "Player";
            p.tag = "Player";

            Rigidbody2D rb = p.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = 0.0001f;

            PlaceableCatalog.AddCollider(p, PlaceableType.PlayerSpawn, false);

            p.AddComponent<Move>();
            p.AddComponent<Inventory>();

            GameObject hold = new GameObject("HoldPoint");
            hold.transform.SetParent(p.transform, false);
            float offsetWorld = PlaceableCatalog.Get(PlaceableType.PlayerSpawn).size.x * 0.6f;
            float ps = Mathf.Max(p.transform.lossyScale.x, 0.001f);
            hold.transform.localPosition = new Vector3(offsetWorld / ps, 0f, 0f);
            _holdPoint = hold.transform;
            _player = p.transform;
        }

        // ------------------------------------------------------------------ objects

        private void SpawnObject(LevelObjectData o, Transform root)
        {
            PlaceableType type;
            if (!PlaceableCatalog.TryParse(o.type, out type))
                return;

            PlaceableDef def = PlaceableCatalog.Get(type);
            Color? colOverride = (type == PlaceableType.Shelf || type == PlaceableType.Box)
                ? o.color : (Color?)null;

            GameObject go = PlaceableCatalog.CreateVisual(type, o.position.ToVector2(), o.rotation,
                o.scale.ToVector2(), root, colOverride);
            go.name = type + "_" + o.id;

            LevelObjectMarker marker = go.AddComponent<LevelObjectMarker>();
            marker.id = o.id;
            marker.type = type;
            marker.color = o.color;
            marker.isPickup = def.isPickup;
            marker.isDestination = def.isDestination;

            switch (type)
            {
                case PlaceableType.Box:
                    SpawnBox(go, type);
                    break;
                case PlaceableType.Shelf:
                    SpawnShelf(go, o, type);
                    break;
                case PlaceableType.Wall:
                case PlaceableType.Column:
                    PlaceableCatalog.AddCollider(go, type, false);
                    break;
                case PlaceableType.Floor:
                    break;
                case PlaceableType.PlayerSpawn:
                    Destroy(go);
                    break;
            }
        }

        private void SpawnBox(GameObject go, PlaceableType type)
        {
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.mass = 0.5f;

            PlaceableCatalog.AddCollider(go, type, false);
            PlaceableCatalog.AddCollider(go, type, true, 1.25f);

            Pick pick = go.AddComponent<Pick>();
            pick.playerHoldPoint = _holdPoint;
        }

        private void SpawnShelf(GameObject go, LevelObjectData o, PlaceableType type)
        {
            PlaceableCatalog.AddCollider(go, type, false);
            PlaceableCatalog.AddCollider(go, type, true, 1.5f);

            ShelfController shelf = go.AddComponent<ShelfController>();
            shelf.shelfColor = o.color;
        }

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !_resultsShown)
            {
                TogglePause();
                return;
            }

            if (_paused || _resultsShown)
                return;

            _elapsed += Time.deltaTime;
            UpdateTimerText();
        }

        private void UpdateTimerText()
        {
            if (_timerText == null || _data == null)
                return;

            float remaining = _data.timer - _elapsed;
            if (remaining >= 0f)
                _timerText.text = "Time: " + FormatTime(remaining);
            else
                _timerText.text = "Overtime: +" + FormatTime(-remaining);
        }

        private static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        // ------------------------------------------------------------------ pause

        private void TogglePause()
        {
            _paused = !_paused;
            if (_pausePanel != null)
                _pausePanel.SetActive(_paused);
            Time.timeScale = _paused ? 0f : 1f;
        }

        private void ResumeGame()
        {
            _paused = false;
            if (_pausePanel != null)
                _pausePanel.SetActive(false);
            Time.timeScale = 1f;
        }

        private void RestartLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("LevelRuntime");
        }

        private void GoToLevelMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("LevelSelect");
        }

        private void EndLevel()
        {
            Time.timeScale = 1f;
            _resultsShown = true;
            if (_pausePanel != null)
                _pausePanel.SetActive(false);

            LevelObjectMarker[] markers = FindObjectsByType<LevelObjectMarker>(FindObjectsSortMode.None);
            SessionRecord rec = SessionLogger.Build(_data, _elapsed, markers);
            string path, err;
            SessionLogger.Write(rec, out path, out err);

            ShowResults(rec, path);
        }

        private void ShowResults(SessionRecord rec, string path)
        {
            _resultsPanel = UIFactory.CreatePanel(_uiCanvas.transform, "ResultsPanel", new Color(0f, 0f, 0f, 0.85f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

            RectTransform box = UIFactory.CreatePanel(_resultsPanel.transform, "ResultsBox",
                new Color(0.08f, 0.09f, 0.12f, 0.99f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, -330f), new Vector2(420f, 330f));

            Text title = UIFactory.CreateText(box, "LEVEL COMPLETE", 38, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(700f, 50f));

            Text score = UIFactory.CreateText(box, "Score: " + rec.score, 52, new Color(0.6f, 0.9f, 1f, 1f),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(700f, 70f));

            string body =
                "Boxes placed: " + rec.boxesPlaced + " / " + rec.boxesTotal +
                "     No colour match: " + rec.boxesNoMatch + "\n" +
                "Box score: " + rec.boxScore + "     Time bonus: " + (rec.timeBonus >= 0 ? "+" : "") + rec.timeBonus + "\n" +
                "Time used: " + rec.timePercent.ToString("0.0") + "% of " + rec.levelTimerSeconds.ToString("0.#") + "s\n\n" +
                "Colour precision (avg)   R: " + (rec.averageRedPrecision * 100f).ToString("0.0") + "%" +
                "   G: " + (rec.averageGreenPrecision * 100f).ToString("0.0") + "%" +
                "   B: " + (rec.averageBluePrecision * 100f).ToString("0.0") + "%\n" +
                "Overall accuracy: " + (rec.averageAccuracy * 100f).ToString("0.0") + "%";

            Text info = UIFactory.CreateText(box, body, 22, Color.white, TextAnchor.UpperCenter);
            UIFactory.SetAnchored(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(760f, 220f));

            Text saved = UIFactory.CreateText(box,
                string.IsNullOrEmpty(path) ? "(session not saved)" : "Session saved: " + Path.GetFileName(path),
                16, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
            UIFactory.SetAnchored(saved.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(760f, 24f));

            Button restart = UIFactory.CreateButton(box, "Restart", RestartLevel, 54f);
            UIFactory.SetAnchored(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-160f, 24f), new Vector2(280f, 54f));

            Button menu = UIFactory.CreateButton(box, "Level Menu", GoToLevelMenu, 54f);
            UIFactory.SetAnchored(menu.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(160f, 24f), new Vector2(280f, 54f));
        }

        // ------------------------------------------------------------------ HUD

        private void BuildHUD()
        {
            string name = _data != null ? _data.levelName : "(none)";
            Text title = UIFactory.CreateText(_uiCanvas.transform, name, 30, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(700f, 50f));

            _timerText = UIFactory.CreateText(_uiCanvas.transform, "", 34, new Color(0.6f, 0.9f, 1f, 1f),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(_timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(400f, 50f));
            UpdateTimerText();

            Text hint = UIFactory.CreateText(_uiCanvas.transform,
                "WASD / arrows to move    -    E to pick up / drop on a shelf    -    Esc to pause",
                20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.SetAnchored(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(900f, 40f));

            Button pause = UIFactory.CreateButton(_uiCanvas.transform, "Pause", TogglePause, 46f);
            UIFactory.SetAnchored(pause.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(180f, 46f));

            BuildPauseMenu();
        }

        private void BuildPauseMenu()
        {
            _pausePanel = UIFactory.CreatePanel(_uiCanvas.transform, "PausePanel", new Color(0f, 0f, 0f, 0.72f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

            RectTransform box = UIFactory.CreatePanel(_pausePanel.transform, "PauseBox",
                new Color(0.08f, 0.09f, 0.12f, 0.99f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280f, -230f), new Vector2(280f, 230f));

            Text title = UIFactory.CreateText(box, "PAUSED", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(500f, 56f));

            Button cont = UIFactory.CreateButton(box, "Continue", ResumeGame, 54f);
            UIFactory.SetAnchored(cont.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(420f, 54f));

            Button restart = UIFactory.CreateButton(box, "Restart", RestartLevel, 54f);
            UIFactory.SetAnchored(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(420f, 54f));

            Button end = UIFactory.CreateButton(box, "End Level", EndLevel, 54f);
            UIFactory.SetAnchored(end.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(420f, 54f));

            Button menu = UIFactory.CreateButton(box, "Level Menu", GoToLevelMenu, 54f);
            UIFactory.SetAnchored(menu.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(420f, 54f));

            _pausePanel.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_cam == null || _player == null || _paused || _resultsShown)
                return;
            Vector3 target = new Vector3(_player.position.x, _player.position.y, -10f);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, target, 8f * Time.deltaTime);
        }
    }
}
