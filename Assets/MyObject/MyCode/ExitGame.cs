using UnityEngine;

// Вихід з програми
public class ExitGame : MonoBehaviour
{
    public void Quit()
    {
        // Цей рядок закриває вже зібрану гру (build)
        Application.Quit();

        // Цей рядок потрібен лише для перевірки в самому Unity Editor
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
