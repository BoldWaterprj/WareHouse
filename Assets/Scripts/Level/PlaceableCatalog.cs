using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warehouse.Levels
{
    public enum PlaceableType
    {
        Floor,
        Wall,
        Column,
        Shelf,
        Box,
        PlayerSpawn
    }

    public class PlaceableDef
    {
        public PlaceableType type;
        public string displayName;
        public Color color;
        public Vector2 size;
        public bool hasCollider;
        public bool isDestination;
        public bool isPickup;
        public int sortingOrder;
    }

    /// <summary>
    /// Single source of truth for the objects that can be placed in a level.
    /// Sprites are generated procedurally so the system has no fragile asset
    /// references; swap in real art later via the optional override sprites.
    /// </summary>
    public static class PlaceableCatalog
    {
        private static Dictionary<PlaceableType, PlaceableDef> _defs;
        private static Sprite _whiteSprite;
        private static Material _unlitMaterial;

        // Optional art overrides (loaded from Resources/Art; falls back to the
        // generated white sprite when a real asset is missing).
        public static readonly Dictionary<PlaceableType, Sprite> SpriteOverrides =
            new Dictionary<PlaceableType, Sprite>();

        static PlaceableCatalog()
        {
            TryOverride(PlaceableType.Floor, "Art/Floor");
            TryOverride(PlaceableType.Wall, "Art/Wall");
            TryOverride(PlaceableType.Column, "Art/Column");
            TryOverride(PlaceableType.Shelf, "Art/Shelf");
            TryOverride(PlaceableType.Box, "Art/Box");
        }

        private static void TryOverride(PlaceableType type, string resourcePath)
        {
            Sprite s = Resources.Load<Sprite>(resourcePath);
            if (s != null)
                SpriteOverrides[type] = s;
        }

        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                    tex.name = "ProcWhite";
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    Color32[] px = new Color32[64];
                    for (int i = 0; i < px.Length; i++)
                        px[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(px);
                    tex.Apply();
                    _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
                    _whiteSprite.name = "ProcWhite";
                }
                return _whiteSprite;
            }
        }

        public static Material UnlitMaterial
        {
            get
            {
                if (_unlitMaterial == null)
                {
                    Shader sh = Shader.Find("Sprites/Default");
                    if (sh == null)
                        sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh != null)
                        _unlitMaterial = new Material(sh);
                }
                return _unlitMaterial;
            }
        }

        public static IEnumerable<PlaceableDef> All
        {
            get
            {
                Ensure();
                return _defs.Values;
            }
        }

        public static PlaceableDef Get(PlaceableType type)
        {
            Ensure();
            return _defs[type];
        }

        public static bool TryParse(string name, out PlaceableType type)
        {
            type = PlaceableType.Floor;
            if (string.IsNullOrEmpty(name))
                return false;

            foreach (PlaceableType value in (PlaceableType[])Enum.GetValues(typeof(PlaceableType)))
            {
                if (string.Equals(value.ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    type = value;
                    return true;
                }
            }
            return false;
        }

        private static void Ensure()
        {
            if (_defs != null)
                return;

            _defs = new Dictionary<PlaceableType, PlaceableDef>();
            Add(PlaceableType.Floor, "Floor", new Color(0.52f, 0.52f, 0.56f), new Vector2(1f, 1f), false, false, false, -20);
            Add(PlaceableType.Wall, "Wall", new Color(0.30f, 0.31f, 0.36f), new Vector2(1f, 1f), true, false, false, 0);
            Add(PlaceableType.Column, "Column", new Color(0.18f, 0.18f, 0.22f), new Vector2(1f, 1f), true, false, false, 1);
            Add(PlaceableType.Shelf, "Shelf", new Color(0.25f, 0.55f, 0.90f), new Vector2(2f, 1f), true, true, false, 2);
            Add(PlaceableType.Box, "Box", new Color(0.87f, 0.56f, 0.24f), new Vector2(0.9f, 0.9f), true, false, true, 3);
            Add(PlaceableType.PlayerSpawn, "Player Spawn", new Color(0.20f, 0.90f, 0.35f), new Vector2(0.5f, 0.5f), false, false, false, 6);
        }

        private static void Add(PlaceableType type, string displayName, Color color, Vector2 size,
            bool hasCollider, bool isDestination, bool isPickup, int sortingOrder)
        {
            _defs[type] = new PlaceableDef
            {
                type = type,
                displayName = displayName,
                color = color,
                size = size,
                hasCollider = hasCollider,
                isDestination = isDestination,
                isPickup = isPickup,
                sortingOrder = sortingOrder
            };
        }

        public static Sprite GetSprite(PlaceableType type)
        {
            Sprite s;
            if (SpriteOverrides.TryGetValue(type, out s) && s != null)
                return s;
            return WhiteSprite;
        }

        /// <summary>Creates the visual object (no collider / gameplay scripts).</summary>
        public static GameObject CreateVisual(PlaceableType type, Vector2 position, float rotation,
            Vector2 scale, Transform parent)
        {
            PlaceableDef def = Get(type);
            GameObject go = new GameObject(def.displayName);
            if (parent != null)
                go.transform.SetParent(parent, false);

            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            go.transform.localScale = new Vector3(def.size.x * scale.x, def.size.y * scale.y, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite(type);
            sr.color = def.color;
            sr.sortingOrder = def.sortingOrder;
            if (UnlitMaterial != null)
                sr.sharedMaterial = UnlitMaterial;

            return go;
        }
    }
}
