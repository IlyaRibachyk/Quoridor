using System.Collections.Generic;
using UnityEngine;

public class InputController : MonoBehaviour
{
    public GameManager gm;
    public BoardGraph board;

    [Header("Кольори")]
    public Color highlightColor = Color.cyan;
    public Color wallHoverColor = Color.blue;
    private Color normalTileColor = Color.black; // Або зчитай у Start

    private GameObject selectedPiece;
    private GameObject hoveredAnchor;
    private GameObject wallToDestroy;
    public bool isConstructionMode = false;
    public bool isVertical = false;

    private List<Renderer> currentHighlightedTiles = new List<Renderer>();

    void Update()
    {
        if (gm.currentPlayer.strategy != null) return;

        if (isConstructionMode && Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            isVertical = !isVertical;
            if (hoveredAnchor != null) ApplyWallVisuals(hoveredAnchor, 0.5f);
        }

        int layerMask = isConstructionMode ? ~0 : ~(1 << LayerMask.NameToLayer("ignoreRaycast"));
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, layerMask))
        {
            GameObject clickedObj = hit.collider.gameObject;

            if (isConstructionMode && clickedObj.CompareTag("WallAnchor"))
            {
                if (hoveredAnchor != clickedObj)
                {
                    ResetHover();
                    if (CanPlaceWall(clickedObj, isVertical))
                    {
                        hoveredAnchor = clickedObj;
                        ApplyWallVisuals(hoveredAnchor, 0.5f);
                    }
                }
            }
            else ResetHover();

            if (Input.GetMouseButtonDown(0))
            {
                if (clickedObj.CompareTag("WallStock"))
                {
                    if (gm.currentPlayer.wallStock.Contains(clickedObj))
                        PrepareToPlaceWall(clickedObj);
                }
                else if (isConstructionMode && clickedObj.CompareTag("WallAnchor"))
                {
                    if (CanPlaceWall(clickedObj, isVertical))
                    {
                        // 1. Ставимо стінку
                        gm.ConfirmWallPlacement(clickedObj, isVertical, wallToDestroy);

                        // 2. Виправляємо видимість
                        Renderer rend = clickedObj.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            // Встановлюємо білий колір
                            rend.material.color = Color.white;

                            // ПОВНЕ ПЕРЕКЛЮЧЕННЯ ШЕЙДЕРА В ОПАК (НЕПРОЗОРИЙ) РЕЖИМ
                            rend.material.SetFloat("_Mode", 0); // 0 = Opaque
                            rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                            rend.material.SetInt("_ZWrite", 1);
                            rend.material.DisableKeyword("_ALPHATEST_ON");
                            rend.material.DisableKeyword("_ALPHABLEND_ON");
                            rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            rend.material.renderQueue = -1;
                        }

                        // 3. Скидаємо стани
                        wallToDestroy = null;
                        hoveredAnchor = null;
                        isConstructionMode = false;
                    }
                }
                else if (clickedObj.CompareTag("Player") && clickedObj == gm.currentPlayer.playerObj)
                {
                    PrepareToMovePiece(clickedObj);
                }
                else if (selectedPiece != null && clickedObj.CompareTag("Tile"))
                {
                    if (currentHighlightedTiles.Contains(clickedObj.GetComponent<Renderer>()))
                    {
                        gm.MovePiece(selectedPiece, hit.collider.bounds.center);
                        ResetAllStates();
                    }
                }
                else ResetAllStates();
            }
        }
        else if (isConstructionMode) ResetHover();
    }

    bool CanPlaceWall(GameObject anchor, bool vertical)
    {
        // 1. Спочатку перевіряємо фізичні обмеження (чи не перетинається з іншими стінками)
        if (!board.PassesPhysicalWallChecks(anchor, vertical)) return false;

        // 2. Отримуємо список ребер (зв'язків між тайлами), які перекриє ця стінка
        var blockedEdges = board.GetEdgesBlockedByWall(anchor.transform.position, vertical);
        if (blockedEdges.Count == 0) return true;

        // 3. Тимчасово видаляємо ці зв'язки з графа, щоб перевірити шлях
        foreach (var edge in blockedEdges)
        {
            if (board.adjacencyList.ContainsKey(edge[0])) board.adjacencyList[edge[0]].Remove(edge[1]);
            if (board.adjacencyList.ContainsKey(edge[1])) board.adjacencyList[edge[1]].Remove(edge[0]);
        }

        // 4. ПЕРЕВІРКА ШЛЯХІВ: Проходимо по всіх гравцях у списку
        bool allPathsExist = true;
        foreach (var player in gm.allPlayers)
        {
            // Шукаємо тайл, на якому зараз стоїть гравець
            GameObject playerTile = board.GetTileUnder(player.playerObj);

            // Якщо хоча б один гравець не може дійти до жодної зі своїх цілей — стінку ставити не можна
            if (!board.HasPath(playerTile, player.goals))
            {
                allPathsExist = false;
                break;
            }
        }

        // 5. Повертаємо зв'язки назад у граф (відкатуємо зміни)
        foreach (var edge in blockedEdges)
        {
            if (board.adjacencyList.ContainsKey(edge[0]) && !board.adjacencyList[edge[0]].Contains(edge[1]))
                board.adjacencyList[edge[0]].Add(edge[1]);

            if (board.adjacencyList.ContainsKey(edge[1]) && !board.adjacencyList[edge[1]].Contains(edge[0]))
                board.adjacencyList[edge[1]].Add(edge[0]);
        }

        // 6. Повертаємо результат: true, якщо всі гравці мають шлях, і false, якщо шлях перекрито
        return allPathsExist;
    }

    void ApplyWallVisuals(GameObject obj, float alpha)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            Color baseColor = (alpha < 1f) ? wallHoverColor : Color.white;
            rend.material.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            rend.material.renderQueue = (alpha < 1f) ? 3000 : 2000;
        }
        obj.transform.localEulerAngles = isVertical ? new Vector3(90, 90, 0) : new Vector3(90, 0, 0);
    }

    void PrepareToMovePiece(GameObject piece) { ResetAllStates(); selectedPiece = piece; HighlightPossibleMoves(); }
    void PrepareToPlaceWall(GameObject stockWall) { ResetAllStates(); isConstructionMode = true; wallToDestroy = stockWall; wallToDestroy.GetComponent<Renderer>().material.color = wallHoverColor; }

    public void ResetHover() { if (hoveredAnchor != null) { hoveredAnchor.GetComponent<Renderer>().material.color = new Color(1, 1, 1, 0); hoveredAnchor = null; } }

    public void ResetAllStates()
    {
        if (wallToDestroy != null) { wallToDestroy.GetComponent<Renderer>().material.color = Color.white; wallToDestroy = null; }
        isConstructionMode = false;
        ResetHover();
        ClearHighlights();
        selectedPiece = null;
    }

    void HighlightPossibleMoves()
    {
        ClearHighlights();
        GameObject currentTile = board.GetTileUnder(selectedPiece);
        if (currentTile == null || !board.adjacencyList.ContainsKey(currentTile)) return;

        foreach (GameObject neighbor in board.adjacencyList[currentTile])
        {
            if (board.IsTileOccupied(neighbor.transform.position, selectedPiece))
            {
                GameObject jumpTile = board.GetJumpTileInGraph(currentTile, neighbor);
                if (jumpTile != null) ApplyHighlight(jumpTile.GetComponent<Renderer>());
            }
            else ApplyHighlight(neighbor.GetComponent<Renderer>());
        }
    }

    public void ClearHighlights()
    {
        foreach (Renderer r in currentHighlightedTiles) if (r != null) r.material.color = normalTileColor;
        currentHighlightedTiles.Clear();
    }

    void ApplyHighlight(Renderer r) { if (r != null && !currentHighlightedTiles.Contains(r)) { currentHighlightedTiles.Add(r); r.material.color = highlightColor; } }
}
