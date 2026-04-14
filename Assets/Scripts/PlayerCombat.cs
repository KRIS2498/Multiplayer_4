using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : NetworkBehaviour
{
    [Header("Combat Settings")]
    [SerializeField] private int _damage = 10;
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

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        SetupInputActions();
    }

    private void SetupInputActions()
    {
        if (_inputActions == null)
        {
            return;
        }

        _actionMap = _inputActions.FindActionMap("Player");
        if (_actionMap == null)
        {
            return;
        }

        // Находим Action для атаки
        _attackAction = _actionMap.FindAction("Attack");
        if (_attackAction == null)
        {
            return;
        }

        // Подписываемся на событие атаки
        _attackAction.performed += OnAttack;
        _attackAction.Enable();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (!_canAttack) return;

        TryAttack();
    }

    private void TryAttack()
    {
        if (!_canAttack) return;

        PlayerNetwork target = FindTarget();

        if (target != null)
        {
            DealDamageServerRpc(target.NetworkObjectId, _damage);

            _canAttack = false;
            Invoke(nameof(ResetAttack), _attackCooldown);
        }
    }

    private PlayerNetwork FindTarget()
    {
        PlayerNetwork[] allPlayers = FindObjectsOfType<PlayerNetwork>();

        PlayerNetwork closestTarget = null;
        float closestDistance = _attackRange;

        foreach (PlayerNetwork player in allPlayers)
        {
            if (player == _playerNetwork) continue;

            float distance = Vector3.Distance(transform.position, player.transform.position);

            if (distance <= _attackRange && distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = player;
            }
        }

        return closestTarget;
    }

    private void ResetAttack()
    {
        _canAttack = true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void DealDamageServerRpc(ulong targetObjectId, int damage)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetObjectId, out NetworkObject targetObject))
        {
            Debug.LogWarning("Цель не найдена!");
            return;
        }

        PlayerNetwork targetPlayer = targetObject.GetComponent<PlayerNetwork>();

        if (targetPlayer == null || targetPlayer == _playerNetwork)
        {
            Debug.LogWarning("Нельзя атаковать себя!");
            return;
        }

        int newHP = Mathf.Max(0, targetPlayer.HP.Value - damage);
        targetPlayer.HP.Value = newHP;

        Debug.Log($"Игрок {targetPlayer.Nickname.Value} получил {damage} урона. Осталось HP: {newHP}");

        if (newHP == 0)
        {
            Debug.Log($"Игрок {targetPlayer.Nickname.Value} погиб!");
        }
    }

    private void OnDisable()
    {
        if (_attackAction != null && IsOwner)
        {
            _attackAction.performed -= OnAttack;
            _attackAction.Disable();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }
}