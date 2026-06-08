using FishNet.Object;
using UnityEngine;

public class TrapRoom : BaseRoom
{
    [Header("Trap Settings")]
    [SerializeField] private GameObject[] _trapPrefabs;
    [SerializeField] private int _trapCount = 5;
    [SerializeField] private float _activationDelay = 1.5f;
    [SerializeField] private float _completionWaitTime = 3f;

    private float _enterTime;
    private bool _playersEntered;

    public override void OnPlayersEntered(System.Collections.Generic.List<PlayerNetwork> players)
    {
        base.OnPlayersEntered(players);
        _playersEntered = true;
        _enterTime = Time.time;

        if (base.IsServerInitialized)
        {
            ActivateTraps();
        }
    }

    private void ActivateTraps()
    {
        var trapPoints = GetTrapSpawnPoints();
        int count = Mathf.Min(_trapCount, trapPoints.Length);

        for (int i = 0; i < count; i++)
        {
            if (_trapPrefabs.Length == 0) break;
            GameObject prefab = _trapPrefabs[Random.Range(0, _trapPrefabs.Length)];
            Transform point = trapPoints[Random.Range(0, trapPoints.Length)];

            GameObject trap = Instantiate(prefab, point.position, point.rotation);
            NetworkObject nob = trap.GetComponent<NetworkObject>();
            if (nob != null)
                base.Spawn(nob);
        }
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;
        if (IsCompleted || !_playersEntered) return;

        if (Time.time >= _enterTime + _completionWaitTime)
        {
            bool bothAlive = true;
            foreach (var p in _playersInRoom)
            {
                if (p != null && !p.IsAlive.Value)
                {
                    bothAlive = false;
                    break;
                }
            }

            if (bothAlive)
                CompleteRoom();
        }
    }

    private Transform[] GetTrapSpawnPoints()
    {
        var list = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).CompareTag("TrapSpawn"))
                list.Add(transform.GetChild(i));
        }
        return list.ToArray();
    }
}
