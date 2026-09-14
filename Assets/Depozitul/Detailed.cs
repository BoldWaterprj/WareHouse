using UnityEngine;
using UnityEngine.UI;

public class Detailed : MonoBehaviour
{
    [Header("UI References")]
    public GameObject detailedShelfPanel;
    public Sprite detailedBoxSprite;

    [Header("Distance Settings")]
    public float interactionDistance = 1f;

    private Inventory playerInventory;

    void Start()
    {
        playerInventory = FindFirstObjectByType<Inventory>();
    }

    void Update()
    {
        if (playerInventory == null) return;

        float distance = Vector2.Distance(transform.position, playerInventory.transform.position);

        if (distance <= interactionDistance && Input.GetKeyDown(KeyCode.E))
        {
            if (playerInventory.currentCarriedBox != null && playerInventory.currentCarriedBox.isHolding)
            {
                OpenShelfMenu();
            }
        }

        if (detailedShelfPanel.activeSelf)
        {
            if (distance > interactionDistance || Input.GetKeyDown(KeyCode.Escape))
            {
                CloseShelfMenu();
            }
        }
    }

    void OpenShelfMenu()
    {
        detailedShelfPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseShelfMenu()
    {
        detailedShelfPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void PlaceBoxInSlot(Button clickedButton)
    {
        Image buttonImage = clickedButton.GetComponent<Image>();
        buttonImage.sprite = detailedBoxSprite;
        buttonImage.color = Color.white;

        clickedButton.interactable = false;

        if (playerInventory != null && playerInventory.currentCarriedBox != null)
        {
            Pick boxToDestroy = playerInventory.currentCarriedBox;

            playerInventory.SetCarriedBox(null);

            boxToDestroy.DropBox();
            Destroy(boxToDestroy.gameObject);
        }

        CloseShelfMenu();
    }
}
