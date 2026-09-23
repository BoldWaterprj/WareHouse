using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Warehouse.UI;

namespace Warehouse.Levels
{
    /// <summary>
    /// Plays a level exported to the "Active levels" folder. Reuses the existing
    /// Move / Pick / Inventory gameplay scripts.
    /// </summary>
    public class LevelRuntime : MonoBehaviour
    {
        private LevelData _data;
        private Camera _cam;
        private Transform _player;
        private Transform _holdPoint;

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            SetupCamera();
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

        private void SpawnObject(LevelObjectData o, Transform root)
        {
            PlaceableType type;
            if (!PlaceableCatalog.TryParse(o.type, out type))
                return;

            PlaceableDef def = PlaceableCatalog.Get(type);
            GameObject go = PlaceableCatalog.CreateVisual(type, o.position.ToVector2(), o.rotation,
                o.scale.ToVector2(), root);
            go.name = type + "_" + o.id;

            LevelObjectMarker marker = go.AddComponent<LevelObjectMarker>();
            marker.id = o.id;
            marker.type = type;
            marker.destinationId = o.destinationId;
            marker.isPickup = def.isPickup;
            marker.isDestination = def.isDestination;

            switch (type)
            {
                case PlaceableType.Box:
                    SpawnBox(go, type);
                    break;
                case PlaceableType.Shelf:
                    PlaceableCatalog.AddCollider(go, type, false);
                    ShelfDestination shelf = go.AddComponent<ShelfDestination>();
                    shelf.destinationId = o.destinationId;
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

        private void BuildHUD()
        {
            Canvas canvas = UIFactory.CreateCanvas("RuntimeCanvas", 10);

            string name = _data != null ? _data.levelName : "(none)";
            Text title = UIFactory.CreateText(canvas.transform, name, 30, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(700f, 50f));

            Text hint = UIFactory.CreateText(canvas.transform,
                "WASD / arrows to move    -    E to pick up / drop",
                20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.SetAnchored(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(700f, 40f));

            Button back = UIFactory.CreateButton(canvas.transform, "Levels", () => SceneManager.LoadScene("LevelSelect"), 46f);
            UIFactory.SetAnchored(back.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(180f, 46f));

            Button restart = UIFactory.CreateButton(canvas.transform, "Restart",
                () => SceneManager.LoadScene("LevelRuntime"), 46f);
            UIFactory.SetAnchored(restart.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-210f, -16f), new Vector2(180f, 46f));

            Button menu = UIFactory.CreateButton(canvas.transform, "Menu", () => SceneManager.LoadScene("MainMenu"), 46f);
            UIFactory.SetAnchored(menu.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-400f, -16f), new Vector2(180f, 46f));
        }

        private void LateUpdate()
        {
            if (_cam == null || _player == null)
                return;
            Vector3 target = new Vector3(_player.position.x, _player.position.y, -10f);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, target, 8f * Time.deltaTime);
        }
    }
}
