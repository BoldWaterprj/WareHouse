using UnityEngine;

public class Inventory : MonoBehaviour
{
    public Pick currentCarriedBox;

    public void SetCarriedBox(Pick box)
    {
        currentCarriedBox = box;
    }
}
