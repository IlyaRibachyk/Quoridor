using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public GameObject playerObj;
    public List<GameObject> wallStock;
    public List<GameObject> goals = new List<GameObject>();
    public List<Transform> cameraPositions;

    // ОЦЕЙ РЯДОК БУВ ПРОПУЩЕНИЙ:
    public IPlayerStrategy strategy;
}

public class Move
{
    public bool isWall;
    public Vector3 position;
    public bool vertical;
    public GameObject targetTile;
    public Vector3 previousPosition;
    public List<GameObject[]> removedEdges = new List<GameObject[]>();
}

public interface IPlayerStrategy
{
    void ExecuteTurn();
}
