using System.Collections.Generic;
using UnityEngine;

// Клас, який обробляє всі дії гравця: кліки мишкою, вибір фішок, 
// підсвічування доступних ходів та процес встановлення стін.
public class InputController : MonoBehaviour
{
    public GameManager gm; // Посилання на головний менеджер гри
    public BoardGraph board; // Посилання на логіку графа (поле)

    [Header("Кольори")]
    public Color highlightColor = Color.cyan; // Колір для підсвічування доступних для ходу клітинок
    public Color wallHoverColor = Color.blue; // Колір напівпрозорої стіни під час прицілювання (наведення мишкою)
    private Color normalTileColor = Color.black; // Стандартний колір клітинки

    // Змінні стану (що зараз робить гравець)
    private GameObject selectedPiece; // Фішка, яку зараз обрав гравець для ходу
    private GameObject hoveredAnchor; // Точка (якір) для стіни, на яку зараз наведена мишка
    private GameObject wallToDestroy; // Стіна із "запасу" гравця (UI на краях поля), яку він обрав для встановлення
    public bool isConstructionMode = false; // Чи знаходиться гравець у режимі встановлення стіни
    public bool isVertical = false; // Поточна орієнтація стіни в руках (вертикальна чи горизонтальна)

    // Список клітинок, які зараз підсвічені (щоб знати, з яких потім знімати підсвічування)
    private List<Renderer> currentHighlightedTiles = new List<Renderer>();

    void Update()
    {
        // Якщо зараз хід бота (ШІ), блокуємо будь-яке введення гравця, щоб він не міг втрутитися
        if (gm.currentPlayer.strategy != null) return;

        // Обробка прокрутки коліщатка миші для обертання стіни під час прицілювання
        if (isConstructionMode && Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            isVertical = !isVertical; // Змінюємо орієнтацію на протилежну
            if (hoveredAnchor != null) ApplyWallVisuals(hoveredAnchor, 0.5f); // Оновлюємо візуалізацію відразу
        }

        // Налаштування шарів для Raycast (променя від мишки). 
        // Якщо ми ставимо стіну (~0 означає всі шари), інакше ігноруємо шар "ignoreRaycast"
        int layerMask = isConstructionMode ? ~0 : ~(1 << LayerMask.NameToLayer("ignoreRaycast"));

        // Створюємо промінь від камери в точку, де зараз знаходиться курсор миші
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Пускаємо промінь на 100 одиниць уперед
        if (Physics.Raycast(ray, out hit, 100f, layerMask))
        {
            GameObject clickedObj = hit.collider.gameObject; // Об'єкт, у який влучив промінь

            // --- ЛОГІКА НАВЕДЕННЯ МИШКОЮ (HOVER) ДЛЯ СТІН ---
            if (isConstructionMode && clickedObj.CompareTag("WallAnchor"))
            {
                // Якщо мишка перемістилася на НОВИЙ якір
                if (hoveredAnchor != clickedObj)
                {
                    ResetHover(); // Скидаємо підсвічування з попереднього якоря
                    // Перевіряємо, чи дозволяють правила поставити стіну в цьому місці
                    if (CanPlaceWall(clickedObj, isVertical))
                    {
                        hoveredAnchor = clickedObj;
                        // Робимо стіну напівпрозорою, щоб гравець бачив, як вона буде стояти (alpha 0.5)
                        ApplyWallVisuals(hoveredAnchor, 0.5f);
                    }
                }
            }
            else ResetHover(); // Якщо мишка не на якорі - прибираємо напівпрозору стіну

            // --- ЛОГІКА КЛІКУ (ЛІВА КНОПКА МИШІ) ---
            if (Input.GetMouseButtonDown(0))
            {
                // 1. Гравець клікнув на стіну в своєму запасі (WallStock)
                if (clickedObj.CompareTag("WallStock"))
                {
                    // Перевіряємо, чи належить ця стіна саме поточному гравцю
                    if (gm.currentPlayer.wallStock.Contains(clickedObj))
                        PrepareToPlaceWall(clickedObj); // Вмикаємо режим будівництва
                }
                // 2. Гравець клікнув на якір, щоб ПІДТВЕРДИТИ встановлення стіни
                else if (isConstructionMode && clickedObj.CompareTag("WallAnchor"))
                {
                    if (CanPlaceWall(clickedObj, isVertical))
                    {
                        // 1. Повідомляємо GameManager, що стіна встановлена
                        gm.ConfirmWallPlacement(clickedObj, isVertical, wallToDestroy);

                        // 2. Виправляємо матеріали та шейдери, щоб стіна стала фізично непрозорою
                        Renderer rend = clickedObj.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            // Встановлюємо білий колір
                            rend.material.color = Color.white;

                            // ПОВНЕ ПЕРЕКЛЮЧЕННЯ ШЕЙДЕРА В ОПАК (НЕПРОЗОРИЙ) РЕЖИМ
                            // Це потрібно, щоб після підтвердження встановлення стіна перестала бути "привидом"
                            rend.material.SetFloat("_Mode", 0); // 0 = Opaque (Непрозорий)
                            rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                            rend.material.SetInt("_ZWrite", 1);
                            rend.material.DisableKeyword("_ALPHATEST_ON");
                            rend.material.DisableKeyword("_ALPHABLEND_ON");
                            rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            rend.material.renderQueue = -1;
                        }

                        // 3. Скидаємо всі стани, бо хід зроблено
                        wallToDestroy = null;
                        hoveredAnchor = null;
                        isConstructionMode = false;
                    }
                }
                // 3. Гравець клікнув на свою фішку
                else if (clickedObj.CompareTag("Player") && clickedObj == gm.currentPlayer.playerObj)
                {
                    // Готуємося до руху (підсвічуємо доступні клітинки)
                    PrepareToMovePiece(clickedObj);
                }
                // 4. Гравець клікнув на клітинку (Tile) поля для переміщення
                else if (selectedPiece != null && clickedObj.CompareTag("Tile"))
                {
                    // Перевіряємо, чи є ця клітинка в списку доступних для ходу (чи вона підсвічена)
                    if (currentHighlightedTiles.Contains(clickedObj.GetComponent<Renderer>()))
                    {
                        // Даємо команду менеджеру перемістити фішку в центр вибраної клітинки
                        gm.MovePiece(selectedPiece, hit.collider.bounds.center);
                        ResetAllStates(); // Очищаємо стан після ходу
                    }
                }
                // 5. Гравець клікнув у порожнечу (або на недоступний об'єкт)
                else ResetAllStates(); // Скасовуємо вибір
            }
        }
        else if (isConstructionMode) ResetHover(); // Якщо промінь взагалі нікуди не влучив (мишка в небі)
    }

    // Головне правило Кворидору: перевірка, чи можна поставити стіну тут
    bool CanPlaceWall(GameObject anchor, bool vertical)
    {
        // 1. Спочатку перевіряємо фізичні обмеження (чи не перетинається з іншими вже встановленими стінками)
        if (!board.PassesPhysicalWallChecks(anchor, vertical)) return false;

        // 2. Отримуємо список ребер (зв'язків між клітинками), які ця стінка хоче перекрити
        var blockedEdges = board.GetEdgesBlockedByWall(anchor.transform.position, vertical);
        if (blockedEdges.Count == 0) return true; // Якщо нічого не перекриває (край поля) - можна ставити

        // 3. Тимчасово видаляємо ці зв'язки з графа, щоб симулювати наявність стіни
        foreach (var edge in blockedEdges)
        {
            if (board.adjacencyList.ContainsKey(edge[0])) board.adjacencyList[edge[0]].Remove(edge[1]);
            if (board.adjacencyList.ContainsKey(edge[1])) board.adjacencyList[edge[1]].Remove(edge[0]);
        }

        // 4. ПЕРЕВІРКА ШЛЯХІВ: Проходимо по всіх гравцях у списку (і реальних, і ШІ)
        bool allPathsExist = true;
        foreach (var player in gm.allPlayers)
        {
            // Шукаємо клітинку, на якій зараз стоїть гравець
            GameObject playerTile = board.GetTileUnder(player.playerObj);

            // АЛГОРИТМ: Якщо хоча б один гравець після встановлення стіни не може дійти 
            // до жодної зі своїх цілей (його заблокували) — таку стінку ставити ЗАБОРОНЕНО.
            if (!board.HasPath(playerTile, player.goals))
            {
                allPathsExist = false;
                break; // Перериваємо цикл, бо однієї заблокованої людини достатньо для заборони
            }
        }

        // 5. Повертаємо зв'язки (ребра) назад у граф (відкатуємо зміни, адже стіна ще фізично не встановлена)
        foreach (var edge in blockedEdges)
        {
            if (board.adjacencyList.ContainsKey(edge[0]) && !board.adjacencyList[edge[0]].Contains(edge[1]))
                board.adjacencyList[edge[0]].Add(edge[1]);

            if (board.adjacencyList.ContainsKey(edge[1]) && !board.adjacencyList[edge[1]].Contains(edge[0]))
                board.adjacencyList[edge[1]].Add(edge[0]);
        }

        // 6. Повертаємо результат: true, якщо всі гравці мають шлях (можна ставити), і false, якщо шлях перекрито
        return allPathsExist;
    }

    // Відповідає за зміну зовнішнього вигляду точки кріплення стіни (прозорість, колір, поворот)
    void ApplyWallVisuals(GameObject obj, float alpha)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            // Якщо alpha < 1, це режим "прицілювання" (колір wallHoverColor). Якщо 1 - стіна встановлена.
            Color baseColor = (alpha < 1f) ? wallHoverColor : Color.white;
            rend.material.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            // Черга рендеру: прозорі об'єкти рендеряться пізніше (3000), непрозорі - раніше (2000)
            rend.material.renderQueue = (alpha < 1f) ? 3000 : 2000;
        }
        // Задаємо фізичний поворот об'єкта залежно від орієнтації (90 градусів по потрібній осі)
        obj.transform.localEulerAngles = isVertical ? new Vector3(90, 90, 0) : new Vector3(90, 0, 0);
    }

    // Підготовчі методи при кліках
    // Готуємося ходити: скидаємо все, запам'ятовуємо обрану фішку і малюємо можливі ходи
    void PrepareToMovePiece(GameObject piece) { ResetAllStates(); selectedPiece = piece; HighlightPossibleMoves(); }

    // Готуємося ставити стіну: скидаємо все, вмикаємо режим будівництва і підсвічуємо обрану стіну в запасі
    void PrepareToPlaceWall(GameObject stockWall) { ResetAllStates(); isConstructionMode = true; wallToDestroy = stockWall; wallToDestroy.GetComponent<Renderer>().material.color = wallHoverColor; }

    // Скидає стан "прицілювання" стіни (робить якір знову невидимим)
    public void ResetHover() { if (hoveredAnchor != null) { hoveredAnchor.GetComponent<Renderer>().material.color = new Color(1, 1, 1, 0); hoveredAnchor = null; } }

    // Глобальне скидання: скасовує будь-яку дію (відміняє вибір стіни або фішки, стирає підсвічування)
    public void ResetAllStates()
    {
        if (wallToDestroy != null) { wallToDestroy.GetComponent<Renderer>().material.color = Color.white; wallToDestroy = null; }
        isConstructionMode = false;
        ResetHover();
        ClearHighlights();
        selectedPiece = null;
    }

    // Логіка підсвічування клітинок, куди може походити гравець
    void HighlightPossibleMoves()
    {
        ClearHighlights(); // Спочатку очищаємо старе підсвічування

        // Знаходимо клітинку під обраною фішкою
        GameObject currentTile = board.GetTileUnder(selectedPiece);
        // Якщо клітинки немає або її немає в графі - виходимо
        if (currentTile == null || !board.adjacencyList.ContainsKey(currentTile)) return;

        // Перебираємо всіх сусідів цієї клітинки в графі (тих, що не заблоковані стінами)
        foreach (GameObject neighbor in board.adjacencyList[currentTile])
        {
            // Якщо сусідня клітинка зайнята іншим гравцем
            if (board.IsTileOccupied(neighbor.transform.position, selectedPiece))
            {
                // Намагаємося виконати стрибок через гравця (за правилами Кворидору)
                GameObject jumpTile = board.GetJumpTileInGraph(currentTile, neighbor);
                // Якщо є куди приземлитися після стрибка — підсвічуємо ту клітинку
                if (jumpTile != null) ApplyHighlight(jumpTile.GetComponent<Renderer>());
            }
            else
            {
                // Якщо клітинка вільна - просто підсвічуємо її
                ApplyHighlight(neighbor.GetComponent<Renderer>());
            }
        }
    }

    // Очищає список підсвічених клітинок, повертаючи їм стандартний колір
    public void ClearHighlights()
    {
        foreach (Renderer r in currentHighlightedTiles) if (r != null) r.material.color = normalTileColor;
        currentHighlightedTiles.Clear();
    }

    // Додає клітинку до списку підсвічених та застосовує колір підсвічування
    void ApplyHighlight(Renderer r) { if (r != null && !currentHighlightedTiles.Contains(r)) { currentHighlightedTiles.Add(r); r.material.color = highlightColor; } }
}
