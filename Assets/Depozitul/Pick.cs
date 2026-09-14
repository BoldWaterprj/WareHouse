using UnityEngine;

public class Pick : MonoBehaviour
{
    public Transform playerHoldPoint;
    private bool isPlayerNearby = false;
    public bool isHolding = false;
    private Transform playerTransform;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isHolding)
            {
                DropBox();
            }
            else if (isPlayerNearby)
            {
                PickUpBox();
            }
        }
    }

    void PickUpBox()
    {
        isHolding = true;
        transform.position = playerHoldPoint.position;
        transform.SetParent(playerHoldPoint);

        playerTransform.gameObject.SendMessage("SetCarriedBox", this, SendMessageOptions.DontRequireReceiver);

        Collider2D[] allColliders = GetComponents<Collider2D>();
        foreach (Collider2D col in allColliders)
        {
            col.enabled = false;
        }

        if (GetComponent<Rigidbody2D>() != null)
        {
            GetComponent<Rigidbody2D>().isKinematic = true;
            GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }
    }

    public void DropBox()
    {
        isHolding = false;

        if (transform.parent != null)
        {
            transform.parent.parent.gameObject.SendMessage("SetCarriedBox", null, SendMessageOptions.DontRequireReceiver);
        }

        transform.SetParent(null);

        Collider2D[] allColliders = GetComponents<Collider2D>();
        foreach (Collider2D col in allColliders)
        {
            col.enabled = true;
        }

        if (GetComponent<Rigidbody2D>() != null)
        {
            GetComponent<Rigidbody2D>().isKinematic = false;
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isHolding) return;
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            playerTransform = other.transform;
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (isHolding) return;
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
        }
    }

}

