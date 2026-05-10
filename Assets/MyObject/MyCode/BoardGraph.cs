using System.Collections.Generic;
using UnityEngine;

public class BoardGraph : MonoBehaviour
{
    public float gridStep = 1.0f;
    public LayerMask playerLayer;

    public GameManager gm;

    public Dictionary<GameObject, List<GameObject>> adjacencyList = new Dictionary<GameObject, List<GameObject>>();
    [HideInInspector] public GameObject[] allAnchors;

    public void BuildPhysicalGraph(List<PlayerData> players)
    {
        adjacencyList.Clear();
        foreach (var p in players) p.goals.Clear();

        GameObject[] tiles = GameObject.FindGameObjectsWithTag("Tile");
        if (tiles.Length == 0) return;

        // 1. Знаходимо крайні межі ігрового поля
        float maxZ = -100f, minZ = 100f;
        float maxX = -100f, minX = 100f;

        foreach (var t in tiles)
        {
            Vector3 pos = t.transform.position;
            if (pos.z > maxZ) maxZ = pos.z;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.x < minX) minX = pos.x;

            adjacencyList[t] = new List<GameObject>();
        }

        // 2. Будуємо зв'язки та призначаємо цілі
        foreach (var t in tiles)
        {
            Vector3 posA = t.transform.position;

            // Призначаємо цілі залежно від індексу гравця у списку (0-3)
            // Гравці 1 та 2 (Північ/Південь)
            if (players.Count >= 1 && Mathf.Abs(posA.z - minZ) < 0.2f) players[0].goals.Add(t);
            if (players.Count >= 2 && Mathf.Abs(posA.z - maxZ) < 0.2f) players[1].goals.Add(t);

            // Гравці 3 та 4 (Захід/Схід) - додаються, якщо у списку 4 гравці
            if (players.Count >= 3 && Mathf.Abs(posA.x - maxX) < 0.2f) players[2].goals.Add(t);

            // Якщо Гравець 4 стартує справа (maxX), його ціль — лівий край (minX)
            if (players.Count >= 4 && Mathf.Abs(posA.x - minX) < 0.2f) players[3].goals.Add(t);

            // Стандартна логіка побудови ребер графа
            foreach (var other in tiles)
            {
                if (t == other) continue;
                Vector3 posB = other.transform.position;
                float dist = Vector3.Distance(posA, posB);

                bool isStraightLine = Mathf.Abs(posA.x - posB.x) < 0.1f || Mathf.Abs(posA.z - posB.z) < 0.1f;

                if (isStraightLine && dist < gridStep * 1.2f)
                {
                    if (!adjacencyList[t].Contains(other)) adjacencyList[t].Add(other);
                }
            }
        }
        allAnchors = GameObject.FindGameObjectsWithTag("WallAnchor");
    }

    public Dictionary<GameObject, List<GameObject>> CloneGraph(Dictionary<GameObject, List<GameObject>> original)
    {
        var clone = new Dictionary<GameObject, List<GameObject>>();
        foreach (var kvp in original) clone[kvp.Key] = new List<GameObject>(kvp.Value);
        return clone;
    }

    public GameObject GetTileUnder(GameObject obj)
    {
        if (obj == null) return null;
        RaycastHit[] hits = Physics.RaycastAll(obj.transform.position + Vector3.up * 0.5f, Vector3.down, 2.0f);
        foreach (var hit in hits) if (hit.collider.CompareTag("Tile")) return hit.collider.gameObject;

        Collider[] closeObjects = Physics.OverlapSphere(obj.transform.position, 0.5f);
        foreach (var c in closeObjects) if (c.CompareTag("Tile")) return c.gameObject;

        return null;
    }

    public List<GameObject[]> GetEdgesBlockedByWall(Vector3 pos, bool vertical)
    {
        List<GameObject[]> blocked = new List<GameObject[]>();
        Vector3 side = vertical ? Vector3.forward : Vector3.right;
        Vector3 dir = vertical ? Vector3.right : Vector3.forward;

        Vector3[] points = { pos - dir * (gridStep * 0.5f), pos + dir * (gridStep * 0.5f) };

        foreach (var p in points)
        {
            GameObject t1 = null, t2 = null;
            foreach (var c in Physics.OverlapSphere(p - side * 0.5f, 0.2f)) if (c.CompareTag("Tile")) t1 = c.gameObject;
            foreach (var c in Physics.OverlapSphere(p + side * 0.5f, 0.2f)) if (c.CompareTag("Tile")) t2 = c.gameObject;
            if (t1 != null && t2 != null) blocked.Add(new GameObject[] { t1, t2 });
        }
        return blocked;
    }

    public bool HasPath(GameObject start, List<GameObject> goals)
    {
        if (start == null || goals == null || goals.Count == 0) return false;
        Queue<GameObject> q = new Queue<GameObject>();
        HashSet<GameObject> visited = new HashSet<GameObject>();
        q.Enqueue(start); visited.Add(start);

        while (q.Count > 0)
        {
            GameObject curr = q.Dequeue();
            if (goals.Contains(curr)) return true;
            if (!adjacencyList.ContainsKey(curr)) continue;

            foreach (GameObject n in adjacencyList[curr])
            {
                if (!visited.Contains(n)) { visited.Add(n); q.Enqueue(n); }
            }
        }
        return false;
    }

    public bool HasPathSim(GameObject start, List<GameObject> goals, Dictionary<GameObject, List<GameObject>> graph)
    {
        if (start == null || goals == null || goals.Count == 0) return false;
        Queue<GameObject> q = new Queue<GameObject>();
        HashSet<GameObject> visited = new HashSet<GameObject>();
        q.Enqueue(start); visited.Add(start);

        while (q.Count > 0)
        {
            var curr = q.Dequeue();
            if (goals.Contains(curr)) return true;
            if (!graph.ContainsKey(curr)) continue;

            foreach (var n in graph[curr]) if (visited.Add(n)) q.Enqueue(n);
        }
        return false;
    }

    public int GetShortestPathLengthSim(GameObject startTile, List<GameObject> goals, Dictionary<GameObject, List<GameObject>> graph)
    {
        if (startTile == null) return -1;
        Queue<(GameObject tile, int dist)> q = new Queue<(GameObject, int)>();
        HashSet<GameObject> visited = new HashSet<GameObject>();

        q.Enqueue((startTile, 0));
        visited.Add(startTile);

        while (q.Count > 0)
        {
            var (curr, dist) = q.Dequeue();
            if (goals.Contains(curr)) return dist;
            if (!graph.ContainsKey(curr)) continue;

            foreach (var n in graph[curr])
            {
                if (!visited.Contains(n)) { visited.Add(n); q.Enqueue((n, dist + 1)); }
            }
        }
        return -1;
    }

    public bool IsTileOccupied(Vector3 pos, GameObject exceptionPiece)
    {
        foreach (var col in Physics.OverlapSphere(pos, 0.3f, playerLayer)) if (col.gameObject != exceptionPiece) return true;
        return false;
    }

    public GameObject GetJumpTileInGraph(GameObject from, GameObject opponentTile)
    {
        Vector3 directionToOpponent = (opponentTile.transform.position - from.transform.position).normalized;
        if (adjacencyList.ContainsKey(opponentTile))
        {
            foreach (GameObject potentialJumpTile in adjacencyList[opponentTile])
            {
                Vector3 directionToNext = (potentialJumpTile.transform.position - opponentTile.transform.position).normalized;
                if (Vector3.Dot(directionToOpponent, directionToNext) > 0.9f)
                {
                    if (!IsTileOccupied(potentialJumpTile.transform.position, null)) return potentialJumpTile;
                }
            }
        }
        return null;
    }

    public bool GetWallIsVerticalFromRotation(Transform t)
    {
        float y = Mathf.Repeat(t.localEulerAngles.y, 180f);
        return Mathf.Abs(y - 90f) < 10f;
    }

    public bool PassesPhysicalWallChecks(GameObject anchor, bool vertical)
    {
        Vector3 pos = anchor.transform.position;
        Collider[] centerCheck = Physics.OverlapBox(pos, new Vector3(0.1f, 0.4f, 0.1f));
        foreach (var hit in centerCheck) if (hit.gameObject != anchor && hit.CompareTag("Untagged")) return false;

        Vector3 dir = vertical ? Vector3.right : Vector3.forward;
        Vector3 endA = pos - dir * (gridStep * 0.5f);
        Vector3 endB = pos + dir * (gridStep * 0.5f);

        if (HasParallelWallNearPoint(endA, vertical, anchor)) return false;
        if (HasParallelWallNearPoint(endB, vertical, anchor)) return false;

        return true;
    }

    private bool HasParallelWallNearPoint(Vector3 p, bool vertical, GameObject selfAnchor)
    {
        Collider[] cols = Physics.OverlapSphere(p, 0.15f);
        foreach (var c in cols)
        {
            if (c.gameObject == selfAnchor || !c.CompareTag("Untagged")) continue;
            bool hitVertical = GetWallIsVerticalFromRotation(c.transform);
            if (hitVertical == vertical) return true;
        }
        return false;
    }

    void OnDrawGizmos()
    {
        if (adjacencyList == null) return;
        foreach (var node in adjacencyList)
        {
            if (node.Key == null) continue;
            foreach (var neighbor in node.Value)
            {
                if (neighbor == null) continue;
                Gizmos.color = Color.white;
                Gizmos.DrawLine(node.Key.transform.position, neighbor.transform.position);
            }
        }

        if (Application.isPlaying && gm != null && gm.allPlayers != null)
        {
            for (int i = 0; i < gm.allPlayers.Count; i++)
            {
                // Призначаємо колір залежно від індексу гравця (0-3)
                switch (i)
                {
                    case 0: Gizmos.color = Color.green; break;  // 1-й гравець
                    case 1: Gizmos.color = Color.red; break;    // 2-й гравець
                    case 2: Gizmos.color = Color.blue; break;   // 3-й гравець
                    case 3: Gizmos.color = Color.yellow; break; // 4-й гравець
                    default: Gizmos.color = Color.white; break;
                }

                foreach (GameObject goalTile in gm.allPlayers[i].goals)
                {
                    if (goalTile != null)
                    {
                        Gizmos.DrawSphere(goalTile.transform.position + Vector3.up * 0.2f, 0.2f);
                    }
                }
            }
        }
    }
}
