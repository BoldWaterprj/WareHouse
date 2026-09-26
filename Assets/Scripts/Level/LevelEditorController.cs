using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Warehouse.UI;

namespace Warehouse.Levels
{
    /// <summary>
    /// Runtime level editor. Its UI is built in code so the scene stays tiny.
    ///
    /// Controls (place tool):
    ///   Left click / drag   place objects; dragging builds a continuous line
    ///   Right click         delete object under the cursor
    /// Controls (Hand):
    ///   Left click / drag   move objects and edit their colour
    /// Always:
    ///   Middle drag / wheel  pan / zoom
    ///   Delete               delete selected object
    ///   Escape               pause menu
    /// </summary>
    public class LevelEditorController : MonoBehaviour
    {
        private static readonly Color[] Presets =
        {
            new Color(0.85f, 0.20f, 0.20f),
            new Color(0.95f, 0.55f, 0.15f),
            new Color(0.90f, 0.85f, 0.20f),
            new Color(0.25f, 0.75f, 0.30f),
            new Color(0.20f, 0.50f, 0.90f),
            new Color(0.60f, 0.30f, 0.85f),
        };

        private LevelData _data;
        private readonly Dictionary<string, GameObject> _spawned = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, LevelObjectData> _byId = new Dictionary<string, LevelObjectData>();
        private GameObject _spawnMarker;

        private PlaceableType _tool = PlaceableType.Floor;
        private bool _handMode;
        private bool _snap = true;
        private LevelObjectMarker _dragging;
        private LevelObjectMarker _selected;
        private bool _painting;
        private Vector2 _lastPlaced;

        private Camera _cam;
        private Transform _levelRoot;
        private bool _panning;
        private Vector3 _prevMouse;

        private Text _status;
        private InputField _nameField;
        private InputField _authorField;
        private InputField _timerField;
        private GameObject _colorSection;
        private Image _colorSwatch;
        private Slider _redSlider;
        private Slider _greenSlider;
        private Slider _blueSlider;
        private Color _currentColor = new Color(0.85f, 0.20f, 0.20f, 1f);
        private bool _suppressColorEvent;

        private GameObject _loadPanel;
        private RectTransform _loadListContent;
        private GameObject _pausePanel;
        private GameObject _pauseMain;
        private GameObject _pauseConfirm;
        private bool _paused;
        private Button _snapButton;

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            SetupCamera();

            _levelRoot = new GameObject("Level").transform;

            _data = new LevelData();
            _data.levelName = "Untitled";
            _data.timer = 120f;

            BuildUI();
            SetCurrentColor(_currentColor, false);
            UpdateColorVisibility();
            UpdateStatus();
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
            _cam.orthographicSize = 8f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f, 1f);
            _cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !TypingInField())
            {
                if (_pauseConfirm.activeSelf) { ShowPauseMain(); return; }
                if (_loadPanel.activeSelf) { CloseLoadPanel(); return; }
                TogglePause();
                return;
            }

            if (_paused)
                return;

            HandleCamera();
            HandleTools();
            HandleKeys();
        }

        // ------------------------------------------------------------------ input

        private void HandleCamera()
        {
            if (!PointerOverUI() && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                _cam.orthographicSize = Mathf.Clamp(
                    _cam.orthographicSize - Input.mouseScrollDelta.y * 0.8f, 2f, 40f);
            }

            if (Input.GetMouseButtonDown(2))
            {
                _panning = true;
                _prevMouse = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(2))
                _panning = false;

            if (_panning)
            {
                Vector3 prevWorld = _cam.ScreenToWorldPoint(_prevMouse);
                Vector3 curWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = prevWorld - curWorld;
                _cam.transform.position += new Vector3(delta.x, delta.y, 0f);
            }

            _prevMouse = Input.mousePosition;
        }

        private void HandleTools()
        {
            if (PointerOverUI())
                return;

            Vector2 world = _cam.ScreenToWorldPoint(Input.mousePosition);

            if (Input.GetMouseButtonDown(1))
            {
                LevelObjectMarker hit = PickTop(world);
                if (hit != null)
                    Remove(hit);
                return;
            }

            if (_handMode)
                HandleHand(world);
            else
                HandlePlace(world);
        }

        private void HandleHand(Vector2 world)
        {
            if (Input.GetMouseButtonDown(0))
            {
                LevelObjectMarker hit = PickTop(world);
                if (hit != null)
                {
                    _dragging = hit;
                    Select(hit);
                }
            }

            if (_dragging != null && Input.GetMouseButton(0))
            {
                PlaceableType t = _dragging.isPlayerSpawn ? PlaceableType.PlayerSpawn : _dragging.type;
                Vector2 p = SnapFor(t, world);
                _dragging.transform.position = new Vector3(p.x, p.y, 0f);
            }

            if (_dragging != null && Input.GetMouseButtonUp(0))
            {
                CommitDrag(_dragging);
                _dragging = null;
            }
        }

        private void HandlePlace(Vector2 world)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (_tool == PlaceableType.PlayerSpawn)
                {
                    SetPlayerSpawn(SnapFor(PlaceableType.PlayerSpawn, world));
                    return;
                }

                Vector2 p = SnapFor(_tool, world);
                if (_snap)
                    PaintLine(ToCell(_tool, p), ToCell(_tool, p));
                else
                    TryPlace(p);

                _lastPlaced = p;
                _painting = true;
            }

            if (_painting && Input.GetMouseButton(0) && _tool != PlaceableType.PlayerSpawn)
            {
                Vector2 p = SnapFor(_tool, world);
                Vector2 size = PlaceableCatalog.Get(_tool).size;
                float minStep = Mathf.Max(0.05f, Mathf.Min(size.x, size.y) * 0.5f);

                if (Vector2.Distance(p, _lastPlaced) >= minStep)
                {
                    if (_snap)
                        PaintLine(ToCell(_tool, _lastPlaced), ToCell(_tool, p));
                    else
                        TryPlace(p);
                    _lastPlaced = p;
                }
            }

            if (Input.GetMouseButtonUp(0))
                _painting = false;
        }

        private void HandleKeys()
        {
            if (TypingInField())
                return;
            if (Input.GetKeyDown(KeyCode.Delete) && _selected != null)
                Remove(_selected);
        }

        // ------------------------------------------------------------------ snapping / cells

        private Vector2 SnapFor(PlaceableType type, Vector2 p)
        {
            if (!_snap)
                return p;

            Vector2 step = PlaceableCatalog.Get(type).size;
            if (step.x <= 0.001f) step.x = 1f;
            if (step.y <= 0.001f) step.y = 1f;
            return new Vector2(Mathf.Round(p.x / step.x) * step.x, Mathf.Round(p.y / step.y) * step.y);
        }

        private Vector2Int ToCell(PlaceableType type, Vector2 p)
        {
            Vector2 step = PlaceableCatalog.Get(type).size;
            float sx = Mathf.Max(step.x, 0.001f);
            float sy = Mathf.Max(step.y, 0.001f);
            return new Vector2Int(Mathf.RoundToInt(p.x / sx), Mathf.RoundToInt(p.y / sy));
        }

        private Vector2 CellToPos(PlaceableType type, Vector2Int cell)
        {
            Vector2 step = PlaceableCatalog.Get(type).size;
            return new Vector2(cell.x * step.x, cell.y * step.y);
        }

        private void PaintLine(Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(to.x - from.x);
            int dy = -Mathf.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int err = dx + dy;
            Vector2Int cur = from;

            while (true)
            {
                PlaceAtCell(cur);
                if (cur == to)
                    break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; cur.x += sx; }
                if (e2 <= dx) { err += dx; cur.y += sy; }
            }
        }

        private void PlaceAtCell(Vector2Int cell)
        {
            TryPlace(CellToPos(_tool, cell));
        }

        private void TryPlace(Vector2 pos)
        {
            if (OverlapsExisting(_tool, pos))
                return;

            Color c = (_tool == PlaceableType.Shelf || _tool == PlaceableType.Box) ? _currentColor : Color.white;
            PlaceObjectAt(_tool, pos, c);
        }

        private bool OverlapsExisting(PlaceableType type, Vector2 pos)
        {
            if (type == PlaceableType.Floor)
                return false;

            PlaceableDef def = PlaceableCatalog.Get(type);
            Rect r = new Rect(pos.x - def.size.x * 0.5f, pos.y - def.size.y * 0.5f, def.size.x, def.size.y);

            for (int i = 0; i < _data.objects.Count; i++)
            {
                LevelObjectData o = _data.objects[i];
                if (o == null)
                    continue;

                PlaceableType t;
                if (!PlaceableCatalog.TryParse(o.type, out t) || t == PlaceableType.Floor)
                    continue;

                PlaceableDef od = PlaceableCatalog.Get(t);
                Vector2 op = o.position.ToVector2();
                Rect or = new Rect(op.x - od.size.x * 0.5f, op.y - od.size.y * 0.5f, od.size.x, od.size.y);
                if (r.Overlaps(or))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ editing

        private void PlaceObjectAt(PlaceableType type, Vector2 pos, Color color)
        {
            LevelObjectData obj = new LevelObjectData
            {
                type = type.ToString(),
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                color = color,
                position = Vec2.From(pos),
                scale = new Vec2(1f, 1f)
            };

            _data.objects.Add(obj);
            _byId[obj.id] = obj;
            SpawnEditor(obj);
            UpdateStatus();
        }

        private GameObject SpawnEditor(LevelObjectData obj)
        {
            PlaceableType type;
            if (!PlaceableCatalog.TryParse(obj.type, out type))
                type = PlaceableType.Floor;

            PlaceableDef def = PlaceableCatalog.Get(type);
            Color? colOverride = (type == PlaceableType.Shelf || type == PlaceableType.Box)
                ? obj.color : (Color?)null;

            GameObject go = PlaceableCatalog.CreateVisual(type, obj.position.ToVector2(), obj.rotation,
                obj.scale.ToVector2(), _levelRoot, colOverride);
            go.name = type + "_" + obj.id;

            LevelObjectMarker marker = go.AddComponent<LevelObjectMarker>();
            marker.id = obj.id;
            marker.type = type;
            marker.color = obj.color;
            marker.isPickup = def.isPickup;
            marker.isDestination = def.isDestination;
            marker.isPlayerSpawn = type == PlaceableType.PlayerSpawn;

            PlaceableCatalog.AddCollider(go, type, true);

            _spawned[obj.id] = go;
            return go;
        }

        private void SetPlayerSpawn(Vector2 pos)
        {
            _data.playerSpawn = Vec2.From(pos);

            if (_spawnMarker == null)
            {
                _spawnMarker = PlaceableCatalog.CreateVisual(PlaceableType.PlayerSpawn, pos, 0f,
                    new Vector2(1f, 1f), _levelRoot);
                _spawnMarker.name = "PlayerSpawn";
                LevelObjectMarker m = _spawnMarker.AddComponent<LevelObjectMarker>();
                m.id = "player_spawn";
                m.type = PlaceableType.PlayerSpawn;
                m.isPlayerSpawn = true;
                PlaceableCatalog.AddCollider(_spawnMarker, PlaceableType.PlayerSpawn, true);
            }
            else
            {
                _spawnMarker.transform.position = new Vector3(pos.x, pos.y, 0f);
            }

            UpdateStatus();
        }

        private void CommitDrag(LevelObjectMarker m)
        {
            Vector2 p = m.transform.position;
            if (m.isPlayerSpawn)
            {
                _data.playerSpawn = Vec2.From(p);
            }
            else
            {
                LevelObjectData d;
                if (_byId.TryGetValue(m.id, out d))
                    d.position = Vec2.From(p);
            }
            UpdateStatus();
        }

        private void Remove(LevelObjectMarker m)
        {
            if (m.isPlayerSpawn)
            {
                _data.playerSpawn = new Vec2(0f, 0f);
                if (_spawnMarker != null)
                    Destroy(_spawnMarker);
                _spawnMarker = null;
            }
            else
            {
                _data.objects.RemoveAll(o => o.id == m.id);
                _byId.Remove(m.id);
                _spawned.Remove(m.id);
                Destroy(m.gameObject);
            }

            if (_selected == m)
                _selected = null;
            UpdateColorVisibility();
            UpdateStatus();
        }

        private void Select(LevelObjectMarker m)
        {
            _selected = m;
            if (m != null && (m.isDestination || m.isPickup))
                SetCurrentColor(m.color, false);
            UpdateColorVisibility();
        }

        private void ClearAll()
        {
            foreach (KeyValuePair<string, GameObject> kv in _spawned)
                if (kv.Value != null)
                    Destroy(kv.Value);
            _spawned.Clear();
            _byId.Clear();

            if (_spawnMarker != null)
                Destroy(_spawnMarker);
            _spawnMarker = null;
            _selected = null;
        }

        private void Rebuild()
        {
            for (int i = 0; i < _data.objects.Count; i++)
            {
                LevelObjectData o = _data.objects[i];
                if (o == null)
                    continue;
                if (string.IsNullOrEmpty(o.id))
                    o.id = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                _byId[o.id] = o;
                SpawnEditor(o);
            }
            SetPlayerSpawn(_data.playerSpawn.ToVector2());
        }

        // ------------------------------------------------------------------ file actions

        private void SyncFields()
        {
            _data.levelName = string.IsNullOrWhiteSpace(_nameField.text) ? "Untitled" : _nameField.text.Trim();
            _data.author = _authorField.text;

            float t;
            if (float.TryParse(_timerField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out t) && t > 0f)
                _data.timer = t;
            else
                _data.timer = 120f;
        }

        private void NewLevel()
        {
            ClearAll();
            _data = new LevelData();
            _data.levelName = "Untitled";
            _data.timer = 120f;

            _nameField.text = "Untitled";
            _authorField.text = "";
            _timerField.text = "120";
            SetStatus("New level.");
            UpdateColorVisibility();
            UpdateStatus();
        }

        private void SaveWorking()
        {
            SyncFields();
            LevelPaths.EnsureFolders();
            string path = LevelPaths.FileNameFor(_data.levelName, false);
            string err;
            if (LevelIO.Save(_data, path, out err))
                SetStatus("Saved: " + path);
            else
                SetStatus("Save failed: " + err);
        }

        private void ExportActive()
        {
            SyncFields();
            LevelPaths.EnsureFolders();
            string path = LevelPaths.FileNameFor(_data.levelName, true);
            string err;
            if (LevelIO.Save(_data, path, out err))
                SetStatus("Exported playable level: " + path);
            else
                SetStatus("Export failed: " + err);
        }

        private void OpenLoadPanel()
        {
            _loadPanel.SetActive(true);
            foreach (Transform child in _loadListContent)
                Destroy(child.gameObject);

            string[] working = LevelPaths.ListWorking();
            string[] active = LevelPaths.ListActive();

            AddListHeader("Working on projects");
            if (working.Length == 0)
                AddListInfo("(empty)");
            for (int i = 0; i < working.Length; i++)
                AddListButton(working[i]);

            AddListHeader("Active levels");
            if (active.Length == 0)
                AddListInfo("(empty)");
            for (int i = 0; i < active.Length; i++)
                AddListButton(active[i]);
        }

        private void CloseLoadPanel()
        {
            _loadPanel.SetActive(false);
        }

        private void LoadFromPath(string path)
        {
            string err;
            LevelData loaded = LevelIO.Load(path, out err);
            if (loaded == null)
            {
                SetStatus("Load failed: " + err);
                return;
            }

            ClearAll();
            _data = loaded;
            _nameField.text = _data.levelName;
            _authorField.text = _data.author;
            _timerField.text = _data.timer.ToString("0.#");
            Rebuild();
            CloseLoadPanel();
            SetStatus("Loaded: " + Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ pause

        private void TogglePause()
        {
            _paused = !_paused;
            _pausePanel.SetActive(_paused);
            if (_paused)
            {
                _dragging = null;
                _painting = false;
                ShowPauseMain();
            }
        }

        private void ResumeEditor()
        {
            _paused = false;
            _pausePanel.SetActive(false);
        }

        private void ShowPauseMain()
        {
            _pauseMain.SetActive(true);
            _pauseConfirm.SetActive(false);
        }

        private void ShowPauseConfirm()
        {
            _pauseMain.SetActive(false);
            _pauseConfirm.SetActive(true);
        }

        private void SaveAndExit()
        {
            SaveWorking();
            SceneManager.LoadScene("MainMenu");
        }

        private void ExitWithoutSave()
        {
            SceneManager.LoadScene("MainMenu");
        }

        // ------------------------------------------------------------------ UI

        private void BuildUI()
        {
            Canvas canvas = UIFactory.CreateCanvas("EditorCanvas", 10);

            // ---- top bar ----
            RectTransform top = UIFactory.CreatePanel(canvas.transform, "TopBar",
                new Color(0.10f, 0.11f, 0.15f, 0.95f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), Vector2.zero);

            CreateTopLabel(top, "Level:", 20f);
            _nameField = UIFactory.CreateInputField(top, "Untitled", "level name");
            PlaceAnchored(_nameField.GetComponent<RectTransform>(), 90f, 240f, 0f, 42f);

            CreateTopLabel(top, "Author:", 350f);
            _authorField = UIFactory.CreateInputField(top, "", "author");
            PlaceAnchored(_authorField.GetComponent<RectTransform>(), 425f, 150f, 0f, 42f);

            CreateTopLabel(top, "Timer (s):", 595f);
            _timerField = UIFactory.CreateInputField(top, "120", "120");
            PlaceAnchored(_timerField.GetComponent<RectTransform>(), 705f, 80f, 0f, 42f);
            _timerField.onValueChanged.AddListener(OnTimerChanged);

            _status = UIFactory.CreateText(top, "", 20, new Color(0.8f, 0.85f, 0.95f, 1f), TextAnchor.MiddleRight);
            UIFactory.SetAnchored(_status.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(1000f, 50f));

            // ---- palette ----
            RectTransform palette = UIFactory.CreatePanel(canvas.transform, "Palette",
                new Color(0.10f, 0.11f, 0.15f, 0.95f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 80f), new Vector2(230f, -80f));
            VerticalLayoutGroup vlg = palette.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            Text paletteTitle = UIFactory.CreateText(palette, "TOOLS", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            LayoutElement titleLe = paletteTitle.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredHeight = 30f;

            UIFactory.CreateButton(palette, "Hand (move)", SetHand, 40f);
            foreach (PlaceableDef def in PlaceableCatalog.All)
            {
                PlaceableType captured = def.type;
                UIFactory.CreateButton(palette, def.displayName, () => SetTool(captured), 40f);
            }

            _snapButton = UIFactory.CreateButton(palette, "Snap: ON", ToggleSnap, 34f);

            BuildColorSection(palette);

            // ---- bottom bar ----
            RectTransform bottom = UIFactory.CreatePanel(canvas.transform, "BottomBar",
                new Color(0.10f, 0.11f, 0.15f, 0.95f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 70f));
            HorizontalLayoutGroup hlg = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.spacing = 8f;
            hlg.padding = new RectOffset(10, 10, 10, 10);

            UIFactory.CreateButton(bottom, "New", NewLevel);
            UIFactory.CreateButton(bottom, "Save", SaveWorking);
            UIFactory.CreateButton(bottom, "Import", OpenLoadPanel);
            UIFactory.CreateButton(bottom, "Export Playable", ExportActive);
            UIFactory.CreateButton(bottom, "Pause", () => { _paused = false; TogglePause(); });

            // ---- load panel ----
            _loadPanel = UIFactory.CreatePanel(canvas.transform, "LoadPanel",
                new Color(0.05f, 0.06f, 0.09f, 0.98f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-450f, -320f), new Vector2(450f, 320f)).gameObject;

            Text loadTitle = UIFactory.CreateText(_loadPanel.transform, "Import level", 28, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(loadTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(0f, 44f));

            UIFactory.CreateScrollView(_loadPanel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(16f, 70f), new Vector2(-16f, -60f), out _loadListContent);

            Button close = UIFactory.CreateButton(_loadPanel.transform, "Close", CloseLoadPanel, 44f);
            UIFactory.SetAnchored(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(200f, 44f));

            _loadPanel.SetActive(false);

            BuildPauseMenu(canvas);
        }

        private void BuildColorSection(RectTransform palette)
        {
            RectTransform cs = UIFactory.CreatePanel(palette, "ColorSection", new Color(0f, 0f, 0f, 0.25f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            LayoutElement le = cs.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 250f;
            le.minHeight = 250f;
            _colorSection = cs.gameObject;

            Text title = UIFactory.CreateText(cs, "COLOR", 18, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceInSection(title.rectTransform, 6f, 4f, 190f, 24f);

            RectTransform swatch = UIFactory.CreatePanel(cs, "Swatch", Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PlaceInSection(swatch, 6f, 30f, 190f, 34f);
            _colorSwatch = swatch.GetComponent<Image>();

            _redSlider = CreateColorRow(cs, "R", 68f, new Color(0.90f, 0.30f, 0.30f));
            _greenSlider = CreateColorRow(cs, "G", 100f, new Color(0.30f, 0.85f, 0.35f));
            _blueSlider = CreateColorRow(cs, "B", 132f, new Color(0.35f, 0.50f, 0.95f));

            // preset swatches
            GameObject row = new GameObject("Presets", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(cs, false);
            RectTransform rowRt = row.GetComponent<RectTransform>();
            PlaceInSection(rowRt, 6f, 168f, 190f, 30f);
            HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.spacing = 4f;

            for (int i = 0; i < Presets.Length; i++)
            {
                Color preset = Presets[i];
                Button b = UIFactory.CreateButton(row.transform, "", null, 30f);
                Image img = b.GetComponent<Image>();
                if (img != null) img.color = preset;
                Text label = b.GetComponentInChildren<Text>();
                if (label != null) label.text = "";
                b.onClick.AddListener(() => SetCurrentColor(preset, true));
            }

            Text hint = UIFactory.CreateText(cs, "0 - 1 per channel", 14, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleLeft);
            PlaceInSection(hint.rectTransform, 6f, 202f, 190f, 20f);
        }

        private Slider CreateColorRow(RectTransform parent, string label, float y, Color fill)
        {
            Text l = UIFactory.CreateText(parent, label, 18, Color.white, TextAnchor.MiddleLeft);
            PlaceInSection(l.rectTransform, 6f, y, 18f, 22f);

            Slider s = UIFactory.CreateSlider(parent, 0f, 1f, 0f, OnColorSliderChanged, 22f, fill);
            PlaceInSection(s.GetComponent<RectTransform>(), 28f, y, 168f, 22f);
            return s;
        }

        private void PlaceInSection(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private void BuildPauseMenu(Canvas canvas)
        {
            _pausePanel = UIFactory.CreatePanel(canvas.transform, "PausePanel", new Color(0f, 0f, 0f, 0.72f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

            _pauseMain = UIFactory.CreatePanel(_pausePanel.transform, "PauseMain", new Color(0.08f, 0.09f, 0.12f, 0.99f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, -170f), new Vector2(260f, 170f)).gameObject;

            Text t1 = UIFactory.CreateText(_pauseMain.transform, "PAUSED", 36, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(t1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(400f, 50f));

            Button cont = UIFactory.CreateButton(_pauseMain.transform, "Continue", ResumeEditor, 52f);
            UIFactory.SetAnchored(cont.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(320f, 52f));

            Button menu = UIFactory.CreateButton(_pauseMain.transform, "Main Menu", ShowPauseConfirm, 52f);
            UIFactory.SetAnchored(menu.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(320f, 52f));

            _pauseConfirm = UIFactory.CreatePanel(_pausePanel.transform, "PauseConfirm", new Color(0.08f, 0.09f, 0.12f, 0.99f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-320f, -180f), new Vector2(320f, 180f)).gameObject;

            Text t2 = UIFactory.CreateText(_pauseConfirm.transform, "Save the level before leaving?", 26, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchored(t2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 60f));

            Button save = UIFactory.CreateButton(_pauseConfirm.transform, "Save & Exit", SaveAndExit, 50f);
            UIFactory.SetAnchored(save.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(380f, 50f));

            Button exit = UIFactory.CreateButton(_pauseConfirm.transform, "Exit Without Saving", ExitWithoutSave, 50f);
            UIFactory.SetAnchored(exit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(380f, 50f));

            Button cancel = UIFactory.CreateButton(_pauseConfirm.transform, "Cancel", ShowPauseMain, 50f);
            UIFactory.SetAnchored(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -105f), new Vector2(380f, 50f));

            _pausePanel.SetActive(false);
        }

        private Text CreateTopLabel(RectTransform parent, string text, float x)
        {
            Text t = UIFactory.CreateText(parent, text, 20, Color.white, TextAnchor.MiddleLeft);
            UIFactory.SetAnchored(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(110f, 40f));
            return t;
        }

        private void PlaceAnchored(RectTransform rt, float x, float width, float y, float height)
        {
            UIFactory.SetAnchored(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(x, y), new Vector2(width, height));
        }

        private void AddListHeader(string text)
        {
            Text t = UIFactory.CreateText(_loadListContent, text, 22, new Color(0.6f, 0.8f, 1f, 1f),
                TextAnchor.MiddleLeft, FontStyle.Bold);
            LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;
        }

        private void AddListInfo(string text)
        {
            Text t = UIFactory.CreateText(_loadListContent, text, 18, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleLeft);
            LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 30f;
        }

        private void AddListButton(string path)
        {
            string captured = path;
            string label = Path.GetFileNameWithoutExtension(path);
            UIFactory.CreateButton(_loadListContent, label, () => LoadFromPath(captured), 44f);
        }

        private void SetHand()
        {
            _handMode = true;
            SetStatus("Tool: Hand (move / edit colour)");
            UpdateColorVisibility();
        }

        private void SetTool(PlaceableType type)
        {
            _handMode = false;
            _tool = type;
            SetStatus("Tool: " + PlaceableCatalog.Get(type).displayName);
            UpdateColorVisibility();
        }

        private void ToggleSnap()
        {
            _snap = !_snap;
            Text t = _snapButton.GetComponentInChildren<Text>();
            if (t != null)
                t.text = _snap ? "Snap: ON" : "Snap: OFF";
        }

        private void OnTimerChanged(string v)
        {
            float t;
            if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out t) && t > 0f)
                _data.timer = t;
        }

        private void OnColorSliderChanged(float _)
        {
            if (_suppressColorEvent)
                return;
            SetCurrentColor(new Color(_redSlider.value, _greenSlider.value, _blueSlider.value, 1f), true);
        }

        private void SetCurrentColor(Color c, bool applyToSelection)
        {
            _currentColor = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);

            _suppressColorEvent = true;
            if (_redSlider != null) _redSlider.value = _currentColor.r;
            if (_greenSlider != null) _greenSlider.value = _currentColor.g;
            if (_blueSlider != null) _blueSlider.value = _currentColor.b;
            _suppressColorEvent = false;

            if (_colorSwatch != null)
                _colorSwatch.color = _currentColor;

            if (applyToSelection && _selected != null && (_selected.isDestination || _selected.isPickup))
            {
                _selected.color = _currentColor;
                SpriteRenderer sr = _selected.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = _currentColor;

                LevelObjectData d;
                if (_byId.TryGetValue(_selected.id, out d))
                    d.color = _currentColor;
            }
        }

        private void UpdateColorVisibility()
        {
            bool need = (!_handMode && (_tool == PlaceableType.Shelf || _tool == PlaceableType.Box))
                        || (_selected != null && (_selected.isDestination || _selected.isPickup));
            if (_colorSection != null)
                _colorSection.SetActive(need);
        }

        private bool PointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private bool TypingInField()
        {
            if (EventSystem.current == null)
                return false;
            GameObject go = EventSystem.current.currentSelectedGameObject;
            return go != null && go.GetComponent<InputField>() != null;
        }

        private LevelObjectMarker PickTop(Vector2 world)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(world);
            LevelObjectMarker best = null;
            int bestOrder = int.MinValue;
            for (int i = 0; i < hits.Length; i++)
            {
                LevelObjectMarker m = hits[i].GetComponentInParent<LevelObjectMarker>();
                if (m == null)
                    continue;
                SpriteRenderer sr = m.GetComponent<SpriteRenderer>();
                int order = sr != null ? sr.sortingOrder : 0;
                if (order >= bestOrder)
                {
                    bestOrder = order;
                    best = m;
                }
            }
            return best;
        }

        private void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message;
        }

        private void UpdateStatus()
        {
            if (_status == null)
                return;
            string tool = _handMode ? "Hand" : PlaceableCatalog.Get(_tool).displayName;
            SetStatus("Objects: " + _data.objects.Count +
                      "   Timer: " + _data.timer.ToString("0.#") + "s" +
                      "   Tool: " + tool);
        }
    }
}
