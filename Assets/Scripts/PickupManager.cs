using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;

public class PickupManager : NetworkBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private GameObject _healthPickupPrefab;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private float _respawnDelay = 10f;

    public override void OnStartNetwork()
    {
        // Только сервер спавнит аптечки
        if (!base.IsServerInitialized)
        {
            enabled = false;
            return;
        }

        SpawnAllPickups();
    }

    private void SpawnAllPickups()
    {
        foreach (Transform spawnPoint in _spawnPoints)
        {
            if (spawnPoint != null)
            {
                SpawnPickup(spawnPoint.position);
            }
        }
    }

    public void OnPickedUp(Vector3 position)
    {
        if (!base.IsServerInitialized) return;
        StartCoroutine(RespawnAfterDelay(position));
    }

    private IEnumerator RespawnAfterDelay(Vector3 position)
    {
        yield return new WaitForSeconds(_respawnDelay);
        SpawnPickup(position);
    }

    private void SpawnPickup(Vector3 position)
    {
        if (_healthPickupPrefab == null)
        {
            return;
        }

        GameObject pickup = Instantiate(_healthPickupPrefab, position, Quaternion.identity);

        HealthPickup healthPickup = pickup.GetComponent<HealthPickup>();
        if (healthPickup != null)
        {
            healthPickup.Init(this, position);
        }

        NetworkObject networkObject = pickup.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            base.Spawn(networkObject);
        }
    }
}