using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tower/Room Database", fileName = "RoomDatabase")]
public class RoomDatabase : ScriptableObject
{
    [System.Serializable]
    public class RoomEntry
    {
        public RoomType Type;
        public GameObject Prefab;
        public int MinFloor = 1;
        public int MaxFloor = 999;
        public float Weight = 1f;
    }

    [SerializeField] private List<RoomEntry> _rooms = new();

    public GameObject GetRandomRoom(RoomType type, int floor)
    {
        var candidates = _rooms.FindAll(r =>
            r.Type == type &&
            r.Prefab != null &&
            floor >= r.MinFloor &&
            floor <= r.MaxFloor);

        if (candidates.Count == 0) return null;

        float totalWeight = 0f;
        foreach (var c in candidates)
            totalWeight += c.Weight;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var c in candidates)
        {
            cumulative += c.Weight;
            if (roll <= cumulative)
                return c.Prefab;
        }

        return candidates[^1].Prefab;
    }

    public GameObject GetRandomNonBossRoom(int floor)
    {
        var types = new[] { RoomType.Enemy, RoomType.Trap, RoomType.Puzzle, RoomType.Parkour };
        var type = types[Random.Range(0, types.Length)];
        return GetRandomRoom(type, floor);
    }

    public GameObject GetRandomBossRoom(int floor)
    {
        return GetRandomRoom(RoomType.Boss, floor);
    }
}
