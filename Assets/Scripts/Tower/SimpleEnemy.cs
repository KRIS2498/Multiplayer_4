using FishNet.Object;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class SimpleEnemy : EnemyBase
{
    [Header("Combat")]
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private float _attackCooldown = 1f;

    [Header("HP Bar (назначь в префабе)")]
    [SerializeField] private Slider _hpSlider;

    private CharacterController _controller;
    private Transform _target;
    private float _lastAttackTime;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        CurrentHP.Value = _maxHP;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        CurrentHP.Value = _maxHP;
        _currentDamage = _damage;
        _currentSpeed = _moveSpeed;

        CurrentHP.OnChange += OnHPChanged;
        OnHPChanged(CurrentHP.Value, CurrentHP.Value, false);
    }

    private void OnDestroy()
    {
        CurrentHP.OnChange -= OnHPChanged;
    }

    private void OnHPChanged(int prev, int next, bool asServer)
    {
        if (_hpSlider == null) return;

        float maxHP = _maxHP > 0 ? _maxHP : 1;
        _hpSlider.value = (float)next / maxHP;
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;
        if (CurrentHP.Value <= 0) return;

        FindTarget();
        if (_target == null) return;

        float distance = Vector3.Distance(transform.position, _target.position);

        if (distance > _attackRange)
            ChaseTarget();
        else
            AttackTarget();
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
            _controller.Move(direction * _currentSpeed * Time.deltaTime);
        else
            _controller.Move(direction * _currentSpeed * Time.deltaTime + Physics.gravity * Time.deltaTime);

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                Time.deltaTime * 5f);
        }
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
                tower.ServerApplyDamage(player.Owner.ClientId, _currentDamage, -1);
        }
    }

    public override void TakeDamage(int amount, int attackerId)
    {
        base.TakeDamage(amount, attackerId);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }
}
