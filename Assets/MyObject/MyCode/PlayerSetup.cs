using UnityEngine;
using UnityEngine.UI; // Додайте, якщо фішка — це UI Image

public class PlayerSetup : MonoBehaviour
{
    public bool isPlayer1;
    public bool isPlayer2; // Додайте це поле в інспекторі для другої фішки
    public Material[] allWoodMaterials;

    void Start()
    {
        if (GameSettings.Instance == null) return;

        // Якщо режим на 4 гравці, залишаємо дефолтні кольори (або ставимо свої)
        if (GameSettings.Instance.isFourPlayers)
        {
            // Можна нічого не робити, тоді залишаться кольори, які ви поставили в редакторі
            return;
        }

        // Логіка для 2 гравців (PvP / PvE)
        int index = isPlayer1 ? GameSettings.Instance.p1TextureIndex : GameSettings.Instance.p2TextureIndex;

        if (index >= 0 && index < allWoodMaterials.Length)
        {
            GetComponent<Renderer>().material = allWoodMaterials[index];
        }
    }
}
