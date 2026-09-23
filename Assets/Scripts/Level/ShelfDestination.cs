using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>
    /// Marks a shelf as a destination. A box whose marker.destinationId matches
    /// this shelf's destinationId is considered correctly placed.
    /// (Full scoring is wired up in the session/instructor step.)
    /// </summary>
    public class ShelfDestination : MonoBehaviour
    {
        public string destinationId = "";

        public bool IsMatch(LevelObjectMarker box)
        {
            if (box == null)
                return false;
            return !string.IsNullOrEmpty(destinationId) &&
                   string.Equals(destinationId, box.destinationId, System.StringComparison.OrdinalIgnoreCase);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            LevelObjectMarker marker = other.GetComponentInParent<LevelObjectMarker>();
            if (marker == null || !marker.isPickup)
                return;

            Debug.Log(IsMatch(marker)
                ? "Shelf '" + destinationId + "': correct box placed."
                : "Shelf '" + destinationId + "': WRONG box (box is '" + marker.destinationId + "').");
        }
    }
}
