using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Netcode.Components;

public class PlayerNetwork : NetworkBehaviour
{
    [Header("Network Stats")]
    public NetworkVariable<FixedString32Bytes> Nickname = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> HP = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsAlive = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("UI World Space")]
    [SerializeField] private TextMeshPro _nicknameText;
    [SerializeField] private TextMeshPro _hpText;

    [Header("Screen UI")]
    private TextMeshProUGUI _healthScreenText;
    private TextMeshProUGUI _ammoScreenText;

    [Header("Combat Settings")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _shootCooldown = 0.4f;
    [SerializeField] private int _maxAmmo = 10;
    [SerializeField] private int _reloadTime = 2;

    private float _lastShotTime;
    private NetworkVariable<int> _currentAmmo = new NetworkVariable<int>(10);
    private NetworkVariable<bool> _isReloading = new NetworkVariable<bool>(false);
    private Coroutine _reloadCoroutine;

    // UI для таймера респавна
    private GameObject _respawnPanel;
    private TextMeshProUGUI _respawnText;
    private Coroutine _respawnTimerCoroutine;

    private void Start()
    {
        if (_nicknameText == null)
            _nicknameText = GetComponentInChildren<TextMeshPro>();

        Nickname.OnValueChanged += OnNicknameChanged;
        HP.OnValueChanged += OnHpChanged;
        IsAlive.OnValueChanged += OnIsAliveChanged;
        _currentAmmo.OnValueChanged += OnAmmoChanged;
        _isReloading.OnValueChanged += OnReloadingChanged;

        // Находим UI для таймера респавна
        FindRespawnPanel();
    }

    public override void OnNetworkSpawn()
    {
        FindScreenUI();

        // Показываем UI
        if (IsOwner)
        {
            SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);

            if (_healthScreenText != null)
                _healthScreenText.gameObject.SetActive(true);
            if (_ammoScreenText != null)
                _ammoScreenText.gameObject.SetActive(true);
        }

        UpdateNicknameUI(Nickname.Value);
        UpdateHPUI(HP.Value);
        UpdateHealthScreenUI(HP.Value);
        UpdateAmmoScreenUI(_currentAmmo.Value, _maxAmmo);

        if (IsOwner && _firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = new Vector3(0, 1.5f, 0.8f);
            _firePoint = firePointObj.transform;
        }
    }

    private void FindScreenUI()
    {
        if (_healthScreenText != null && _ammoScreenText != null)
        {
            return;
        }

        // Ищем Canvas в сцене
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        // Ищем HealthText
        if (_healthScreenText == null)
        {
            Transform healthTransform = canvas.transform.Find("HealthText");
            if (healthTransform != null)
            {
                _healthScreenText = healthTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        // Ищем AmmoText
        if (_ammoScreenText == null)
        {
            Transform ammoTransform = canvas.transform.Find("AmmoText");
            if (ammoTransform != null)
            {
                _ammoScreenText = ammoTransform.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    private void FindRespawnPanel()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Transform panelTransform = canvas.transform.Find("RespawnPanel");
            if (panelTransform != null)
            {
                _respawnPanel = panelTransform.gameObject;
                _respawnText = panelTransform.GetComponentInChildren<TextMeshProUGUI>();
                if (_respawnPanel != null)
                    _respawnPanel.SetActive(false);
            }
        }
    }

    private void ShowRespawnTimer(int secondsRemaining)
    {
        if (_respawnPanel != null)
        {
            _respawnPanel.SetActive(true);
            if (_respawnText != null)
            {
                _respawnText.text = $"RESPAWN IN: {secondsRemaining}s";
            }
        }
    }

    private void HideRespawnTimer()
    {
        if (_respawnPanel != null)
        {
            _respawnPanel.SetActive(false);
        }
    }

    private void UpdateHealthScreenUI(int health)
    {
        if (_healthScreenText != null && IsOwner)
        {
            _healthScreenText.text = $"HEALTH: {health}";

            if (health <= 30)
                _healthScreenText.color = Color.red;
            else if (health <= 60)
                _healthScreenText.color = Color.yellow;
            else
                _healthScreenText.color = Color.white;
        }
    }

    private void UpdateAmmoScreenUI(int currentAmmo, int maxAmmo)
    {
        if (_ammoScreenText != null && IsOwner)
        {
            _ammoScreenText.text = $"AMMO: {currentAmmo}/{maxAmmo}";

            if (currentAmmo <= 3)
                _ammoScreenText.color = Color.red;
            else if (currentAmmo <= 6)
                _ammoScreenText.color = Color.yellow;
            else
                _ammoScreenText.color = Color.white;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitNicknameServerRpc(string nickname)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname)
            ? $"Player_{OwnerClientId}"
            : nickname.Trim();

        Nickname.Value = safeValue;
        Debug.Log($"Player {OwnerClientId} set nickname: {safeValue}");
    }

    private void OnNicknameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        UpdateNicknameUI(newValue);
    }

    private void OnHpChanged(int oldValue, int newValue)
    {
        UpdateHPUI(newValue);

        // Обновляем UI только для владельца
        if (IsOwner)
        {
            UpdateHealthScreenUI(newValue);
        }

        if (newValue < oldValue && IsOwner)
        {
            StartCoroutine(DamageFlashEffect());
        }

        // Сервер обрабатывает смерть
        if (IsServer && newValue <= 0 && IsAlive.Value)
        {
            IsAlive.Value = false;
            StartCoroutine(RespawnRoutine());
        }
    }

    private IEnumerator RespawnRoutine()
    {

        yield return new WaitForSeconds(3f);

        if (!IsServer) yield break;

        // Находим все точки спавна
        Transform[] spawnPoints = FindSpawnPoints();
        Vector3 respawnPosition;

        if (spawnPoints.Length > 0)
        {
            int idx = Random.Range(0, spawnPoints.Length);
            respawnPosition = spawnPoints[idx].position;
        }
        else
        {
            respawnPosition = new Vector3(0, 1, 0);
        }

        // Отключаем CharacterController перед телепортацией
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        // Телепортируем игрока
        transform.position = respawnPosition;

        // Синхронизируем позицию для NetworkTransform
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null)
        {
            // Обновляем позицию в сети
            networkTransform.SetState(transform.position, transform.rotation, transform.localScale);
        }

        // Включаем CharacterController обратно
        if (cc != null)
        {
            cc.enabled = true;
        }

        // Сбрасываем скорость и гравитацию
        if (cc != null)
        {
            cc.Move(Vector3.zero);
        }

        // Восстанавливаем характеристики
        HP.Value = 100;
        _currentAmmo.Value = _maxAmmo;
        _isReloading.Value = false;
        IsAlive.Value = true;

        UpdatePositionClientRpc(respawnPosition);
    }

    [ClientRpc]
    private void UpdatePositionClientRpc(Vector3 newPosition)
    {
        // Принудительно обновляем позицию на клиентах
        if (!IsServer)
        {
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                transform.position = newPosition;
                cc.enabled = true;
            }
            else
            {
                transform.position = newPosition;
            }
        }
    }

    private void OnIsAliveChanged(bool oldValue, bool newValue)
    {

        if (!newValue)
        {
            // Игрок умер
            SetPlayerVisible(false);

            Collider collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (IsOwner)
            {
                StartRespawnTimer();
            }
        }
        else
        {
            // Игрок возродился
            SetPlayerVisible(true);

            Collider collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = true;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            if (IsOwner)
            {
                StopRespawnTimer();
            }
        }
    }

    private void StartRespawnTimer()
    {
        if (_respawnTimerCoroutine != null)
            StopCoroutine(_respawnTimerCoroutine);
        _respawnTimerCoroutine = StartCoroutine(RespawnTimerCoroutine());
    }

    private void StopRespawnTimer()
    {
        if (_respawnTimerCoroutine != null)
        {
            StopCoroutine(_respawnTimerCoroutine);
            _respawnTimerCoroutine = null;
        }
        HideRespawnTimer();
    }

    private IEnumerator RespawnTimerCoroutine()
    {
        float timer = 3f;
        while (timer > 0)
        {
            ShowRespawnTimer(Mathf.CeilToInt(timer));
            timer -= Time.deltaTime;
            yield return null;
        }
        HideRespawnTimer();
    }

    private void SetPlayerVisible(bool visible)
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = visible;
        }

        SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            renderer.enabled = visible;
        }

        if (_nicknameText != null) _nicknameText.enabled = visible;
        if (_hpText != null) _hpText.enabled = visible;
    }

    private Transform[] FindSpawnPoints()
    {
        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");


        Transform[] spawnPoints = new Transform[spawnPointObjects.Length];
        for (int i = 0; i < spawnPointObjects.Length; i++)
        {
            spawnPoints[i] = spawnPointObjects[i].transform;
        }

        return spawnPoints;
    }

    private void OnAmmoChanged(int oldValue, int newValue)
    {
        if (IsOwner)
        {
            UpdateAmmoScreenUI(newValue, _maxAmmo);
        }
    }

    private void OnReloadingChanged(bool oldValue, bool newValue)
    {
        if (IsOwner && newValue == true)
        {
            if (_ammoScreenText != null)
            {
                _ammoScreenText.text = $"RELOADING...";
                _ammoScreenText.color = Color.yellow;
            }
        }
        else if (IsOwner && newValue == false && oldValue == true)
        {
            UpdateAmmoScreenUI(_currentAmmo.Value, _maxAmmo);
        }
    }

    private void UpdateNicknameUI(FixedString32Bytes nickname)
    {
        if (_nicknameText != null)
        {
            _nicknameText.text = nickname.ToString();
        }
    }

    private void UpdateHPUI(int hp)
    {
        if (_hpText != null)
        {
            _hpText.text = $"HP: {hp}";

            if (hp <= 30)
                _hpText.color = Color.red;
            else if (hp <= 60)
                _hpText.color = Color.yellow;
            else
                _hpText.color = Color.green;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage, ulong attackerId)
    {
        if (!IsServer) return;
        if (!IsAlive.Value) return;

        // Не наносим урон самому себе
        if (OwnerClientId == attackerId) return;

        int newHP = HP.Value - damage;
        HP.Value = Mathf.Max(0, newHP);
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (!IsAlive.Value) return;

        if (Input.GetMouseButtonDown(1))
        {
            ShootServerRpc(_firePoint.position, _firePoint.forward);
        }

        if (Input.GetKeyDown(KeyCode.R) && !_isReloading.Value)
        {
            ReloadServerRpc();
        }
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 pos, Vector3 dir, ServerRpcParams rpcParams = default)
    {
        if (!IsAlive.Value) return;
        if (HP.Value <= 0) return;
        if (_currentAmmo.Value <= 0) return;
        if (Time.time < _lastShotTime + _shootCooldown) return;
        if (_isReloading.Value) return;

        _lastShotTime = Time.time;
        _currentAmmo.Value--;

        if (_projectilePrefab != null)
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
        if (!IsAlive.Value) return;
        if (_isReloading.Value || _currentAmmo.Value == _maxAmmo) return;

        if (_reloadCoroutine != null)
            StopCoroutine(_reloadCoroutine);
        _reloadCoroutine = StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        _isReloading.Value = true;

        float elapsed = 0;
        while (elapsed < _reloadTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _currentAmmo.Value = _maxAmmo;
        _isReloading.Value = false;
    }

    private IEnumerator DamageFlashEffect()
    {
        if (_healthScreenText != null)
        {
            Color originalColor = _healthScreenText.color;
            _healthScreenText.color = Color.red;
            yield return new WaitForSeconds(0.2f);
            _healthScreenText.color = originalColor;
        }

        if (_hpText != null)
        {
            Color originalColor = _hpText.color;
            _hpText.color = Color.red;
            yield return new WaitForSeconds(0.2f);
            _hpText.color = originalColor;
        }
    }

    private void OnDestroy()
    {
        if (Nickname != null)
            Nickname.OnValueChanged -= OnNicknameChanged;
        if (HP != null)
            HP.OnValueChanged -= OnHpChanged;
        if (IsAlive != null)
            IsAlive.OnValueChanged -= OnIsAliveChanged;
        if (_currentAmmo != null)
            _currentAmmo.OnValueChanged -= OnAmmoChanged;
        if (_isReloading != null)
            _isReloading.OnValueChanged -= OnReloadingChanged;
    }
}