using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIBotStrategy : MonoBehaviour, IPlayerStrategy
{
    private GameManager gm;
    private BoardGraph board;

    private PlayerData p1Data; // Опонент
    private PlayerData p2Data;

    public int mcRollouts = 40;
    public int mcPlies = 10;
    public float mcWallWeight = 1.0f;

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

        // Опонентом для розрахунків вважаємо Гравця 1 (індекс 0)
        if (gm.allPlayers.Count > 0)
        {
            p1Data = gm.allPlayers[0];
        }
    }

    public void ExecuteTurn()
    {
        StartCoroutine(ExecuteAIMoveCoroutine());
    }

    IEnumerator ExecuteAIMoveCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        Move bestMove = null;
        float bestScore = float.MinValue;

        List<Move> allMoves = GetSmartMoves();

        if (gm.currentMode == GameManager.GameMode.PvE && p2Data.wallStock.Count > 0 && IsP1OneStepFromGoal() && !CanP2WinThisTurn())
        {
            List<Move> wallMoves = allMoves.FindAll(m => m.isWall);
            wallMoves = wallMoves.FindAll(IsHelpfulWallForPanic);
            if (wallMoves.Count > 0) allMoves = wallMoves;
        }

        int currentCalc = 0;

        foreach (Move move in allMoves)
        {
            ApplyMoveLogic(move, true);
            GameObject p1Tile = board.GetTileUnder(p1Data.playerObj);
            GameObject p2Tile = board.GetTileUnder(p2Data.playerObj);

            if (p1Tile != null && p2Tile != null)
            {
                float score;
                if (move.isWall)
                {
                    var graphAfterWall = board.CloneGraph(board.adjacencyList);
                    GameObject p1TileAfter = board.GetTileUnder(p1Data.playerObj);
                    GameObject p2TileAfter = board.GetTileUnder(p2Data.playerObj);
                    score = MonteCarloScoreForWall(graphAfterWall, p1TileAfter, p2TileAfter);
                }
                else
                {
                    var graphCopy = board.CloneGraph(board.adjacencyList);
                    score = Minimax(1, float.MinValue, float.MaxValue, false, p1Tile, p2Tile, graphCopy);
                }

                if (score > bestScore) { bestScore = score; bestMove = move; }
            }

            ApplyMoveLogic(move, false);

            currentCalc++;
            if (currentCalc >= 10) { currentCalc = 0; yield return null; }
        }

        if (bestMove != null) RealizeMove(bestMove);
        else gm.SwitchTurn();
    }

    float Minimax(int depth, float alpha, float beta, bool isMaximizing, GameObject p1Tile, GameObject p2Tile, Dictionary<GameObject, List<GameObject>> graph)
    {
        if (depth == 0) return EvaluateBoard(p1Tile, p2Tile, graph);

        if (isMaximizing)
        {
            float maxEval = float.MinValue;
            foreach (Move m in GetSmartMovesSimulated(p2Tile, graph))
            {
                var newGraph = board.CloneGraph(graph);
                GameObject newP2Tile = p2Tile;
                ApplyMoveToGraph(m, newGraph);
                if (!m.isWall) newP2Tile = m.targetTile;

                float eval = Minimax(depth - 1, alpha, beta, false, p1Tile, newP2Tile, newGraph);
                maxEval = Mathf.Max(maxEval, eval);
                alpha = Mathf.Max(alpha, eval);
                if (beta <= alpha) break;
            }
            return maxEval;
        }
        else
        {
            float minEval = float.MaxValue;
            foreach (Move m in GetSmartMovesSimulated(p1Tile, graph))
            {
                var newGraph = board.CloneGraph(graph);
                GameObject newP1Tile = p1Tile;
                ApplyMoveToGraph(m, newGraph);
                if (!m.isWall) newP1Tile = m.targetTile;

                float eval = Minimax(depth - 1, alpha, beta, true, newP1Tile, p2Tile, newGraph);
                minEval = Mathf.Min(minEval, eval);
                beta = Mathf.Min(beta, eval);
                if (beta <= alpha) break;
            }
            return minEval;
        }
    }

    float MonteCarloScoreForWall(Dictionary<GameObject, List<GameObject>> graph, GameObject p1TileStart, GameObject p2TileStart)
    {
        if (p1TileStart == null || p2TileStart == null) return float.MinValue;
        float total = 0f;

        for (int i = 0; i < mcRollouts; i++)
        {
            var g = board.CloneGraph(graph);
            GameObject p1 = p1TileStart, p2 = p2TileStart;
            bool p1Turn = true;

            for (int ply = 0; ply < mcPlies; ply++)
            {
                if (p1Turn) { p1 = ChooseMonteCarloPawnMove(p1, p1Data.goals, g); if (p1Data.goals.Contains(p1)) break; }
                else { p2 = ChooseMonteCarloPawnMove(p2, p2Data.goals, g); if (p2Data.goals.Contains(p2)) break; }
                p1Turn = !p1Turn;
            }
            total += EvaluateBoard(p1, p2, g);
        }
        return total / mcRollouts;
    }

    GameObject ChooseMonteCarloPawnMove(GameObject from, List<GameObject> goals, Dictionary<GameObject, List<GameObject>> graph)
    {
        if (from == null || !graph.ContainsKey(from)) return from;
        var neighbors = graph[from];
        if (neighbors == null || neighbors.Count == 0) return from;

        int bestDist = int.MaxValue;
        List<GameObject> best = new List<GameObject>();

        foreach (var n in neighbors)
        {
            int d = board.GetShortestPathLengthSim(n, goals, graph);
            if (d == -1) continue;

            if (d < bestDist) { bestDist = d; best.Clear(); best.Add(n); }
            else if (d == bestDist) best.Add(n);
        }

        if (best.Count == 0) return neighbors[Random.Range(0, neighbors.Count)];
        return best[Random.Range(0, best.Count)];
    }

    private float EvaluateBoard(GameObject p1Tile, GameObject p2Tile, Dictionary<GameObject, List<GameObject>> graph)
    {
        int p1Path = board.GetShortestPathLengthSim(p1Tile, p1Data.goals, graph);
        int p2Path = board.GetShortestPathLengthSim(p2Tile, p2Data.goals, graph);
        if (p1Path == -1) return 1000f;
        if (p2Path == -1) return -1000f;
        return (p1Path * 2f) - p2Path;
    }

    private List<Move> GetSmartMovesSimulated(GameObject tile, Dictionary<GameObject, List<GameObject>> graph)
    {
        List<Move> moves = new List<Move>();
        if (!graph.ContainsKey(tile)) return moves;
        foreach (var n in graph[tile]) moves.Add(new Move { isWall = false, targetTile = n, position = n.transform.position });
        return moves;
    }

    private void ApplyMoveLogic(Move m, bool apply)
    {
        if (m.isWall)
        {
            if (apply)
            {
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
            else
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
        else
        {
            if (apply)
            {
                m.previousPosition = p2Data.playerObj.transform.position;
                p2Data.playerObj.transform.position = m.targetTile.transform.position + Vector3.up * gm.tileHeightOffset;
            }
            else p2Data.playerObj.transform.position = m.previousPosition;
            Physics.SyncTransforms();
        }
    }

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

    private List<Move> GetSmartMoves()
    {
        List<Move> smartMoves = new List<Move>();
        GameObject currentTile = board.GetTileUnder(p2Data.playerObj);

        if (currentTile != null && board.adjacencyList.ContainsKey(currentTile))
        {
            foreach (GameObject neighbor in board.adjacencyList[currentTile])
            {
                if (board.IsTileOccupied(neighbor.transform.position, p2Data.playerObj))
                {
                    GameObject jumpTile = board.GetJumpTileInGraph(currentTile, neighbor);
                    if (jumpTile != null) smartMoves.Add(new Move { isWall = false, position = jumpTile.transform.position, targetTile = jumpTile });
                }
                else smartMoves.Add(new Move { isWall = false, position = neighbor.transform.position, targetTile = neighbor });
            }
        }

        if (p2Data.wallStock.Count > 0)
        {
            foreach (GameObject anchor in board.allAnchors)
            {
                if (anchor == null || !anchor.CompareTag("WallAnchor")) continue;
                if (Vector3.Distance(anchor.transform.position, p1Data.playerObj.transform.position) < 2.5f)
                {
                    CheckAndAddWall(smartMoves, anchor, true);
                    CheckAndAddWall(smartMoves, anchor, false);
                }
            }
        }
        return smartMoves;
    }

    private void CheckAndAddWall(List<Move> list, GameObject anchor, bool vertical)
    {
        var graphCopy = board.CloneGraph(board.adjacencyList);
        if (CanPlaceWallSim(anchor, vertical, graphCopy))
            list.Add(new Move { isWall = true, position = anchor.transform.position, vertical = vertical });
    }

    bool CanPlaceWallSim(GameObject anchor, bool vertical, Dictionary<GameObject, List<GameObject>> graph)
    {
        if (!board.PassesPhysicalWallChecks(anchor, vertical)) return false;
        var blockedEdges = board.GetEdgesBlockedByWall(anchor.transform.position, vertical);
        if (blockedEdges.Count == 0) return true;

        foreach (var edge in blockedEdges)
        {
            if (graph.ContainsKey(edge[0])) graph[edge[0]].Remove(edge[1]);
            if (graph.ContainsKey(edge[1])) graph[edge[1]].Remove(edge[0]);
        }
        return board.HasPathSim(board.GetTileUnder(p1Data.playerObj), p1Data.goals, graph) &&
               board.HasPathSim(board.GetTileUnder(p2Data.playerObj), p2Data.goals, graph);
    }

    private void RealizeMove(Move m)
    {
        if (m.isWall)
        {
            GameObject wallFromStock = (p2Data.wallStock.Count > 0) ? p2Data.wallStock[0] : null;
            if (wallFromStock == null) return;

            GameObject anchor = null;
            foreach (GameObject a in board.allAnchors)
            {
                if (Vector3.Distance(a.transform.position, m.position) < 0.1f) { anchor = a; break; }
            }

            if (anchor != null)
            {
                anchor.transform.localEulerAngles = m.vertical ? new Vector3(90, 90, 0) : new Vector3(90, 0, 0);
                gm.ConfirmWallPlacement(anchor, m.vertical, wallFromStock);
            }
        }
        else gm.MovePiece(p2Data.playerObj, m.position);

        gm.inputController.ResetAllStates();
    }

    bool CanP2WinThisTurn()
    {
        GameObject p2Tile = board.GetTileUnder(p2Data.playerObj);
        if (p2Tile == null) return false;
        if (p2Data.goals.Contains(p2Tile)) return true;
        if (!board.adjacencyList.ContainsKey(p2Tile)) return false;
        foreach (var n in board.adjacencyList[p2Tile]) if (p2Data.goals.Contains(n)) return true;
        return false;
    }

    bool IsP1OneStepFromGoal()
    {
        GameObject p1Tile = board.GetTileUnder(p1Data.playerObj);
        if (p1Tile == null) return false;
        return board.GetShortestPathLengthSim(p1Tile, p1Data.goals, board.adjacencyList) == 1;
    }

    bool IsHelpfulWallForPanic(Move wallMove)
    {
        GameObject p1Tile = board.GetTileUnder(p1Data.playerObj);
        GameObject p2Tile = board.GetTileUnder(p2Data.playerObj);
        if (p1Tile == null || p2Tile == null) return false;

        int beforeP1 = board.GetShortestPathLengthSim(p1Tile, p1Data.goals, board.adjacencyList);
        int beforeP2 = board.GetShortestPathLengthSim(p2Tile, p2Data.goals, board.adjacencyList);
        if (beforeP1 == -1 || beforeP2 == -1) return false;

        ApplyMoveLogic(wallMove, true);
        int afterP1 = board.GetShortestPathLengthSim(p1Tile, p1Data.goals, board.adjacencyList);
        int afterP2 = board.GetShortestPathLengthSim(p2Tile, p2Data.goals, board.adjacencyList);
        ApplyMoveLogic(wallMove, false);

        if (afterP1 == -1 || afterP2 == -1) return false;
        if (afterP1 <= beforeP1) return false;
        if (afterP2 > beforeP2 + 1) return false;

        return true;
    }
}
