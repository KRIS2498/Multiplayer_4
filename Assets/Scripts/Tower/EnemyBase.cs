using FishNet.Object;
using UnityEngine;

public abstract class EnemyBase : NetworkBehaviour
{
    [Header("Enemy Base Stats")]
    [SerializeField] protected int _maxHP = 30;
    [SerializeField] protected int _damage = 10;
    [SerializeField] protected float _moveSpeed = 3f;

    protected int _currentHP;
    protected int _currentDamage;
    protected float _currentSpeed;

    public virtual void SetDifficulty(int hp, int damage, float speedMultiplier)
    {
        _maxHP = hp;
        _damage = damage;
        _moveSpeed *= speedMultiplier;

        _currentHP = _maxHP;
        _currentDamage = _damage;
        _currentSpeed = _moveSpeed;
    }

    public virtual void TakeDamage(int amount, int attackerId)
    {
        if (!base.IsServerInitialized) return;

        _currentHP -= amount;
        Debug.Log($"[Enemy] Took {amount} damage, HP: {_currentHP}/{_maxHP}");

        if (_currentHP <= 0)
        {
            Die(attackerId);
        }
    }

    protected virtual void Die(int killerId)
    {
        base.Despawn();
    }
}
