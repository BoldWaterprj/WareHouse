using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Warehouse.UI;

namespace Warehouse.Levels
{
    /// <summary>
    /// Runtime level editor. Its UI is built in code so the scene stays tiny.
    ///
    /// Controls:
    ///   Left click         place the selected object (or drag an existing one)
    ///   Right click        delete the object under the cursor
    ///   Middle drag        pan camera
    ///   Mouse wheel        zoom
    ///   Delete             delete the selected object
    /// </summary>
    public class LevelEditorController : MonoBehaviour
    {
        private LevelData _data;
        private readonly Dictionary<string, GameObject> _spawned = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, LevelObjectData> _byId = new Dictionary<string, LevelObjectData>();
        private GameObject _spawnMarker;

        private PlaceableType _tool = PlaceableType.Floor;
        private bool _snap = true;
        private float _grid = 0.5f;
        private LevelObjectMarker _dragging;
        private LevelObjectMarker _selected;

        private Camera _cam;
        private Transform _levelRoot;
        private bool _panning;

        private Text _status;
        private InputField _nameField;
        private InputField _authorField;
        private InputField _destField;
        private GameObject _loadPanel;
        private RectTransform _loadListContent;
        private Button _snapButton;
        private bool _suppressDestEvent;

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            SetupCamera();

            _levelRoot = new GameObject("Level").transform;

            _data = new LevelData();
            _data.levelName = "Untitled";

            BuildUI();
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
            HandleCamera();
            HandleTools();
            HandleKeys();
        }

        // ------------------------------------------------------------------ input

        private void HandleCamera()
        {
            Vector3 _prevMouse = new Vector3();

            if (!PointerOverUI() && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                _cam.orthographicSize = Mathf.Clamp(
                    _cam.orthographicSize - Input.mouseScrollDelta.y * 0.8f, 2f, 40f);
            }

            if (Input.GetMouseButtonDown(2))
                _panning = true;
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
            Vector2 snapped = Snap(world);

            if (Input.GetMouseButtonDown(0))
            {
                LevelObjectMarker hit = PickTop(world);
                if (hit != null)
                {
                    _dragging = hit;
                    Select(hit);
                }
                else
                {
                    Place(snapped);
                }
            }

            if (_dragging != null && Input.GetMouseButton(0))
            {
                _dragging.transform.position = new Vector3(snapped.x, snapped.y, 0f);
            }

            if (_dragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2 p = _dragging.transform.position;
                if (_dragging.isPlayerSpawn)
                {
                    _data.playerSpawn = Vec2.From(p);
                }
                else
                {
                    LevelObjectData d;
                    if (_byId.TryGetValue(_dragging.id, out d))
                        d.position = Vec2.From(p);
                }
                _dragging = null;
                UpdateStatus();
            }

            if (Input.GetMouseButtonDown(1))
            {
                LevelObjectMarker hit = PickTop(world);
                if (hit != null)
                    Remove(hit);
            }
        }

        private void HandleKeys()
        {
            if (TypingInField())
                return;

            if (Input.GetKeyDown(KeyCode.Delete) && _selected != null)
                Remove(_selected);
            if (Input.GetKeyDown(KeyCode.Escape) && _loadPanel.activeSelf)
                CloseLoadPanel();
        }

        private Vector2 Snap(Vector2 p)
        {
            if (!_snap)
                return p;
            return new Vector2(Mathf.Round(p.x / _grid) * _grid, Mathf.Round(p.y / _grid) * _grid);
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

        // ------------------------------------------------------------------ editing

        private void Place(Vector2 pos)
        {
            if (_tool == PlaceableType.PlayerSpawn)
            {
                SetPlayerSpawn(pos);
                return;
            }

            PlaceableDef def = PlaceableCatalog.Get(_tool);
            LevelObjectData obj = new LevelObjectData
            {
                type = _tool.ToString(),
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                destinationId = (def.isDestination || def.isPickup) ? _destField.text : "",
                position = Vec2.From(pos),
                scale = new Vec2(1f, 1f)
            };

            _data.objects.Add(obj);
            _byId[obj.id] = obj;
            GameObject go = SpawnEditor(obj);
            Select(go.GetComponent<LevelObjectMarker>());
            UpdateStatus();
        }

        private GameObject SpawnEditor(LevelObjectData obj)
        {
            PlaceableType type;
            if (!PlaceableCatalog.TryParse(obj.type, out type))
                type = PlaceableType.Floor;

            PlaceableDef def = PlaceableCatalog.Get(type);
            GameObject go = PlaceableCatalog.CreateVisual(type, obj.position.ToVector2(), obj.rotation,
                obj.scale.ToVector2(), _levelRoot);
            go.name = type + "_" + obj.id;

            LevelObjectMarker marker = go.AddComponent<LevelObjectMarker>();
            marker.id = obj.id;
            marker.type = type;
            marker.destinationId = obj.destinationId;
            marker.isPickup = def.isPickup;
            marker.isDestination = def.isDestination;
            marker.isPlayerSpawn = type == PlaceableType.PlayerSpawn;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

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

                BoxCollider2D c = _spawnMarker.AddComponent<BoxCollider2D>();
                c.isTrigger = true;
                c.size = Vector2.one;
            }
            else
            {
                _spawnMarker.transform.position = new Vector3(pos.x, pos.y, 0f);
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
            UpdateStatus();
        }

        private void Select(LevelObjectMarker m)
        {
            _selected = m;
            if (m != null && (m.isDestination || m.isPickup))
            {
                _suppressDestEvent = true;
                _destField.text = m.destinationId;
                _suppressDestEvent = false;
            }
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
        }

        private void NewLevel()
        {
            ClearAll();
            _data = new LevelData();
            _data.levelName = "Untitled";

            _nameField.text = "Untitled";
            _authorField.text = "";
            _destField.text = "A";
            SetStatus("New level.");
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
            Rebuild();
            CloseLoadPanel();
            SetStatus("Loaded: " + Path.GetFileName(path));
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
            PlaceAnchored(_nameField.GetComponent<RectTransform>(), 90f, 280f, 0f, 42f);

            CreateTopLabel(top, "Author:", 390f);
            _authorField = UIFactory.CreateInputField(top, "", "author");
            PlaceAnchored(_authorField.GetComponent<RectTransform>(), 475f, 200f, 0f, 42f);

            CreateTopLabel(top, "Dest. ID:", 695f);
            _destField = UIFactory.CreateInputField(top, "A", "A");
            PlaceAnchored(_destField.GetComponent<RectTransform>(), 790f, 110f, 0f, 42f);
            _destField.onValueChanged.AddListener(OnDestChanged);

            _status = UIFactory.CreateText(top, "", 20, new Color(0.8f, 0.85f, 0.95f, 1f), TextAnchor.MiddleRight);
            UIFactory.SetAnchored(_status.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(880f, 50f));

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

            Text paletteTitle = UIFactory.CreateText(palette, "PLACE", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            LayoutElement titleLe = paletteTitle.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredHeight = 30f;

            foreach (PlaceableDef def in PlaceableCatalog.All)
            {
                PlaceableType captured = def.type;
                UIFactory.CreateButton(palette, def.displayName, () => SetTool(captured), 46f);
            }

            _snapButton = UIFactory.CreateButton(palette, "Snap: ON", ToggleSnap, 40f);

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
        }

        private void CreateTopLabel(RectTransform parent, string text, float x)
        {
            Text t = UIFactory.CreateText(parent, text, 20, Color.white, TextAnchor.MiddleLeft);
            UIFactory.SetAnchored(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(90f, 40f));
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

        private void SetTool(PlaceableType type)
        {
            _tool = type;
            SetStatus("Tool: " + PlaceableCatalog.Get(type).displayName);
        }

        private void ToggleSnap()
        {
            _snap = !_snap;
            Text t = _snapButton.GetComponentInChildren<Text>();
            if (t != null)
                t.text = _snap ? "Snap: ON" : "Snap: OFF";
        }

        private void OnDestChanged(string v)
        {
            if (_suppressDestEvent)
                return;
            if (_selected != null && (_selected.isDestination || _selected.isPickup))
            {
                _selected.destinationId = v;
                LevelObjectData d;
                if (_byId.TryGetValue(_selected.id, out d))
                    d.destinationId = v;
            }
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
            SetStatus("Objects: " + _data.objects.Count +
                      "   Spawn: (" + _data.playerSpawn.x.ToString("0.##") + ", " +
                      _data.playerSpawn.y.ToString("0.##") + ")   Tool: " +
                      PlaceableCatalog.Get(_tool).displayName);
        }
    }
}
