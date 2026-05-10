using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public enum GameMode { PvP, PvE }
    public GameMode currentMode = GameMode.PvP;

    public BoardGraph board;
    public InputController inputController;

    [Header("Players")]
    // Тепер використовуємо список для підтримки 2 або 4 гравців
    public List<PlayerData> allPlayers = new List<PlayerData>();
    private int currentPlayerIndex = 0;
    [HideInInspector] public PlayerData currentPlayer;

    [Header("UI & Timer")]
    public GameObject victoryPanel;
    public TextMeshProUGUI victoryText;
    public TurnTimer turnTimer;
    public TextMeshProUGUI turnText;

    [Header("Camera Settings")]
    public Transform mainCameraTransform;
    public float cameraLerpSpeed = 5.0f;
    private int currentCamIndex = 0;
    private Transform targetCamTransform;

    public float tileHeightOffset = 0.72f;

    private GameObject lastHighlightedTile;
    private Color originalColor;
    public Color highlightColor = Color.yellow;
    public Color defaultTileColor = Color.black;

    void Start()
    {
        // 1. Визначаємо режим гри
        int mode = PlayerPrefs.GetInt("GameMode", 0);
        currentMode = (mode == 1) ? GameMode.PvE : GameMode.PvP;

        // 2. Ініціалізація першого гравця
        if (allPlayers.Count > 0)
        {
            currentPlayerIndex = 0;
            currentPlayer = allPlayers[currentPlayerIndex];
        }

        UpdateTurnText();

        // 3. Будуємо граф (передаємо список усіх гравців для розрахунку цілей)
        board.BuildPhysicalGraph(allPlayers);

        // 4. Початкова позиція камери
        if (currentPlayer.cameraPositions != null && currentPlayer.cameraPositions.Count > 0)
        {
            targetCamTransform = currentPlayer.cameraPositions[0];
            mainCameraTransform.position = targetCamTransform.position;
            mainCameraTransform.rotation = targetCamTransform.rotation;
        }

        HideAllAnchors();

        // 5. Налаштування ШІ для всіх, крім першого гравця, якщо це PvE
        if (currentMode == GameMode.PvE)
        {
            for (int i = 1; i < allPlayers.Count; i++)
            {
                allPlayers[i].strategy = GetComponent<AIBotStrategy>();
                if (allPlayers[i].strategy != null)
                    ((AIBotStrategy)allPlayers[i].strategy).Init(this, board);
            }
        }

        UpdatePlayerHighlight();
    }

    void Update()
    {
        // Камера рухається, якщо це PvP АБО якщо зараз хід реального гравця у PvE
        if (currentMode == GameMode.PvP || (currentMode == GameMode.PvE && currentPlayerIndex == 0))
        {
            HandleCameraMovement();
            HandleCameraSwitching();
        }
    }

    public void UpdatePlayerHighlight()
    {
        // 1. Повертаємо стан попередньої плитки (якщо вона була)
        if (lastHighlightedTile != null)
        {
            Renderer lastRenderer = lastHighlightedTile.GetComponent<Renderer>();
            // Повертаємо початковий колір
            lastRenderer.material.color = originalColor;
        }

        // 2. Знаходимо плитку під поточним гравцем
        GameObject currentTile = board.GetTileUnder(allPlayers[currentPlayerIndex].playerObj);

        if (currentTile != null)
        {
            Renderer tileRenderer = currentTile.GetComponent<Renderer>();

            // Запам'ятовуємо плитку та її колір перед зміною
            lastHighlightedTile = currentTile;
            originalColor = tileRenderer.material.color;

            // Просто змінюємо колір плитки на колір підсвічування (без емісії)
            tileRenderer.material.color = highlightColor;
        }
    }

    private void HideAllAnchors()
    {
        GameObject[] anchors = GameObject.FindGameObjectsWithTag("WallAnchor");
        foreach (var a in anchors)
        {
            Renderer rend = a.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(1, 1, 1, 0);
                // Налаштування для прозорості в URP/Built-in
                rend.material.SetFloat("_Surface", 1);
                rend.material.renderQueue = 3000;
            }
        }
    }

    public void SwitchTurn()
    {
        inputController.ClearHighlights();

        // Перехід до наступного гравця по колу
        currentPlayerIndex = (currentPlayerIndex + 1) % allPlayers.Count;
        currentPlayer = allPlayers[currentPlayerIndex];

        UpdateTurnText();
        currentCamIndex = 0;

        // Оновлюємо камеру ТІЛЬКИ якщо це не бот у PvE
        if (currentMode == GameMode.PvP || (currentMode == GameMode.PvE && currentPlayerIndex == 0))
        {
            UpdateTargetCamera();
        }

        inputController.ResetAllStates();

        // Скидання таймера тільки в PvP
        if (currentMode == GameMode.PvP && turnTimer != null)
        {
            turnTimer.ResetTurnTimer();
        }

        // Якщо зараз хід бота — запускаємо стратегію
        if (currentMode == GameMode.PvE && currentPlayerIndex != 0 && currentPlayer.strategy != null)
        {
            currentPlayer.strategy.ExecuteTurn();
        }

        UpdatePlayerHighlight();
    }

    public void MovePiece(GameObject piece, Vector3 targetPos)
    {
        piece.transform.position = targetPos + Vector3.up * tileHeightOffset;
        if (CheckWinCondition()) return;
        SwitchTurn();
    }

    public void ConfirmWallPlacement(GameObject anchor, bool isVertical, GameObject wallToDestroy)
    {
        // 1. Блокуємо ребра в графі
        var blockedEdges = board.GetEdgesBlockedByWall(anchor.transform.position, isVertical);
        foreach (var edge in blockedEdges)
        {
            if (board.adjacencyList.ContainsKey(edge[0])) board.adjacencyList[edge[0]].Remove(edge[1]);
            if (board.adjacencyList.ContainsKey(edge[1])) board.adjacencyList[edge[1]].Remove(edge[0]);
        }

        // 2. Робимо стінку видимою та непрозорою
        Renderer rend = anchor.GetComponent<Renderer>();
        if (rend != null)
        {
            // 1. Робимо колір білим і непрозорим
            rend.material.color = Color.white;

            // 2. Перемикаємо стандартний шейдер Unity в режим Opaque
            // Це критично важливо, щоб прибрати прозорість
            rend.material.SetFloat("_Mode", 0);
            rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            rend.material.SetInt("_ZWrite", 1);
            rend.material.DisableKeyword("_ALPHATEST_ON");
            rend.material.DisableKeyword("_ALPHABLEND_ON");
            rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            rend.material.renderQueue = -1;
        }

        anchor.tag = "Untagged"; // Щоб більше не можна було клікнути

        // 3. Видаляємо стінку з запасу
        if (wallToDestroy != null)
        {
            currentPlayer.wallStock.Remove(wallToDestroy);
            Destroy(wallToDestroy);
        }

        SwitchTurn();
    }

    bool CheckWinCondition()
    {
        GameObject currentTile = board.GetTileUnder(currentPlayer.playerObj);

        if (currentPlayer.goals.Contains(currentTile))
        {
            ShowVictoryMenu();
            return true;
        }
        return false;
    }

    void ShowVictoryMenu()
    {
        victoryPanel.SetActive(true);
        victoryText.text = $"Player {currentPlayerIndex + 1} WON!";
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void UpdateTurnText()
    {
        if (turnText != null)
            turnText.text = $"Turn: Player {currentPlayerIndex + 1}";
    }

    public void SwitchTurnByTimer() { SwitchTurn(); }

    private void HandleCameraMovement()
    {
        if (targetCamTransform == null || mainCameraTransform == null) return;
        mainCameraTransform.position = Vector3.Lerp(mainCameraTransform.position, targetCamTransform.position, Time.deltaTime * cameraLerpSpeed);
        mainCameraTransform.rotation = Quaternion.Lerp(mainCameraTransform.rotation, targetCamTransform.rotation, Time.deltaTime * cameraLerpSpeed);
    }

    private void HandleCameraSwitching()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow)) { currentCamIndex++; UpdateTargetCamera(); }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { currentCamIndex--; UpdateTargetCamera(); }
    }

    private void UpdateTargetCamera()
    {
        var currentList = currentPlayer.cameraPositions;
        if (currentList == null || currentList.Count == 0) return;

        if (currentCamIndex >= currentList.Count) currentCamIndex = 0;
        if (currentCamIndex < 0) currentCamIndex = currentList.Count - 1;

        targetCamTransform = currentList[currentCamIndex];
    }
}
