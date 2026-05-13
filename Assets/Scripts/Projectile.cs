using FishNet.Object;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float _speed = 18f;
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _maxLifetime = 5f;
    [SerializeField] private float _ignoreOwnerTime = 0.2f;

    private float _spawnTimeReal;
    private int _ownerId = -1;
    private Rigidbody _rb;

    public override void OnStartNetwork()
    {
        _spawnTimeReal = Time.time;

        if (base.Owner != null)
        {
            _ownerId = base.Owner.ClientId;
            Debug.Log($"[Projectile] Spawned by owner: {_ownerId}, Damage: {_damage}");
        }

        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
        }

        _rb.isKinematic = false;
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.velocity = transform.forward * _speed;

        if (base.IsServerInitialized)
        {
            Destroy(gameObject, _maxLifetime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;

        Debug.Log($"[Projectile] OnTriggerEnter with: {other.name}, OwnerId: {_ownerId}");

        if (Time.time - _spawnTimeReal < _ignoreOwnerTime)
        {
            PlayerNetwork potentialOwner = other.GetComponent<PlayerNetwork>();
            if (potentialOwner != null && potentialOwner.Owner.ClientId == _ownerId)
            {
                Debug.Log("[Projectile] Ignoring owner (grace period)");
                return;
            }
        }

        PlayerNetwork player = other.GetComponent<PlayerNetwork>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerNetwork>();
        }

        if (player != null)
        {
            if (player.Owner.ClientId != _ownerId && player.IsAlive.Value)
            {
                GameManager gm = FindObjectOfType<GameManager>();
                if (gm != null)
                {
                    gm.ApplyDamageToPlayer(player.Owner.ClientId, _damage, _ownerId);
                }
                Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}