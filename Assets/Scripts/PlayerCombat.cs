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

        Debug.Log("[PlayerCombat] Attack input enabled!");
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

        PlayerNetwork target = FindTarget();

        if (target != null)
        {
            Debug.Log($"[PlayerCombat] Attacking {target.Nickname.Value}!");
            
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.ServerApplyDamage(target.Owner.ClientId, _damage, base.Owner.ClientId);
            }

            _canAttack = false;
            StartCoroutine(ResetAttackCoroutine());
        }
    }

    private IEnumerator ResetAttackCoroutine()
    {
        yield return new WaitForSeconds(_attackCooldown);
        _canAttack = true;
    }

    private PlayerNetwork FindTarget()
    {
        PlayerNetwork[] allPlayers = FindObjectsOfType<PlayerNetwork>();

        PlayerNetwork closestTarget = null;
        float closestDistance = _attackRange;

        foreach (PlayerNetwork player in allPlayers)
        {
            if (player == _playerNetwork) continue;
            if (!player.IsAlive.Value) continue; // ���������� ������

            float distance = Vector3.Distance(transform.position, player.transform.position);

            if (distance <= _attackRange && distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = player;
            }
        }

        return closestTarget;
    }

    [ServerRpc(RequireOwnership = false)]
    private void DealDamageServerRpc(int targetObjectId, int damage, int attackerId)
    {
        Debug.Log($"[PlayerCombat] DealDamageServerRpc CALLED! TargetId={targetObjectId}, Damage={damage}, Attacker={attackerId}");

        if (!base.IsServerInitialized) return;

        if (!base.ServerManager.Objects.Spawned.TryGetValue(targetObjectId, out NetworkObject targetObject))
        {
            Debug.LogWarning("[PlayerCombat] Target not found in Spawned objects!");
            return;
        }

        PlayerNetwork targetPlayer = targetObject.GetComponent<PlayerNetwork>();

        if (targetPlayer == null)
        {
            Debug.LogWarning("[PlayerCombat] Target has no PlayerNetwork component!");
            return;
        }

        if (targetPlayer == _playerNetwork)
        {
            Debug.LogWarning("[PlayerCombat] Cannot attack self!");
            return;
        }

        if (!targetPlayer.IsAlive.Value)
        {
            Debug.Log($"[PlayerCombat] {targetPlayer.Nickname.Value} is already dead!");
            return;
        }

        Debug.Log($"[PlayerCombat] Calling ServerApplyDamage on {targetPlayer.Nickname.Value}");
        targetPlayer.ServerApplyDamage(damage, attackerId);
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