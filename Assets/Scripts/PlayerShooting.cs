using Unity.Netcode;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _cooldown = 0.4f;
    [SerializeField] private int _maxAmmo = 10;
    [SerializeField] private int _reloadTime = 2;

    private float _lastShotTime;
    private NetworkVariable<int> _currentAmmo = new NetworkVariable<int>(10);
    private NetworkVariable<bool> _isReloading = new NetworkVariable<bool>(false);

    private PlayerNetwork _playerNetwork;

    private void Awake()
    {
        _playerNetwork = GetComponent<PlayerNetwork>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Установка начального количества патронов
            SetMaxAmmoServerRpc(_maxAmmo);
        }

        // Подписываемся на изменения
        _currentAmmo.OnValueChanged += OnAmmoChanged;
    }

    [ServerRpc]
    private void SetMaxAmmoServerRpc(int maxAmmo)
    {
        _currentAmmo.Value = maxAmmo;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Стрельба
        if (Input.GetButtonDown("Fire1"))
        {
            ShootServerRpc(_firePoint.position, _firePoint.forward);
        }

        // Перезарядка
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading.Value)
        {
            ReloadServerRpc();
        }

        // Отладка
        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            Debug.Log($"Current ammo: {_currentAmmo.Value}");
        }
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 pos, Vector3 dir, ServerRpcParams rpcParams = default)
    {
        // Проверка на жизнь
        if (_playerNetwork != null && _playerNetwork.HP.Value <= 0)
        {
            return;
        }

        // Проверка патронов
        if (_currentAmmo.Value <= 0)
        {
            return;
        }

        // Проверка кулдауна
        if (Time.time < _lastShotTime + _cooldown)
        {
            return;
        }

        // Проверка перезарядки
        if (_isReloading.Value)
        {
            return;
        }

        // Все проверки пройдены - стреляем
        _lastShotTime = Time.time;
        _currentAmmo.Value--;

        // Спавним снаряд
        if (_projectilePrefab != null && _firePoint != null)
        {
            GameObject projectile = Instantiate(_projectilePrefab, pos + dir * 1.2f, Quaternion.LookRotation(dir));
            NetworkObject networkObject = projectile.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
            }
        }
    }

    [ServerRpc]
    private void ReloadServerRpc(ServerRpcParams rpcParams = default)
    {
        if (_isReloading.Value || _currentAmmo.Value == _maxAmmo)
            return;

        StartCoroutine(ReloadCoroutine());
    }

    private System.Collections.IEnumerator ReloadCoroutine()
    {
        _isReloading.Value = true;

        yield return new WaitForSeconds(_reloadTime);

        _currentAmmo.Value = _maxAmmo;
        _isReloading.Value = false;
    }

    private void OnAmmoChanged(int oldValue, int newValue)
    {
        if (IsOwner)
        {
            Debug.Log($"Ammo: {newValue}/{_maxAmmo}");
        }
    }

    public override void OnNetworkDespawn()
    {
        _currentAmmo.OnValueChanged -= OnAmmoChanged;
    }
}