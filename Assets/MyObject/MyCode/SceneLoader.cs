using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // Перехід за назвою сцени
    public void LoadSceneByName(string sceneName)
    {
        // Відновлення часу
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
