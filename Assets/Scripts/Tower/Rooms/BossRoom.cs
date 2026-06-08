using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

public class BossRoom : BaseRoom
{
    [Header("Boss Settings")]
    [SerializeField] private GameObject _bossPrefab;
    [SerializeField] private Transform _bossSpawnPoint;

    private NetworkObject _spawnedBoss;
    private DifficultyManager _difficulty;

    public override void InitializeRoom()
    {
        base.InitializeRoom();
        _difficulty = FindObjectOfType<DifficultyManager>();

        if (!base.IsServerInitialized) return;

        if (_bossPrefab != null)
        {
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
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;
        if (IsCompleted) return;
        if (_spawnedBoss == null || !_spawnedBoss.IsSpawned)
        {
            CompleteRoom();
        }
    }
}
