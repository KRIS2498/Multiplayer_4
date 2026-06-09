using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerCombat : NetworkBehaviour
{
    [Header("Combat Settings")]
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _attackCooldown = 1f;

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("References")]
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private Transform _attackPoint;

    private float _lastAttackTime;
    private bool _canAttack = true;

    private InputActionMap _actionMap;
    private InputAction _attackAction;

    private void Start()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        if (_attackPoint == null)
        {
            GameObject point = new GameObject("AttackPoint");
            point.transform.parent = transform;
            point.transform.localPosition = new Vector3(0, 1, 0);
            _attackPoint = point.transform;
        }
    }

    public override void OnStartNetwork()
    {
        if (!base.Owner.IsLocalClient) return;
        SetupInputActions();
    }

    private void SetupInputActions()
    {
        if (_inputActions == null)
        {
            Debug.LogWarning("[PlayerCombat] InputActions not assigned!");
            return;
        }

        _actionMap = _inputActions.FindActionMap("Player");
        if (_actionMap == null)
        {
            Debug.LogWarning("[PlayerCombat] Player action map not found!");
            return;
        }

        _attackAction = _actionMap.FindAction("Attack");
        if (_attackAction == null)
        {
            Debug.LogWarning("[PlayerCombat] Attack action not found!");
            return;
        }

        _attackAction.performed += OnAttack;
        _attackAction.Enable();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!base.IsOwner) return;
        if (!_canAttack) return;
        if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;

        TryAttack();
    }

    private void TryAttack()
    {
        if (!_canAttack) return;

        PlayerNetwork playerTarget = FindPlayerTarget();
        EnemyBase enemyTarget = FindEnemyTarget();

        if (playerTarget != null)
        {
            TowerManager tower = TowerManager.Instance;
            if (tower != null)
            {
                tower.ServerApplyDamage(playerTarget.Owner.ClientId, _damage, base.Owner.ClientId);
            }

            _canAttack = false;
            StartCoroutine(ResetAttackCoroutine());
        }
        else if (enemyTarget != null)
        {
            int enemyId = enemyTarget.GetComponent<NetworkObject>().ObjectId;
            AttackEnemyServerRpc(enemyId, _damage, base.Owner.ClientId);

            _canAttack = false;
            StartCoroutine(ResetAttackCoroutine());
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void AttackEnemyServerRpc(int enemyObjectId, int damage, int attackerId)
    {
        if (!base.ServerManager.Objects.Spawned.TryGetValue(enemyObjectId, out NetworkObject targetObject))
            return;

        EnemyBase enemy = targetObject.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, attackerId);
        }
    }

    private EnemyBase FindEnemyTarget()
    {
        EnemyBase[] allEnemies = FindObjectsOfType<EnemyBase>();

        EnemyBase closest = null;
        float closestDistance = _attackRange;

        foreach (var enemy in allEnemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance <= closestDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closest = enemy;
            }
        }

        return closest;
    }

    private IEnumerator ResetAttackCoroutine()
    {
        yield return new WaitForSeconds(_attackCooldown);
        _canAttack = true;
    }

    private PlayerNetwork FindPlayerTarget()
    {
        PlayerNetwork[] allPlayers = FindObjectsOfType<PlayerNetwork>();

        PlayerNetwork closestTarget = null;
        float closestDistance = _attackRange;

        foreach (PlayerNetwork player in allPlayers)
        {
            if (player == _playerNetwork) continue;
            if (!player.IsAlive.Value) continue;

            float distance = Vector3.Distance(transform.position, player.transform.position);

            if (distance <= closestDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = player;
            }
        }

        return closestTarget;
    }

    private void OnDisable()
    {
        if (_attackAction != null && base.IsOwner)
        {
            _attackAction.performed -= OnAttack;
            _attackAction.Disable();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_attackPoint != null ? _attackPoint.position : transform.position, _attackRange);
    }
}
