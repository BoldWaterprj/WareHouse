using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>Attached to every level object so it can be identified / edited.</summary>
    public class LevelObjectMarker : MonoBehaviour
    {
        public string id;
        public PlaceableType type;
        public string destinationId;
        public bool isPlayerSpawn;
        public bool isPickup;
        public bool isDestination;
    }
}
