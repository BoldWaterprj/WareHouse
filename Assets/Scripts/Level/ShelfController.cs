using System.Collections.Generic;
using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>
    /// A shelf holds up to two boxes. When a box is dropped (not carried) inside the
    /// shelf zone it snaps into a free slot, sits behind the shelf (sorting order)
    /// so it looks like it is inside, and its colour is recorded for scoring.
    /// </summary>
    public class ShelfController : MonoBehaviour
    {
        public Color shelfColor = Color.white;
        public int capacity = 2;

        private readonly List<LevelObjectMarker> _placed = new List<LevelObjectMarker>();

        public int PlacedCount { get { return _placed.Count; } }
        public IReadOnlyList<LevelObjectMarker> Placed { get { return _placed; } }

        public Vector2 SlotPosition(int index)
        {
            // Two slots side by side across the 2-unit-wide shelf. The box sits
            // slightly above the shelf centre so it peeks out while the shelf (drawn
            // in front) covers its lower part, giving the "inside the shelf" look.
            float x = index <= 0 ? -0.5f : 0.5f;
            return new Vector2(transform.position.x + x, transform.position.y - 0.4f);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (_placed.Count >= capacity)
                return;

            LevelObjectMarker marker = other.GetComponentInParent<LevelObjectMarker>();
            Pick pick = other.GetComponentInParent<Pick>();
            if (marker == null || pick == null || !marker.isPickup)
                return;
            if (pick.isHolding || marker.placed)
                return;

            Place(marker, pick);
        }

        private void Place(LevelObjectMarker marker, Pick pick)
        {
            int slot = _placed.Count;
            marker.transform.position = SlotPosition(slot);
            marker.placed = true;
            marker.shelfColor = shelfColor;

            pick.isHolding = false;
            pick.enabled = false;

            Rigidbody2D rb = marker.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            Collider2D[] cols = marker.GetComponents<Collider2D>();
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;

            SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = PlaceableCatalog.PlacedBoxSortingOrder;

            _placed.Add(marker);
        }
    }
}
