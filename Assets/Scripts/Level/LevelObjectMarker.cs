using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>Attached to every level object so it can be identified / edited.</summary>
    public class LevelObjectMarker : MonoBehaviour
    {
        public string id;
        public PlaceableType type;

        /// <summary>Colour code of this object (used by Shelf / Box).</summary>
        public Color color = Color.white;

        /// <summary>For a placed box: the colour of the shelf it was placed on.</summary>
        public Color shelfColor = Color.white;

        public bool isPlayerSpawn;
        public bool isPickup;
        public bool isDestination;

        /// <summary>True once the box snapped into a shelf.</summary>
        public bool placed;
    }
}
