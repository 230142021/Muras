
using UnityEngine;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    public TMP_Text inventoryText;

    public int AppleCount { get; private set; }

    private void Awake()
    {
        Instance = this;
        UpdateInventoryUI();
    }

    public void AddApple()
    {
        AppleCount++;
        UpdateInventoryUI();

        Debug.Log("Apples: " + AppleCount);
    }

    public bool RemoveApples(int amount)
    {
        if (AppleCount < amount)
            return false;

        AppleCount -= amount;
        UpdateInventoryUI();

        return true;
    }

    private void UpdateInventoryUI()
    {
        if (inventoryText != null)
        {
            inventoryText.text = "Алма  <b>× " + AppleCount + "</b>";
        }
    }
}
