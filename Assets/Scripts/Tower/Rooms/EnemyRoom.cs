using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRoom : BaseRoom
{
    [Header("Enemy Settings")]
    [SerializeField] private int _totalEnemies = 20;
    [SerializeField] private float _waveInterval = 5f;
    [SerializeField] private GameObject[] _enemyPrefabs;

    [Header("Pickup Settings")]
    [SerializeField] private Transform[] _pickupSpawnPoints;
    [SerializeField] private GameObject _healthPickupPrefab;
    [SerializeField] private GameObject _ammoPickupPrefab;
    [SerializeField, Range(0f, 1f)] private float _healthPickupChance = 0.5f;
    [SerializeField] private float _pickupRespawnDelay = 10f;

    private List<NetworkObject> _spawnedEnemies = new();
    private DifficultyManager _difficulty;
    private Coroutine _waveCoroutine;
    private int _enemiesSpawned;
    private FloorDoor _floorDoor;
    private HashSet<int> _occupiedPickupPoints = new();

    public override void InitializeRoom()
    {
        base.InitializeRoom();
        _difficulty = FindObjectOfType<DifficultyManager>();
        _floorDoor = FindObjectOfType<FloorDoor>();

        if (!base.IsServerInitialized) return;

        _enemiesSpawned = 0;
        _spawnedEnemies.Clear();

        SpawnPickups();
        _waveCoroutine = StartCoroutine(WaveSpawner());
    }

    private IEnumerator WaveSpawner()
    {
        while (_enemiesSpawned < _totalEnemies)
        {
            SpawnOneEnemy();
            _enemiesSpawned++;
            yield return new WaitForSeconds(_waveInterval);
        }
    }

    private void SpawnOneEnemy()
    {
        if (_enemyPrefabs.Length == 0) return;

        GameObject prefab = _enemyPrefabs[Random.Range(0, _enemyPrefabs.Length)];
        Transform[] spawns = GetEnemySpawnPoints();
        Vector3 pos = spawns.Length > 0
            ? spawns[Random.Range(0, spawns.Length)].position
            : transform.position + Random.insideUnitSphere * 3f;

        GameObject enemy = Instantiate(prefab, pos, Quaternion.identity);
        NetworkObject nob = enemy.GetComponent<NetworkObject>();
        if (nob != null)
        {
            base.Spawn(nob);
            _spawnedEnemies.Add(nob);
            ApplyDifficulty(enemy);
        }
    }

    private void ApplyDifficulty(GameObject enemy)
    {
        if (_difficulty == null) return;

        var enemyComponent = enemy.GetComponent<EnemyBase>();
        if (enemyComponent != null)
        {
            enemyComponent.SetDifficulty(
                _difficulty.GetEnemyHP(),
                _difficulty.GetEnemyDamage(),
                _difficulty.GetSpeedMultiplier());
        }
    }

    private void SpawnPickups()
    {
        if (_pickupSpawnPoints == null || _pickupSpawnPoints.Length == 0)
        {
            Debug.LogWarning("[EnemyRoom] No pickup spawn points!");
            return;
        }

        for (int i = 0; i < _pickupSpawnPoints.Length; i++)
        {
            if (_pickupSpawnPoints[i] == null) continue;
            SpawnSinglePickup(i);
        }
    }

    private void SpawnSinglePickup(int index)
    {
        if (index < 0 || index >= _pickupSpawnPoints.Length) return;
        if (_pickupSpawnPoints[index] == null) return;
        if (_occupiedPickupPoints.Contains(index)) return;

        GameObject prefab = Random.value < _healthPickupChance
            ? _healthPickupPrefab
            : _ammoPickupPrefab;

        if (prefab == null) return;

        _occupiedPickupPoints.Add(index);
        Vector3 position = _pickupSpawnPoints[index].position;

        GameObject pickup = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject nob = pickup.GetComponent<NetworkObject>();
        if (nob != null)
        {
            RoomPickup rp = pickup.GetComponent<RoomPickup>();
            if (rp != null)
                rp.Init(index, OnPickupCollected);

            base.Spawn(nob);
            pickup.transform.SetParent(transform);
        }
        else
        {
            _occupiedPickupPoints.Remove(index);
            Destroy(pickup);
        }
    }

    private void OnPickupCollected(int index)
    {
        _occupiedPickupPoints.Remove(index);
        StartCoroutine(RespawnPickupAfterDelay(index));
    }

    private IEnumerator RespawnPickupAfterDelay(int index)
    {
        yield return new WaitForSeconds(_pickupRespawnDelay);
        SpawnSinglePickup(index);
    }

    private Transform[] GetEnemySpawnPoints()
    {
        var list = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).CompareTag("EnemySpawn"))
                list.Add(transform.GetChild(i));
        }
        return list.ToArray();
    }

    public override void CleanupRoom()
    {
        if (_waveCoroutine != null)
        {
            StopCoroutine(_waveCoroutine);
            _waveCoroutine = null;
        }

        foreach (var nob in _spawnedEnemies)
        {
            if (nob != null && nob.IsSpawned)
                base.Despawn(nob);
        }
        _spawnedEnemies.Clear();
        _occupiedPickupPoints.Clear();
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;
        if (IsCompleted) return;

        _spawnedEnemies.RemoveAll(e => e == null || !e.IsSpawned);

        if (_enemiesSpawned >= _totalEnemies && _spawnedEnemies.Count == 0)
        {
            if (_waveCoroutine != null)
            {
                StopCoroutine(_waveCoroutine);
                _waveCoroutine = null;
            }

            CompleteRoom();

            if (_floorDoor != null)
                _floorDoor.OpenDoor();
        }
    }
}
