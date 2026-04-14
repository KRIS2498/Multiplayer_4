using Unity.Netcode;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float _speed = 18f;
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _maxLifetime = 5f;
    [SerializeField] private LayerMask _hitLayers = -1;

    private float _spawnTime;
    private Vector3 _lastPosition;

    public override void OnNetworkSpawn()
    {
        _spawnTime = Time.time;
        _lastPosition = transform.position;

        // Автоматически уничтожить снаряд через время
        if (IsServer)
        {
            Invoke(nameof(DestroyProjectile), _maxLifetime);
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        Vector3 newPosition = transform.position + transform.forward * _speed * Time.deltaTime;

        // Raycast для обнаружения попаданий
        Vector3 direction = newPosition - _lastPosition;
        float distance = direction.magnitude;

        if (distance > 0.01f)
        {
            RaycastHit hit;
            if (Physics.Raycast(_lastPosition, direction.normalized, out hit, distance, _hitLayers))
            {
                OnHit(hit.collider);
                return;
            }
        }

        // Нет попадания - двигаем дальше
        transform.position = newPosition;
        _lastPosition = newPosition;
    }

    private void OnHit(Collider other)
    {
        if (!IsServer) return;

        PlayerNetwork player = other.GetComponent<PlayerNetwork>();
        if (player != null && player.OwnerClientId != OwnerClientId)
        {
            // Передаём ID атакующего
            player.TakeDamageServerRpc(_damage, OwnerClientId);
            Debug.Log($"Projectile hit {player.Nickname.Value} for {_damage} damage!");
        }

        NetworkObject.Despawn(true);
    }

    private void DestroyProjectile()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (NetworkObject == null || !NetworkObject.IsSpawned) return;

        OnHit(other);
    }
}