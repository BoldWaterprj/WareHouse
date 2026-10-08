using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>
    /// A shelf holds up to two boxes. A box dropped (not carried) inside the shelf
    /// zone snaps into a free slot and sits behind the shelf. You can also take the
    /// last box back: stand next to the shelf, carry nothing and press E.
    /// </summary>
    public class ShelfController : MonoBehaviour
    {
        public Color shelfColor = Color.white;
        public int capacity = 2;
        public float interactionRadius = 2f;

        private readonly List<LevelObjectMarker> _placed = new List<LevelObjectMarker>();

        public int PlacedCount { get { return _placed.Count; } }
        public IReadOnlyList<LevelObjectMarker> Placed { get { return _placed; } }

        public Vector2 SlotPosition(int index)
        {
            float x = index <= 0 ? -0.5f : 0.5f;
            return new Vector2(transform.position.x + x, transform.position.y - 0.4f);
        }

        private void Update()
        {
            if (_placed.Count == 0)
                return;
            if (!Input.GetKeyDown(KeyCode.E))
                return;

            Inventory inv = FindFirstObjectByType<Inventory>();
            if (inv == null || inv.currentCarriedBox != null)
                return; // carrying something already -> that E is for dropping

            if (Vector2.Distance(inv.transform.position, transform.position) > interactionRadius)
                return;

            TakeLast(inv);
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
            marker.transform.position = SlotPosition(_placed.Count);
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

            SetColliders(marker, false);

            SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = PlaceableCatalog.PlacedBoxSortingOrder;

            _placed.Add(marker);
        }

        private void TakeLast(Inventory inv)
        {
            LevelObjectMarker marker = _placed[_placed.Count - 1];
            _placed.RemoveAt(_placed.Count - 1);
            marker.placed = false;

            Transform hold = inv.transform.Find("HoldPoint");
            if (hold == null)
                return;

            Pick pick = marker.GetComponent<Pick>();
            if (pick == null)
                return;

            marker.transform.SetParent(hold);
            marker.transform.position = hold.position;

            Rigidbody2D rb = marker.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            SetColliders(marker, false);

            SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = PlaceableCatalog.Get(PlaceableType.Box).sortingOrder;

            pick.isHolding = true;
            inv.SetCarriedBox(pick);

            // Enable Pick one frame later, so the E press that took the box does not
            // also get seen by Pick and drop it again immediately.
            StartCoroutine(EnablePickNextFrame(pick));
        }

        private IEnumerator EnablePickNextFrame(Pick pick)
        {
            yield return null;
            if (pick != null)
                pick.enabled = true;
        }

        private static void SetColliders(LevelObjectMarker marker, bool enabled)
        {
            Collider2D[] cols = marker.GetComponents<Collider2D>();
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = enabled;
        }
    }
}
