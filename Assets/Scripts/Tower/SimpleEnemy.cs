using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleEnemy : EnemyBase
{
    [Header("Combat")]
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private float _attackCooldown = 1f;

    private CharacterController _controller;
    private Transform _target;
    private float _lastAttackTime;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _currentHP = _maxHP;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        _currentHP = _maxHP;
        _currentDamage = _damage;
        _currentSpeed = _moveSpeed;
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;
        if (_currentHP <= 0) return;

        FindTarget();
        if (_target == null) return;

        float distance = Vector3.Distance(transform.position, _target.position);

        if (distance > _attackRange)
        {
            ChaseTarget();
        }
        else
        {
            AttackTarget();
        }
    }

    private void FindTarget()
    {
        var players = FindObjectsOfType<PlayerNetwork>();
        float closestDist = float.MaxValue;
        Transform closest = null;

        foreach (var p in players)
        {
            if (!p.IsAlive.Value) continue;
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = p.transform;
            }
        }

        _target = closest;
    }

    private void ChaseTarget()
    {
        Vector3 direction = (_target.position - transform.position).normalized;
        direction.y = 0;

        if (_controller.isGrounded)
        {
            _controller.Move(direction * _currentSpeed * Time.deltaTime);
        }
        else
        {
            _controller.Move(direction * _currentSpeed * Time.deltaTime + Physics.gravity * Time.deltaTime);
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            Time.deltaTime * 5f);
    }

    private void AttackTarget()
    {
        if (Time.time < _lastAttackTime + _attackCooldown) return;
        _lastAttackTime = Time.time;

        PlayerNetwork player = _target.GetComponent<PlayerNetwork>();
        if (player != null && player.IsAlive.Value)
        {
            TowerManager tower = TowerManager.Instance;
            if (tower != null)
            {
                tower.ServerApplyDamage(player.Owner.ClientId, _currentDamage, -1);
            }
            else
            {
                player.ApplyDamage(_currentDamage, -1);
            }
        }
    }

    public override void TakeDamage(int amount, int attackerId)
    {
        base.TakeDamage(amount, attackerId);

        TowerManager tower = TowerManager.Instance;
        if (tower != null)
        {
            tower.ServerApplyDamage(attackerId, 0, attackerId);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }
}
