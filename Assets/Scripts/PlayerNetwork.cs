using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerNetwork : NetworkBehaviour
{
    public static event System.Action<PlayerNetwork> OnAnyPlayerDied;
    [Header("Network Stats")]
    public readonly SyncVar<string> Nickname = new("Player");
    public readonly SyncVar<int> HP = new(100);
    public readonly SyncVar<bool> IsAlive = new(true);
    public readonly SyncVar<int> Score = new(0);
    public readonly SyncVar<int> Deaths = new(0);
    public readonly SyncVar<int> Coins = new(0);

    [Header("UI World Space")]
    [SerializeField] private TextMeshPro _nicknameText;
    [SerializeField] private TextMeshPro _hpText;

    [Header("Screen UI")]
    private TextMeshProUGUI _healthScreenText;
    private TextMeshProUGUI _ammoScreenText;
    private TextMeshProUGUI _coinsScreenText;

    // ������ �� ������ �������� ��� ��������
    private PlayerShooting _playerShooting;
    
    private GameObject _respawnPanel;
    private TextMeshProUGUI _respawnText;
    private Coroutine _respawnTimerCoroutine;

    private void Start()
    {
        if (_nicknameText == null)
            _nicknameText = GetComponentInChildren<TextMeshPro>();

        FindRespawnPanel();

        Nickname.OnChange += OnNicknameChanged;
        HP.OnChange += OnHpChanged;
        IsAlive.OnChange += OnIsAliveChanged;
    }

    public override void OnStartNetwork()
    {
        FindScreenUI();

        if (base.Owner.IsLocalClient)
        {
            SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);

            if (_healthScreenText != null)
                _healthScreenText.gameObject.SetActive(true);
            if (_ammoScreenText != null)
                _ammoScreenText.gameObject.SetActive(true);
            if (_coinsScreenText != null)
                _coinsScreenText.gameObject.SetActive(true);
        }

        UpdateNicknameUI(Nickname.Value);
        UpdateHPUI(HP.Value);
        UpdateHealthScreenUI(HP.Value);
        UpdateCoinsScreenUI(Coins.Value);

        Coins.OnChange += OnCoinsChanged;

        _playerShooting = GetComponent<PlayerShooting>();
        if (_playerShooting != null && base.Owner.IsLocalClient)
        {
            _playerShooting.OnAmmoChanged += UpdateAmmoScreenUI;
            _playerShooting.OnReloadingChanged += OnReloadingChanged;
        }
    }

    private void FindScreenUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (_healthScreenText == null)
        {
            Transform healthTransform = canvas.transform.Find("HealthText");
            if (healthTransform != null)
                _healthScreenText = healthTransform.GetComponent<TextMeshProUGUI>();
        }

        if (_ammoScreenText == null)
        {
            Transform ammoTransform = canvas.transform.Find("AmmoText");
            if (ammoTransform != null)
                _ammoScreenText = ammoTransform.GetComponent<TextMeshProUGUI>();
        }

        if (_coinsScreenText == null)
        {
            Transform coinsTransform = canvas.transform.Find("CoinsText");
            if (coinsTransform != null)
                _coinsScreenText = coinsTransform.GetComponent<TextMeshProUGUI>();
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
                _respawnText.text = $"RESPAWN IN: {secondsRemaining}s";
        }
    }

    private void HideRespawnTimer()
    {
        if (_respawnPanel != null)
            _respawnPanel.SetActive(false);
    }

    private void UpdateHealthScreenUI(int health)
    {
        if (_healthScreenText != null && base.Owner.IsLocalClient)
        {
            _healthScreenText.text = $"HEALTH: {health}";
            _healthScreenText.color = health <= 30 ? Color.red : (health <= 60 ? Color.yellow : Color.white);
        }
    }

    private void UpdateAmmoScreenUI(int currentAmmo, int maxAmmo)
    {
        if (_ammoScreenText != null && base.Owner.IsLocalClient)
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

    private void OnReloadingChanged(bool isReloading)
    {
        if (_ammoScreenText != null && base.Owner.IsLocalClient && isReloading)
        {
            _ammoScreenText.text = $"RELOADING...";
            _ammoScreenText.color = Color.yellow;
        }
    }

    private void OnCoinsChanged(int oldValue, int newValue, bool asServer)
    {
        UpdateCoinsScreenUI(newValue);
    }

    private void UpdateCoinsScreenUI(int coins)
    {
        if (_coinsScreenText != null && base.Owner.IsLocalClient)
        {
            _coinsScreenText.text = $"COINS: {coins}";
            _coinsScreenText.color = new Color(1f, 0.84f, 0f);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitNicknameServerRpc(string nickname)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname) 
            ? $"Player_{base.Owner.ClientId}" 
            : nickname.Trim();
        Nickname.Value = safeValue;
        Debug.Log($"Player {base.Owner.ClientId} set nickname: {safeValue}");

        if (TowerManager.Instance != null)
            TowerManager.Instance.LoadPlayerCoins(safeValue, this);
    }

    private void OnNicknameChanged(string oldValue, string newValue, bool asServer)
    {
        UpdateNicknameUI(newValue);
    }

    private void OnHpChanged(int oldValue, int newValue, bool asServer)
    {
        UpdateHPUI(newValue);

        if (base.Owner.IsLocalClient)
            UpdateHealthScreenUI(newValue);

        if (newValue < oldValue && base.Owner.IsLocalClient)
            StartCoroutine(DamageFlashEffect());

        if (base.IsServerInitialized && newValue <= 0 && IsAlive.Value)
        {
            IsAlive.Value = false;
            Deaths.Value++;
            Debug.Log($"Player {Nickname.Value} DIED! Killer: {_lastAttackerId}");

            OnAnyPlayerDied?.Invoke(this);

            if (TowerManager.Instance == null)
            {
                GameManager gm = FindObjectOfType<GameManager>();
                if (gm != null && _lastAttackerId != -1)
                {
                    gm.OnPlayerKilled(_lastAttackerId, base.Owner.ClientId);
                }
            }

            TowerManager.Instance?.CheckAllPlayersDead();

            if (TowerManager.Instance == null || TowerManager.Instance.CurrentState != TowerManager.TowerState.RunEnded)
            {
                StartCoroutine(RespawnRoutine());
            }
        }
    }

    // �������� ���� ��� �������� ���������� �������
    private int _lastAttackerId = -1;

    public IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(3f);

        if (!base.IsServerInitialized) yield break;

        if (TowerManager.Instance != null &&
            (TowerManager.Instance.CurrentState == TowerManager.TowerState.RunEnded ||
             TowerManager.Instance.CurrentState == TowerManager.TowerState.RunVictory))
        {
            yield break;
        }

        Vector3 respawnPosition = GetRespawnPosition();

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = respawnPosition;

        if (cc != null) cc.enabled = true;
        if (cc != null) cc.Move(Vector3.zero);

        HP.Value = 100;
        IsAlive.Value = true;

        UpdatePositionObserversRpc(respawnPosition);
    }

    private Vector3 GetRespawnPosition()
    {
        TowerManager tower = TowerManager.Instance;
        if (tower != null && tower.CurrentRoom != null)
        {
            Transform[] roomSpawns = tower.CurrentRoom.PlayerSpawnPoints;
            if (roomSpawns != null && roomSpawns.Length > 0)
            {
                int index = Random.Range(0, roomSpawns.Length);
                return roomSpawns[index].position;
            }
        }

        Transform[] spawnPoints = FindSpawnPoints();
        if (spawnPoints.Length > 0)
            return spawnPoints[Random.Range(0, spawnPoints.Length)].position;

        return new Vector3(0, 1, 0);
    }

    [ObserversRpc]
    private void UpdatePositionObserversRpc(Vector3 newPosition)
    {
        if (!base.IsServerInitialized)
        {
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = newPosition;
            if (cc != null) cc.enabled = true;
        }
    }

    private void OnIsAliveChanged(bool oldValue, bool newValue, bool asServer)
    {
        if (!newValue)
        {
            SetPlayerVisible(false);

            Collider collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (base.Owner.IsLocalClient)
                StartRespawnTimer();
        }
        else
        {
            SetPlayerVisible(true);

            Collider collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = true;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            if (base.Owner.IsLocalClient)
                StopRespawnTimer();
        }
    }

    private void StartRespawnTimer()
    {
        if (_respawnTimerCoroutine != null) StopCoroutine(_respawnTimerCoroutine);
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
        foreach (MeshRenderer renderer in renderers) renderer.enabled = visible;

        SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers) renderer.enabled = visible;

        if (_nicknameText != null) _nicknameText.enabled = visible;
        if (_hpText != null) _hpText.enabled = visible;
    }

    private Transform[] FindSpawnPoints()
    {
        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");
        Transform[] spawnPoints = new Transform[spawnPointObjects.Length];
        for (int i = 0; i < spawnPointObjects.Length; i++)
            spawnPoints[i] = spawnPointObjects[i].transform;
        return spawnPoints;
    }

    private void UpdateNicknameUI(string nickname)
    {
        if (_nicknameText != null)
            _nicknameText.text = nickname;
    }

    private void UpdateHPUI(int hp)
    {
        if (_hpText != null)
        {
            _hpText.text = $"HP: {hp}";
            _hpText.color = hp <= 30 ? Color.red : (hp <= 60 ? Color.yellow : Color.green);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage, int attackerId)
    {
        ApplyDamage(damage, attackerId);
    }

    public void ApplyDamage(int damage, int attackerId)
    {
        if (!base.IsServerInitialized) 
        {
            Debug.LogError("[PlayerNetwork] NOT SERVER!");
            return;
        }
        if (!IsAlive.Value) return;
        if (base.Owner.ClientId == attackerId) return;

        _lastAttackerId = attackerId;
        int newHp = Mathf.Max(0, HP.Value - damage);
        HP.Value = newHp;

        Debug.Log($"[PlayerNetwork] DMG! {Nickname.Value} HP: {newHp}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void ServerApplyDamage(int damage, int attackerId)
    {
        ApplyDamage(damage, attackerId);
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
        Nickname.OnChange -= OnNicknameChanged;
        HP.OnChange -= OnHpChanged;
        IsAlive.OnChange -= OnIsAliveChanged;
        Coins.OnChange -= OnCoinsChanged;

        if (_playerShooting != null && base.Owner.IsLocalClient)
        {
            _playerShooting.OnAmmoChanged -= UpdateAmmoScreenUI;
            _playerShooting.OnReloadingChanged -= OnReloadingChanged;
        }
    }
}