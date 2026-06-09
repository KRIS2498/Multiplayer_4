using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public abstract class EnemyBase : NetworkBehaviour
{
    [Header("Enemy Base Stats")]
    [SerializeField] protected int _maxHP = 30;
    [SerializeField] protected int _damage = 10;
    [SerializeField] protected float _moveSpeed = 3f;

    public readonly SyncVar<int> CurrentHP = new(30);
    protected int _currentDamage;
    protected float _currentSpeed;
    public int MaxHP => _maxHP;

    public virtual void SetDifficulty(int hp, int damage, float speedMultiplier)
    {
        _currentDamage = damage;
        _currentSpeed = _moveSpeed * speedMultiplier;
        _maxHP = hp;
        CurrentHP.Value = hp;
    }

    public virtual void TakeDamage(int amount, int attackerId)
    {
        if (!base.IsServerInitialized) return;

        CurrentHP.Value -= amount;

        if (CurrentHP.Value <= 0)
        {
            Die(attackerId);
        }
    }

    protected virtual void Die(int killerId)
    {
        base.Despawn();
    }
}
