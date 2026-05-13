using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _cooldown = 0.4f;
    [SerializeField] private int _maxAmmo = 10;
    [SerializeField] private int _reloadTime = 2;

    private float _lastShotTime;
    private bool _canShoot = true;
    private float _shootDelayTimer = 0f;

    public readonly SyncVar<int> _currentAmmo = new(10);
    public readonly SyncVar<bool> _isReloading = new(false);

    private PlayerNetwork _playerNetwork;
    private Coroutine _reloadCoroutine;

    // События для UI в PlayerNetwork
    public System.Action<int, int> OnAmmoChanged;
    public System.Action<bool> OnReloadingChanged;

    private void Awake()
    {
        _playerNetwork = GetComponent<PlayerNetwork>();

        if (_firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = new Vector3(0, 1.5f, 0.8f);
            _firePoint = firePointObj.transform;
        }
    }

    public override void OnStartNetwork()
    {
        if (base.Owner.IsLocalClient)
        {
            SetMaxAmmoServerRpc(_maxAmmo);
        }

        // Подписываемся на изменения SyncVar
        _currentAmmo.OnChange += OnAmmoChangedHandler;
        _isReloading.OnChange += OnReloadingChangedHandler;

        // Вызываем начальное состояние
        OnAmmoChangedHandler(_currentAmmo.Value, _currentAmmo.Value, false);
        OnReloadingChangedHandler(_isReloading.Value, _isReloading.Value, false);
    }

    private void OnAmmoChangedHandler(int oldValue, int newValue, bool asServer)
    {
        Debug.Log($"Ammo changed: {newValue}/{_maxAmmo}");
        OnAmmoChanged?.Invoke(newValue, _maxAmmo);
    }

    private void OnReloadingChangedHandler(bool oldValue, bool newValue, bool asServer)
    {
        Debug.Log($"Reloading changed: {newValue}");
        OnReloadingChanged?.Invoke(newValue);
    }

    [ServerRpc]
    private void SetMaxAmmoServerRpc(int maxAmmo)
    {
        _currentAmmo.Value = maxAmmo;
    }

    private void Update()
    {
        if (!base.IsOwner) return;

        // Обновляем таймер кулдауна
        if (!_canShoot)
        {
            _shootDelayTimer -= Time.deltaTime;
            if (_shootDelayTimer <= 0f)
            {
                _canShoot = true;
            }
        }

        if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;

        // Стрельба (ПКМ)
        if (Input.GetMouseButtonDown(1) && _canShoot && !_isReloading.Value && _currentAmmo.Value > 0)
        {
            ShootServerRpc(_firePoint.position, _firePoint.forward);

            _canShoot = false;
            _shootDelayTimer = _cooldown;
        }

        // Перезарядка (R)
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading.Value && _currentAmmo.Value != _maxAmmo)
        {
            ReloadServerRpc();
        }
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 pos, Vector3 dir)
    {
        if (_playerNetwork != null && _playerNetwork.HP.Value <= 0) return;
        if (_currentAmmo.Value <= 0) return;
        if (_isReloading.Value) return;
        if (Time.time < _lastShotTime + _cooldown) return;

        _lastShotTime = Time.time;
        _currentAmmo.Value--;

        if (_projectilePrefab != null && _firePoint != null)
        {
            Vector3 spawnPosition = pos + dir * 0.5f;

            GameObject projectile = Instantiate(_projectilePrefab, spawnPosition, Quaternion.LookRotation(dir));

            // ? КРИТИЧЕСКИ ВАЖНО: Передаём владельца при спавне снаряда!
            NetworkObject networkObject = projectile.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                base.Spawn(networkObject, base.Owner); // ? Добавлен base.Owner
                Debug.Log($"[PlayerShooting] Projectile spawned with owner: {base.Owner.ClientId}");
            }
            else
            {
                Debug.LogError("[PlayerShooting] Projectile prefab missing NetworkObject component!");
            }
        }
    }

    [ServerRpc]
    private void ReloadServerRpc()
    {
        if (_isReloading.Value || _currentAmmo.Value == _maxAmmo) return;

        if (_reloadCoroutine != null)
            StopCoroutine(_reloadCoroutine);
        _reloadCoroutine = StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        _isReloading.Value = true;

        yield return new WaitForSeconds(_reloadTime);

        _currentAmmo.Value = _maxAmmo;
        _isReloading.Value = false;
    }

    public void ResetAmmo()
    {
        if (!base.IsServerInitialized) return;

        _currentAmmo.Value = _maxAmmo;
        _isReloading.Value = false;

        if (_reloadCoroutine != null)
            StopCoroutine(_reloadCoroutine);

        Debug.Log($"[PlayerShooting] Ammo reset to {_maxAmmo}");
    }
}