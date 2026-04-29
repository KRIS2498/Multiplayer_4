using FishNet.Object;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float _speed = 18f;
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _maxLifetime = 5f;
    [SerializeField] private LayerMask _hitLayers = -1;
    [SerializeField] private float _ignoreOwnerTime = 0.2f; // Время игнорирования владельца

    private float _spawnTime;
    private Vector3 _lastPosition;
    private float _spawnTimeReal;
    private int _ownerId = -1;

    public override void OnStartNetwork()
    {
        _spawnTime = Time.time;
        _spawnTimeReal = Time.time;
        _lastPosition = transform.position;
        _ownerId = base.Owner.ClientId;

        // Автоматически уничтожить снаряд через время (только на сервере)
        if (base.IsServerInitialized)
        {
            Invoke(nameof(DestroyProjectile), _maxLifetime);
        }
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;

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
        if (!base.IsServerInitialized) return;

        // Игнорируем владельца в течение короткого времени после выстрела
        if (Time.time - _spawnTimeReal < _ignoreOwnerTime)
        {
            // Проверяем, является ли объект владельцем
            PlayerNetwork potentialOwner = other.GetComponent<PlayerNetwork>();
            if (potentialOwner != null && potentialOwner.Owner.ClientId == _ownerId)
            {
                Debug.Log("Игнорируем столкновение с владельцем");
                return; // Пропускаем попадание в себя
            }
        }

        // Проверяем, что попали в игрока
        if (other.TryGetComponent<PlayerNetwork>(out PlayerNetwork player))
        {
            // Не наносим урон себе (дополнительная проверка)
            if (player.Owner.ClientId != _ownerId)
            {
                player.TakeDamageServerRpc(_damage, _ownerId);
                Debug.Log($"Projectile hit {player.Nickname.Value} for {_damage} damage!");
                base.Despawn();
            }
        }
        else
        {
            // Попадание в стену или другой объект
            base.Despawn();
        }
    }

    private void DestroyProjectile()
    {
        if (base.IsSpawned)
        {
            base.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;
        if (!base.IsSpawned) return;

        OnHit(other);
    }
}