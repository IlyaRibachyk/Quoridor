using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Клас, що реалізує логіку штучного інтелекту для керування ботом у грі.
// Успадковує MonoBehaviour для роботи в Unity та реалізує інтерфейс IPlayerStrategy.
public class AIBotStrategy : MonoBehaviour, IPlayerStrategy
{
    // Посилання на головні менеджери гри
    private GameManager gm;
    private BoardGraph board;

    // Дані гравців: p1Data - це гравець-людина (опонент), p2Data - це сам бот
    private PlayerData p1Data;
    private PlayerData p2Data;

    // Налаштування для алгоритму Монте-Карло (пошук дерева варіантів)
    public int mcRollouts = 40;     // Кількість симуляцій (програвань гри до кінця)
    public int mcPlies = 10;        // Глибина ходів у кожній симуляції
    public float mcWallWeight = 1.0f; // Ваговий коефіцієнт для встановлення стін

    // Метод ініціалізації бота при старті гри
    public void Init(GameManager gameManager, BoardGraph boardGraph)
    {
        gm = gameManager;
        board = boardGraph;

        // Шукаємо, який саме гравець зі списку GameManager використовує цей скрипт
        foreach (var p in gm.allPlayers)
        {
            if (p.strategy == (IPlayerStrategy)this)
            {
                p2Data = p; // Цей конкретний бот тепер "p2Data" для своєї логіки
                break;
            }
        }

        // Опонентом для розрахунків вважаємо Гравця 1 (індекс 0 у списку)
        if (gm.allPlayers.Count > 0)
        {
            p1Data = gm.allPlayers[0];
        }
    }

    // Викликається GameManager-ом, коли настає хід бота
    public void ExecuteTurn()
    {
        // Запускаємо корутину, щоб ШІ думав асинхронно і гра не "зависала"
        StartCoroutine(ExecuteAIMoveCoroutine());
    }

    // Основна корутина мислення бота
    IEnumerator ExecuteAIMoveCoroutine()
    {
        // Невелика затримка для реалістичності (ніби бот думає)
        yield return new WaitForSeconds(0.5f);

        Move bestMove = null;
        float bestScore = float.MinValue;

        // Отримуємо всі можливі логічні ходи для бота (рух + встановлення стін)
        List<Move> allMoves = GetSmartMoves();

        // Екстрена перевірка (Режим паніки): якщо це гра проти бота, є стіни, опонент за крок від перемоги, а ми не можемо виграти зараз
        if (gm.currentMode == GameManager.GameMode.PvE && p2Data.wallStock.Count > 0 && IsP1OneStepFromGoal() && !CanP2WinThisTurn())
        {
            // Відфільтровуємо лише ті ходи, які ставлять стіни
            List<Move> wallMoves = allMoves.FindAll(m => m.isWall);
            // Залишаємо лише ті стіни, які реально завадять опоненту
            wallMoves = wallMoves.FindAll(IsHelpfulWallForPanic);
            // Якщо такі рятівні стіни є, розглядаємо тільки їх
            if (wallMoves.Count > 0) allMoves = wallMoves;
        }

        int currentCalc = 0; // Лічильник обчислень для запобігання зависанню кадру

        // Перебираємо всі можливі ходи для оцінки
        foreach (Move move in allMoves)
        {
            // Тимчасово застосовуємо хід до ігрового світу
            ApplyMoveLogic(move, true);
            GameObject p1Tile = board.GetTileUnder(p1Data.playerObj);
            GameObject p2Tile = board.GetTileUnder(p2Data.playerObj);

            if (p1Tile != null && p2Tile != null)
            {
                float score;
                // Якщо хід — це встановлення стіни, оцінюємо його методом Монте-Карло
                if (move.isWall)
                {
                    var graphAfterWall = board.CloneGraph(board.adjacencyList);
                    GameObject p1TileAfter = board.GetTileUnder(p1Data.playerObj);
                    GameObject p2TileAfter = board.GetTileUnder(p2Data.playerObj);
                    score = MonteCarloScoreForWall(graphAfterWall, p1TileAfter, p2TileAfter);
                }
                // Якщо хід — це переміщення фішки, використовуємо алгоритм Minimax
                else
                {
                    var graphCopy = board.CloneGraph(board.adjacencyList);
                    score = Minimax(1, float.MinValue, float.MaxValue, false, p1Tile, p2Tile, graphCopy);
                }

                // Зберігаємо хід, якщо він кращий за попередні
                if (score > bestScore) { bestScore = score; bestMove = move; }
            }

            // Скасовуємо тимчасовий хід
            ApplyMoveLogic(move, false);

            currentCalc++;
            // Кожні 10 обчислень робимо паузу на 1 кадр, щоб Unity не підвисав
            if (currentCalc >= 10) { currentCalc = 0; yield return null; }
        }

        // Виконуємо найкращий знайдений хід, або просто передаємо хід, якщо ходів немає
        if (bestMove != null) RealizeMove(bestMove);
        else gm.SwitchTurn();
    }

    // Алгоритм Minimax з альфа-бета відсіканням для прорахунку ходів фішок на кілька кроків вперед
    float Minimax(int depth, float alpha, float beta, bool isMaximizing, GameObject p1Tile, GameObject p2Tile, Dictionary<GameObject, List<GameObject>> graph)
    {
        // Базовий випадок: досягли ліміту глибини, повертаємо статичну оцінку поля
        if (depth == 0) return EvaluateBoard(p1Tile, p2Tile, graph);

        if (isMaximizing) // Хід нашого бота (прагнемо максимального результату)
        {
            float maxEval = float.MinValue;
            foreach (Move m in GetSmartMovesSimulated(p2Tile, graph))
            {
                var newGraph = board.CloneGraph(graph);
                GameObject newP2Tile = p2Tile;
                ApplyMoveToGraph(m, newGraph);
                if (!m.isWall) newP2Tile = m.targetTile;

                // Рекурсивний виклик для ходу опонента
                float eval = Minimax(depth - 1, alpha, beta, false, p1Tile, newP2Tile, newGraph);
                maxEval = Mathf.Max(maxEval, eval);
                alpha = Mathf.Max(alpha, eval);
                if (beta <= alpha) break; // Альфа-бета відсікання зайвих гілок
            }
            return maxEval;
        }
        else // Хід опонента (прагнемо мінімального результату для нас)
        {
            float minEval = float.MaxValue;
            foreach (Move m in GetSmartMovesSimulated(p1Tile, graph))
            {
                var newGraph = board.CloneGraph(graph);
                GameObject newP1Tile = p1Tile;
                ApplyMoveToGraph(m, newGraph);
                if (!m.isWall) newP1Tile = m.targetTile;

                // Рекурсивний виклик для нашого наступного ходу
                float eval = Minimax(depth - 1, alpha, beta, true, newP1Tile, p2Tile, newGraph);
                minEval = Mathf.Min(minEval, eval);
                beta = Mathf.Min(beta, eval);
                if (beta <= alpha) break; // Альфа-бета відсікання зайвих гілок
            }
            return minEval;
        }
    }

    // Оцінка якості стіни за допомогою алгоритму Монте-Карло (випадкові симуляції гри)
    float MonteCarloScoreForWall(Dictionary<GameObject, List<GameObject>> graph, GameObject p1TileStart, GameObject p2TileStart)
    {
        if (p1TileStart == null || p2TileStart == null) return float.MinValue;
        float total = 0f;

        // Робимо задану кількість симуляцій-програвань
        for (int i = 0; i < mcRollouts; i++)
        {
            var g = board.CloneGraph(graph);
            GameObject p1 = p1TileStart, p2 = p2TileStart;
            bool p1Turn = true;

            // Відіграємо випадкові ходи на задану глибину пліїв (напівходів)
            for (int ply = 0; ply < mcPlies; ply++)
            {
                if (p1Turn) { p1 = ChooseMonteCarloPawnMove(p1, p1Data.goals, g); if (p1Data.goals.Contains(p1)) break; }
                else { p2 = ChooseMonteCarloPawnMove(p2, p2Data.goals, g); if (p2Data.goals.Contains(p2)) break; }
                p1Turn = !p1Turn;
            }
            // Додаємо оцінку стану дошки після симуляції до загальної суми
            total += EvaluateBoard(p1, p2, g);
        }
        // Повертаємо середнє значення якості ходу
        return total / mcRollouts;
    }

    // Допоміжний метод для Монте-Карло: вибір ходу фішки в симуляції (рухається до мети)
    GameObject ChooseMonteCarloPawnMove(GameObject from, List<GameObject> goals, Dictionary<GameObject, List<GameObject>> graph)
    {
        if (from == null || !graph.ContainsKey(from)) return from;
        var neighbors = graph[from];
        if (neighbors == null || neighbors.Count == 0) return from;

        int bestDist = int.MaxValue;
        List<GameObject> best = new List<GameObject>();

        // Шукаємо сусідню клітинку, з якої шлях до фінішу найкоротший
        foreach (var n in neighbors)
        {
            int d = board.GetShortestPathLengthSim(n, goals, graph);
            if (d == -1) continue;

            if (d < bestDist) { bestDist = d; best.Clear(); best.Add(n); }
            else if (d == bestDist) best.Add(n);
        }

        // Якщо шлях не знайдено, робимо випадковий хід. Інакше - випадковий серед найкращих.
        if (best.Count == 0) return neighbors[Random.Range(0, neighbors.Count)];
        return best[Random.Range(0, best.Count)];
    }

    // Евристична функція: оцінює поточний стан дошки. 
    // Чим більший результат, тим вигідніша ситуація для бота.
    private float EvaluateBoard(GameObject p1Tile, GameObject p2Tile, Dictionary<GameObject, List<GameObject>> graph)
    {
        int p1Path = board.GetShortestPathLengthSim(p1Tile, p1Data.goals, graph);
        int p2Path = board.GetShortestPathLengthSim(p2Tile, p2Data.goals, graph);
        if (p1Path == -1) return 1000f; // Опонент заблокований (бот перемагає)
        if (p2Path == -1) return -1000f;// Бот заблокований (дуже погано)
        // Формула: опонент має йти довго, а ми - швидко. Множник 2f додає пріоритет перешкоджанню гравцю.
        return (p1Path * 2f) - p2Path;
    }

    // Повертає список можливих переміщень по симульованому графу
    private List<Move> GetSmartMovesSimulated(GameObject tile, Dictionary<GameObject, List<GameObject>> graph)
    {
        List<Move> moves = new List<Move>();
        if (!graph.ContainsKey(tile)) return moves;
        foreach (var n in graph[tile]) moves.Add(new Move { isWall = false, targetTile = n, position = n.transform.position });
        return moves;
    }

    // Фізично застосовує (або скасовує) хід у грі для перевірки його наслідків
    private void ApplyMoveLogic(Move m, bool apply)
    {
        if (m.isWall) // Якщо це встановлення стіни
        {
            if (apply)
            {
                // Видаляємо зв'язки в графі, які перекриває стіна
                m.removedEdges.Clear();
                var edges = board.GetEdgesBlockedByWall(m.position, m.vertical);
                foreach (var edge in edges)
                {
                    GameObject t1 = edge[0], t2 = edge[1];
                    if (board.adjacencyList.ContainsKey(t1) && board.adjacencyList[t1].Contains(t2))
                    {
                        board.adjacencyList[t1].Remove(t2); board.adjacencyList[t2].Remove(t1);
                        m.removedEdges.Add(new GameObject[] { t1, t2 });
                    }
                }
            }
            else // Скасовуємо стіну (відновлюємо зв'язки)
            {
                foreach (var edge in m.removedEdges)
                {
                    GameObject t1 = edge[0], t2 = edge[1];
                    if (t1 != null && t2 != null)
                    {
                        if (!board.adjacencyList[t1].Contains(t2)) board.adjacencyList[t1].Add(t2);
                        if (!board.adjacencyList[t2].Contains(t1)) board.adjacencyList[t2].Add(t1);
                    }
                }
                m.removedEdges.Clear();
            }
        }
        else // Якщо це рух фішки
        {
            if (apply)
            {
                // Переміщуємо фішку бота на цільову клітинку
                m.previousPosition = p2Data.playerObj.transform.position;
                p2Data.playerObj.transform.position = m.targetTile.transform.position + Vector3.up * gm.tileHeightOffset;
            }
            // Повертаємо фішку на попередню позицію
            else p2Data.playerObj.transform.position = m.previousPosition;
            Physics.SyncTransforms(); // Оновлюємо фізику Unity
        }
    }

    // Застосовує перекриття стіною до віртуальної копії графа (для алгоритмів)
    private void ApplyMoveToGraph(Move m, Dictionary<GameObject, List<GameObject>> graph)
    {
        if (!m.isWall) return;
        var edges = board.GetEdgesBlockedByWall(m.position, m.vertical);
        foreach (var edge in edges)
        {
            if (graph.ContainsKey(edge[0])) graph[edge[0]].Remove(edge[1]);
            if (graph.ContainsKey(edge[1])) graph[edge[1]].Remove(edge[0]);
        }
    }

    // Генерує список всіх логічних ходів для ШІ (тільки розумні ходи)
    private List<Move> GetSmartMoves()
    {
        List<Move> smartMoves = new List<Move>();
        GameObject currentTile = board.GetTileUnder(p2Data.playerObj);

        if (currentTile != null && board.adjacencyList.ContainsKey(currentTile))
        {
            // Перебираємо всі сусідні клітинки для ходу
            foreach (GameObject neighbor in board.adjacencyList[currentTile])
            {
                // Якщо сусідня клітинка зайнята опонентом, пробуємо перестрибнути
                if (board.IsTileOccupied(neighbor.transform.position, p2Data.playerObj))
                {
                    GameObject jumpTile = board.GetJumpTileInGraph(currentTile, neighbor);
                    if (jumpTile != null) smartMoves.Add(new Move { isWall = false, position = jumpTile.transform.position, targetTile = jumpTile });
                }
                // Інакше додаємо звичайний хід
                else smartMoves.Add(new Move { isWall = false, position = neighbor.transform.position, targetTile = neighbor });
            }
        }

        // Генеруємо можливі встановлення стін
        if (p2Data.wallStock.Count > 0)
        {
            foreach (GameObject anchor in board.allAnchors)
            {
                if (anchor == null || !anchor.CompareTag("WallAnchor")) continue;
                // Для оптимізації розглядаємо стіни лише поруч із опонентом (в радіусі 2.5)
                if (Vector3.Distance(anchor.transform.position, p1Data.playerObj.transform.position) < 2.5f)
                {
                    CheckAndAddWall(smartMoves, anchor, true);  // Вертикальна
                    CheckAndAddWall(smartMoves, anchor, false); // Горизонтальна
                }
            }
        }
        return smartMoves;
    }

    // Перевіряє чи можна поставити стіну, і якщо так - додає до списку ходів
    private void CheckAndAddWall(List<Move> list, GameObject anchor, bool vertical)
    {
        var graphCopy = board.CloneGraph(board.adjacencyList);
        if (CanPlaceWallSim(anchor, vertical, graphCopy))
            list.Add(new Move { isWall = true, position = anchor.transform.position, vertical = vertical });
    }

    // Симуляційна перевірка валідності встановлення стіни
    bool CanPlaceWallSim(GameObject anchor, bool vertical, Dictionary<GameObject, List<GameObject>> graph)
    {
        // Перевіряємо фізичні перетини з іншими стінами
        if (!board.PassesPhysicalWallChecks(anchor, vertical)) return false;
        var blockedEdges = board.GetEdgesBlockedByWall(anchor.transform.position, vertical);
        if (blockedEdges.Count == 0) return true;

        // Перевіряємо чи стіна не перекриває шлях до фінішу повністю (правило гри)
        foreach (var edge in blockedEdges)
        {
            if (graph.ContainsKey(edge[0])) graph[edge[0]].Remove(edge[1]);
            if (graph.ContainsKey(edge[1])) graph[edge[1]].Remove(edge[0]);
        }
        return board.HasPathSim(board.GetTileUnder(p1Data.playerObj), p1Data.goals, graph) &&
               board.HasPathSim(board.GetTileUnder(p2Data.playerObj), p2Data.goals, graph);
    }

    // Виконує остаточно обраний хід у самій грі
    private void RealizeMove(Move m)
    {
        if (m.isWall) // Бот ставить стіну
        {
            GameObject wallFromStock = (p2Data.wallStock.Count > 0) ? p2Data.wallStock[0] : null;
            if (wallFromStock == null) return;

            GameObject anchor = null;
            // Шукаємо якір (точку кріплення) для стіни на сцені
            foreach (GameObject a in board.allAnchors)
            {
                if (Vector3.Distance(a.transform.position, m.position) < 0.1f) { anchor = a; break; }
            }

            if (anchor != null)
            {
                // Обертаємо якір та підтверджуємо встановлення
                anchor.transform.localEulerAngles = m.vertical ? new Vector3(90, 90, 0) : new Vector3(90, 0, 0);
                gm.ConfirmWallPlacement(anchor, m.vertical, wallFromStock);
            }
        }
        else // Бот переміщує фішку
        {
            gm.MovePiece(p2Data.playerObj, m.position);
        }

        gm.inputController.ResetAllStates(); // Скидаємо стан контролера після ходу
    }

    // Перевіряє, чи може бот виграти прямісінько в цей хід
    bool CanP2WinThisTurn()
    {
        GameObject p2Tile = board.GetTileUnder(p2Data.playerObj);
        if (p2Tile == null) return false;
        if (p2Data.goals.Contains(p2Tile)) return true;
        if (!board.adjacencyList.ContainsKey(p2Tile)) return false;
        foreach (var n in board.adjacencyList[p2Tile]) if (p2Data.goals.Contains(n)) return true;
        return false;
    }

    // Перевіряє, чи залишився опоненту (Гравцю 1) всього 1 крок до перемоги
    bool IsP1OneStepFromGoal()
    {
        GameObject p1Tile = board.GetTileUnder(p1Data.playerObj);
        if (p1Tile == null) return false;
        return board.GetShortestPathLengthSim(p1Tile, p1Data.goals, board.adjacencyList) == 1;
    }

    // Перевіряє, чи реально конкретна стіна допомагає заблокувати опонента в екстреній ситуації
    bool IsHelpfulWallForPanic(Move wallMove)
    {
        GameObject p1Tile = board.GetTileUnder(p1Data.playerObj);
        GameObject p2Tile = board.GetTileUnder(p2Data.playerObj);
        if (p1Tile == null || p2Tile == null) return false;

        // Заміряємо шляхи ДО встановлення стіни
        int beforeP1 = board.GetShortestPathLengthSim(p1Tile, p1Data.goals, board.adjacencyList);
        int beforeP2 = board.GetShortestPathLengthSim(p2Tile, p2Data.goals, board.adjacencyList);
        if (beforeP1 == -1 || beforeP2 == -1) return false;

        // Заміряємо шляхи ПІСЛЯ встановлення стіни
        ApplyMoveLogic(wallMove, true);
        int afterP1 = board.GetShortestPathLengthSim(p1Tile, p1Data.goals, board.adjacencyList);
        int afterP2 = board.GetShortestPathLengthSim(p2Tile, p2Data.goals, board.adjacencyList);
        ApplyMoveLogic(wallMove, false);

        if (afterP1 == -1 || afterP2 == -1) return false;
        if (afterP1 <= beforeP1) return false;     // Якщо шлях гравця не подовжився - стіна марна
        if (afterP2 > beforeP2 + 1) return false;  // Якщо ми заблокували самі себе сильніше - стіна погана

        return true; // Стіна визнається корисною
    }
}
