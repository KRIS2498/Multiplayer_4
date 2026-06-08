using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRoom : BaseRoom
{
    [Header("Enemy Settings")]
    [SerializeField] private int _baseEnemyCount = 3;
    [SerializeField] private GameObject[] _enemyPrefabs;

    private List<NetworkObject> _spawnedEnemies = new();
    private DifficultyManager _difficulty;

    public override void InitializeRoom()
    {
        base.InitializeRoom();
        _difficulty = FindObjectOfType<DifficultyManager>();

        if (!base.IsServerInitialized) return;

        SpawnEnemies();
    }

    private void SpawnEnemies()
    {
        int count = _difficulty != null
            ? _difficulty.GetSpawnCount(_baseEnemyCount)
            : _baseEnemyCount;

        Transform[] spawns = GetEnemySpawnPoints();
        for (int i = 0; i < count; i++)
        {
            if (_enemyPrefabs.Length == 0) break;

            GameObject prefab = _enemyPrefabs[Random.Range(0, _enemyPrefabs.Length)];
            Vector3 pos = spawns.Length > 0
                ? spawns[Random.Range(0, spawns.Length)].position
                : transform.position + Random.insideUnitSphere * 3f;
            pos.y = 0;

            GameObject enemy = Instantiate(prefab, pos, Quaternion.identity);
            NetworkObject nob = enemy.GetComponent<NetworkObject>();
            if (nob != null)
            {
                base.Spawn(nob);
                _spawnedEnemies.Add(nob);

                ApplyDifficulty(enemy);
            }
        }
    }

    private void ApplyDifficulty(GameObject enemy)
    {
        if (_difficulty == null) return;

        var enemyComponent = enemy.GetComponent<EnemyBase>();
        if (enemyComponent != null)
        {
            enemyComponent.SetDifficulty(_difficulty.GetEnemyHP(), _difficulty.GetEnemyDamage(), _difficulty.GetSpeedMultiplier());
        }
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

    private void Update()
    {
        if (!base.IsServerInitialized) return;
        if (IsCompleted) return;

        _spawnedEnemies.RemoveAll(e => e == null || !e.IsSpawned);

        if (_spawnedEnemies.Count == 0)
        {
            CompleteRoom();
        }
    }
}
