using System.Collections.Generic;
using UnityEngine;

// Клас, що відповідає за логічне представлення ігрового поля у вигляді графа.
// Дозволяє алгоритмам шукати шляхи, розраховувати стрибки та перевіряти перекриття стінами.
public class BoardGraph : MonoBehaviour
{
    // Фізичний розмір однієї клітинки (крок сітки)
    public float gridStep = 1.0f;
    // Шар (Layer), на якому знаходяться фішки гравців (для фізичних перевірок)
    public LayerMask playerLayer;

    // Посилання на головний менеджер гри
    public GameManager gm;

    // Головна структура даних: Словник суміжності (Граф). 
    // Зберігає інформацію: "Для цієї клітинки (Key) ось список її доступних сусідів (Value)"
    public Dictionary<GameObject, List<GameObject>> adjacencyList = new Dictionary<GameObject, List<GameObject>>();

    // Масив усіх можливих точок (якорів), куди можна поставити стіну
    [HideInInspector] public GameObject[] allAnchors;

    // Метод генерує граф ігрового поля на старті гри та визначає цілі для кожного гравця
    public void BuildPhysicalGraph(List<PlayerData> players)
    {
        adjacencyList.Clear();
        foreach (var p in players) p.goals.Clear();

        // Знаходимо всі об'єкти з тегом "Tile" (клітинки поля)
        GameObject[] tiles = GameObject.FindGameObjectsWithTag("Tile");
        if (tiles.Length == 0) return;

        // 1. Знаходимо крайні межі ігрового поля (мінімальні та максимальні координати)
        float maxZ = -100f, minZ = 100f;
        float maxX = -100f, minX = 100f;

        foreach (var t in tiles)
        {
            Vector3 pos = t.transform.position;
            // Оновлюємо межі
            if (pos.z > maxZ) maxZ = pos.z;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.x < minX) minX = pos.x;

            // Ініціалізуємо порожній список сусідів для кожної клітинки
            adjacencyList[t] = new List<GameObject>();
        }

        // 2. Будуємо зв'язки (ребра графа) та призначаємо цільові лінії для гравців
        foreach (var t in tiles)
        {
            Vector3 posA = t.transform.position;

            // Призначаємо цілі залежно від індексу гравця у списку (0-3)
            // Гравець 0 стартує знизу, його ціль - верхній край (maxZ) -> В коді умова < 0.2f від minZ означає, що це стартова лінія іншого гравця, яка і є ціллю.
            // Гравці 1 та 2 (Північ/Південь)
            if (players.Count >= 1 && Mathf.Abs(posA.z - minZ) < 0.2f) players[0].goals.Add(t);
            if (players.Count >= 2 && Mathf.Abs(posA.z - maxZ) < 0.2f) players[1].goals.Add(t);

            // Гравці 3 та 4 (Захід/Схід) - додаються, якщо у списку 3 або 4 гравці
            if (players.Count >= 3 && Mathf.Abs(posA.x - maxX) < 0.2f) players[2].goals.Add(t);

            // Якщо Гравець 4 стартує справа (maxX), його ціль — лівий край (minX)
            if (players.Count >= 4 && Mathf.Abs(posA.x - minX) < 0.2f) players[3].goals.Add(t);

            // Стандартна логіка побудови ребер графа (хто з ким сусідить)
            foreach (var other in tiles)
            {
                if (t == other) continue; // Саму з собою не з'єднуємо
                Vector3 posB = other.transform.position;
                float dist = Vector3.Distance(posA, posB);

                // Перевіряємо, чи знаходяться клітинки на одній прямій (по осі X або Z)
                bool isStraightLine = Mathf.Abs(posA.x - posB.x) < 0.1f || Mathf.Abs(posA.z - posB.z) < 0.1f;

                // Якщо вони на одній прямій і відстань між ними дорівнює одному кроку - вони сусіди
                if (isStraightLine && dist < gridStep * 1.2f)
                {
                    if (!adjacencyList[t].Contains(other)) adjacencyList[t].Add(other);
                }
            }
        }
        // Знаходимо всі точки для стін на сцені
        allAnchors = GameObject.FindGameObjectsWithTag("WallAnchor");
    }

    // Створює глибоку копію графа. Використовується ШІ, щоб "уявно" ламати стіни і перевіряти шляхи без впливу на реальну гру.
    public Dictionary<GameObject, List<GameObject>> CloneGraph(Dictionary<GameObject, List<GameObject>> original)
    {
        var clone = new Dictionary<GameObject, List<GameObject>>();
        foreach (var kvp in original) clone[kvp.Key] = new List<GameObject>(kvp.Value);
        return clone;
    }

    // Повертає об'єкт клітинки, на якій зараз фізично стоїть переданий об'єкт (наприклад, фішка гравця)
    public GameObject GetTileUnder(GameObject obj)
    {
        if (obj == null) return null;
        // Пускаємо промінь вниз, щоб знайти клітинку
        RaycastHit[] hits = Physics.RaycastAll(obj.transform.position + Vector3.up * 0.5f, Vector3.down, 2.0f);
        foreach (var hit in hits) if (hit.collider.CompareTag("Tile")) return hit.collider.gameObject;

        // Запасний варіант: шукаємо клітинку в радіусі
        Collider[] closeObjects = Physics.OverlapSphere(obj.transform.position, 0.5f);
        foreach (var c in closeObjects) if (c.CompareTag("Tile")) return c.gameObject;

        return null;
    }

    // Повертає список пар клітинок (ребер графа), які будуть перекриті, якщо поставити стіну в певній позиції
    public List<GameObject[]> GetEdgesBlockedByWall(Vector3 pos, bool vertical)
    {
        List<GameObject[]> blocked = new List<GameObject[]>();
        Vector3 side = vertical ? Vector3.forward : Vector3.right;
        Vector3 dir = vertical ? Vector3.right : Vector3.forward;

        // Стіна займає дві секції. Визначаємо центри цих секцій.
        Vector3[] points = { pos - dir * (gridStep * 0.5f), pos + dir * (gridStep * 0.5f) };

        // Для кожної половинки стіни знаходимо клітинки по обидва боки від неї
        foreach (var p in points)
        {
            GameObject t1 = null, t2 = null;
            foreach (var c in Physics.OverlapSphere(p - side * 0.5f, 0.2f)) if (c.CompareTag("Tile")) t1 = c.gameObject;
            foreach (var c in Physics.OverlapSphere(p + side * 0.5f, 0.2f)) if (c.CompareTag("Tile")) t2 = c.gameObject;
            // Якщо по обидва боки є клітинки, значить стіна розриває зв'язок між ними
            if (t1 != null && t2 != null) blocked.Add(new GameObject[] { t1, t2 });
        }
        return blocked; // Повертає список заблокованих зв'язків (ребер)
    }

    // Перевіряє, чи існує шлях від старту до хоча б однієї з цілей на РЕАЛЬНІЙ дошці (алгоритм BFS)
    public bool HasPath(GameObject start, List<GameObject> goals)
    {
        if (start == null || goals == null || goals.Count == 0) return false;
        Queue<GameObject> q = new Queue<GameObject>();
        HashSet<GameObject> visited = new HashSet<GameObject>();
        q.Enqueue(start); visited.Add(start);

        while (q.Count > 0)
        {
            GameObject curr = q.Dequeue();
            if (goals.Contains(curr)) return true; // Шлях знайдено
            if (!adjacencyList.ContainsKey(curr)) continue;

            foreach (GameObject n in adjacencyList[curr])
            {
                if (!visited.Contains(n)) { visited.Add(n); q.Enqueue(n); }
            }
        }
        return false; // Шляху немає (наприклад, гравець повністю заблокований)
    }

    // Те саме, що й HasPath, але шукає шлях на ВІРТУАЛЬНОМУ (симульованому) графі. Використовується ШІ.
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

    // Знаходить не просто чи є шлях, а його найкоротшу ДОВЖИНУ (у кроках) на симульованому графі
    public int GetShortestPathLengthSim(GameObject startTile, List<GameObject> goals, Dictionary<GameObject, List<GameObject>> graph)
    {
        if (startTile == null) return -1;
        // Черга зберігає клітинку та відстань до неї від старту
        Queue<(GameObject tile, int dist)> q = new Queue<(GameObject, int)>();
        HashSet<GameObject> visited = new HashSet<GameObject>();

        q.Enqueue((startTile, 0));
        visited.Add(startTile);

        while (q.Count > 0)
        {
            var (curr, dist) = q.Dequeue();
            if (goals.Contains(curr)) return dist; // Повертаємо кількість кроків
            if (!graph.ContainsKey(curr)) continue;

            foreach (var n in graph[curr])
            {
                if (!visited.Contains(n)) { visited.Add(n); q.Enqueue((n, dist + 1)); }
            }
        }
        return -1; // Шляху не існує
    }

    // Перевіряє, чи стоїть на вказаній позиції якась інша фішка гравця (крім exceptionPiece)
    public bool IsTileOccupied(Vector3 pos, GameObject exceptionPiece)
    {
        foreach (var col in Physics.OverlapSphere(pos, 0.3f, playerLayer))
            if (col.gameObject != exceptionPiece) return true;
        return false;
    }

    // Логіка Кворидору: якщо перед тобою стоїть опонент, ти можеш перестрибнути його.
    // Метод шукає клітинку відразу ЗА опонентом.
    public GameObject GetJumpTileInGraph(GameObject from, GameObject opponentTile)
    {
        // Напрямок стрибка
        Vector3 directionToOpponent = (opponentTile.transform.position - from.transform.position).normalized;
        if (adjacencyList.ContainsKey(opponentTile))
        {
            // Шукаємо серед сусідів опонента клітинку, яка лежить на тій самій прямій
            foreach (GameObject potentialJumpTile in adjacencyList[opponentTile])
            {
                Vector3 directionToNext = (potentialJumpTile.transform.position - opponentTile.transform.position).normalized;
                // Dot product > 0.9 означає, що вектори дивляться практично в одному напрямку
                if (Vector3.Dot(directionToOpponent, directionToNext) > 0.9f)
                {
                    // Якщо клітинка для приземлення вільна - повертаємо її
                    if (!IsTileOccupied(potentialJumpTile.transform.position, null)) return potentialJumpTile;
                }
            }
        }
        return null;
    }

    // Допоміжний метод: визначає, чи є стіна вертикальною, зчитуючи її кут повороту (Rotation Y)
    public bool GetWallIsVerticalFromRotation(Transform t)
    {
        float y = Mathf.Repeat(t.localEulerAngles.y, 180f);
        return Mathf.Abs(y - 90f) < 10f; // Якщо кут ~90 або ~270 градусів
    }

    // Перевіряє фізичну можливість поставити стіну (чи не "встромляється" вона в іншу стіну)
    public bool PassesPhysicalWallChecks(GameObject anchor, bool vertical)
    {
        Vector3 pos = anchor.transform.position;
        // 1. Перевірка центру: чи не стоїть вже тут інша стіна (хрест-на-хрест)
        Collider[] centerCheck = Physics.OverlapBox(pos, new Vector3(0.1f, 0.4f, 0.1f));
        foreach (var hit in centerCheck) if (hit.gameObject != anchor && hit.CompareTag("Untagged")) return false;

        // 2. Перевірка кінців стіни (стіна займає дві довжини, перевіряємо, чи не накладається її половина на іншу стіну)
        Vector3 dir = vertical ? Vector3.right : Vector3.forward;
        Vector3 endA = pos - dir * (gridStep * 0.5f);
        Vector3 endB = pos + dir * (gridStep * 0.5f);

        if (HasParallelWallNearPoint(endA, vertical, anchor)) return false;
        if (HasParallelWallNearPoint(endB, vertical, anchor)) return false;

        return true;
    }

    // Перевіряє, чи є в заданій точці стіна такої самої орієнтації (щоб запобігти накладанню стін одна на одну)
    private bool HasParallelWallNearPoint(Vector3 p, bool vertical, GameObject selfAnchor)
    {
        Collider[] cols = Physics.OverlapSphere(p, 0.15f);
        foreach (var c in cols)
        {
            if (c.gameObject == selfAnchor || !c.CompareTag("Untagged")) continue;
            bool hitVertical = GetWallIsVerticalFromRotation(c.transform);
            if (hitVertical == vertical) return true; // Знайшли паралельну стіну в цій точці
        }
        return false;
    }

    // Вбудований метод Unity: малює допоміжну графіку (лінії графа та цілі) в редакторі (вікно Scene) для зручності розробника
    void OnDrawGizmos()
    {
        // Малюємо білі лінії зв'язків між клітинками (ребра графа)
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

        // Малюємо кольорові сфери над клітинками-цілями для кожного гравця
        if (Application.isPlaying && gm != null && gm.allPlayers != null)
        {
            for (int i = 0; i < gm.allPlayers.Count; i++)
            {
                // Призначаємо колір залежно від індексу гравця (0-3)
                switch (i)
                {
                    case 0: Gizmos.color = Color.green; break;  // 1-й гравець (Зелений)
                    case 1: Gizmos.color = Color.red; break;    // 2-й гравець (Червоний)
                    case 2: Gizmos.color = Color.blue; break;   // 3-й гравець (Синій)
                    case 3: Gizmos.color = Color.yellow; break; // 4-й гравець (Жовтий)
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
