using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>Serializable 2D vector (JsonUtility-friendly).</summary>
    [Serializable]
    public class Vec2
    {
        public float x;
        public float y;

        public Vec2() { }

        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2 ToVector2()
        {
            return new Vector2(x, y);
        }

        public static Vec2 From(Vector2 v)
        {
            return new Vec2(v.x, v.y);
        }
    }

    /// <summary>One placed object in a level.</summary>
    [Serializable]
    public class LevelObjectData
    {
        /// <summary>Placeable type name: Floor, Wall, Column, Shelf, Box.</summary>
        public string type = "Floor";

        /// <summary>Unique instance id.</summary>
        public string id = "";

        /// <summary>
        /// Colour code for Shelf / Box. The colour IS the matching key: a box
        /// placed on a shelf with the same colour scores best. Stored in the level.
        /// </summary>
        public Color color = Color.white;

        public Vec2 position = new Vec2();
        public float rotation = 0f;
        public Vec2 scale = new Vec2(1f, 1f);
    }

    /// <summary>Root level document stored as JSON.</summary>
    [Serializable]
    public class LevelData
    {
        public int formatVersion = 2;
        public string levelName = "Untitled";
        public string author = "";
        public string createdUtc = "";

        /// <summary>Level time in seconds, set in the editor and written explicitly.</summary>
        public float timer = 120f;

        /// <summary>Separate, adjustable player spawn location.</summary>
        public Vec2 playerSpawn = new Vec2(0f, 0f);

        public List<LevelObjectData> objects = new List<LevelObjectData>();
    }
}
