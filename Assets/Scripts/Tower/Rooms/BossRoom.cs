using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossRoom : BaseRoom
{
    [Header("Boss Settings")]
    [SerializeField] private GameObject _bossPrefab;
    [SerializeField] private Transform _bossSpawnPoint;

    [Header("Pickup Settings")]
    [SerializeField] private Transform[] _pickupSpawnPoints;
    [SerializeField] private GameObject _healthPickupPrefab;
    [SerializeField] private GameObject _ammoPickupPrefab;
    [SerializeField, Range(0f, 1f)] private float _healthPickupChance = 0.5f;
    [SerializeField] private float _pickupRespawnDelay = 10f;

    private NetworkObject _spawnedBoss;
    private DifficultyManager _difficulty;
    private FloorDoor _floorDoor;
    private HashSet<int> _occupiedPickupPoints = new();

    public override void InitializeRoom()
    {
        base.InitializeRoom();
        _difficulty = FindObjectOfType<DifficultyManager>();
        _floorDoor = FindObjectOfType<FloorDoor>();

        if (!base.IsServerInitialized) return;

        SpawnPickups();
        SpawnBoss();
    }

    private void SpawnBoss()
    {
        if (_bossPrefab == null)
        {
            Debug.LogError("[BossRoom] Boss prefab not assigned!");
            return;
        }

        Vector3 spawnPos = _bossSpawnPoint != null
            ? _bossSpawnPoint.position
            : transform.position + Vector3.up;

        GameObject boss = Instantiate(_bossPrefab, spawnPos, Quaternion.identity);
        NetworkObject nob = boss.GetComponent<NetworkObject>();
        if (nob != null)
        {
            base.Spawn(nob);
            _spawnedBoss = nob;

            if (_difficulty != null)
            {
                var enemyComp = boss.GetComponent<EnemyBase>();
                if (enemyComp != null)
                {
                    enemyComp.SetDifficulty(
                        _difficulty.GetEnemyHP() * 5,
                        _difficulty.GetEnemyDamage() * 2,
                        _difficulty.GetSpeedMultiplier() * 0.7f
                    );
                }
            }
        }
    }

    private void SpawnPickups()
    {
        if (_pickupSpawnPoints == null || _pickupSpawnPoints.Length == 0) return;

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

    public override void CleanupRoom()
    {
        if (_spawnedBoss != null && _spawnedBoss.IsSpawned)
        {
            base.Despawn(_spawnedBoss);
        }
        _spawnedBoss = null;
        _occupiedPickupPoints.Clear();
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;
        if (IsCompleted) return;

        if (_spawnedBoss == null || !_spawnedBoss.IsSpawned)
        {
            if (_floorDoor != null)
                _floorDoor.OpenDoor();

            CompleteRoom();
        }
    }
}
