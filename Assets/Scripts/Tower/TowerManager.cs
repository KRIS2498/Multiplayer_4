using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TowerManager : NetworkBehaviour
{
    public enum TowerState
    {
        Lobby,
        GeneratingFloor,
        FloorActive,
        FloorComplete,
        RunEnded,
        RunVictory
    }

    public static TowerManager Instance { get; private set; }

    [Header("Core Settings")]
    [SerializeField] private RoomDatabase _roomDatabase;
    [SerializeField] private DifficultyManager _difficultyManager;
    [SerializeField] private Transform _roomAnchor;

    [Header("Lobby")]
    [SerializeField] private Transform[] _lobbySpawnPoints;
    [SerializeField] private Transform _startingRoomPosition;
    [SerializeField] private float _lobbyCountdownDuration = 10f;

    [Header("Run Settings")]
    [SerializeField] private int _maxBosses = 5;
    [SerializeField] private float _transitionDelay = 2f;

    [Header("UI")]
    [SerializeField] private GameObject _floorTransitionUI;
    [SerializeField] private GameObject _runEndUI;
    [SerializeField] private GameObject _lobbyUI;
    [SerializeField] private TMPro.TextMeshProUGUI _lobbyStatusText;
    [SerializeField] private TMPro.TextMeshProUGUI _lobbyCountdownText;

    private readonly SyncVar<TowerState> _currentState = new(TowerState.Lobby);
    private readonly SyncVar<int> _currentFloor = new(1);
    private readonly SyncVar<int> _bossesKilled = new(0);
    private readonly SyncVar<int> _bestFloorRecord = new(0);
    private readonly SyncVar<int> _lobbyPlayerCount = new(0);
    private readonly SyncVar<float> _lobbyCountdownTimer = new(0f);

    private BaseRoom _currentRoom;
    private GameObject _currentRoomGO;
    private Coroutine _stateCoroutine;
    private Coroutine _lobbyCountdownCoroutine;

    [Header("Tether")]
    [SerializeField] private float _tetherMaxDistance = 10f;
    [SerializeField] private float _tetherDamagePerSecond = 2f;
    [SerializeField] private float _tetherCheckInterval = 0.5f;
    [SerializeField] private float _tetherGracePeriod = 2f;

    private float _tetherTimer;
    private float _tetherFloorActiveTime;

    private int _playerCount;
    private bool _configError;
    private bool _runStartedFromDoor;

    public TowerState CurrentState => _currentState.Value;
    public int CurrentFloor => _currentFloor.Value;
    public int BossesKilled => _bossesKilled.Value;
    public int BestFloorRecord => _bestFloorRecord.Value;
    public BaseRoom CurrentRoom => _currentRoom;
    public float TetherMaxDistance => _tetherMaxDistance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;
        if (_currentState.Value != TowerState.FloorActive) return;

        var players = GetAllPlayerNetworks();
        if (players.Count < 2) return;

        if (Time.time - _tetherFloorActiveTime < _tetherGracePeriod) return;

        float distance = Vector3.Distance(players[0].transform.position, players[1].transform.position);
        if (distance <= _tetherMaxDistance)
        {
            _tetherTimer = _tetherCheckInterval;
            return;
        }

        _tetherTimer -= Time.deltaTime;
        if (_tetherTimer <= 0)
        {
            _tetherTimer = _tetherCheckInterval;
            int damage = Mathf.Max(1, Mathf.RoundToInt(_tetherDamagePerSecond * _tetherCheckInterval));
            ServerApplyDamage(players[0].Owner.ClientId, damage, -1);
            ServerApplyDamage(players[1].Owner.ClientId, damage, -1);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        base.ServerManager.OnRemoteConnectionState += OnPlayerConnectionChanged;
        _bestFloorRecord.OnChange += OnBestFloorRecordChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsClient)
        {
            _bestFloorRecord.OnChange += OnBestFloorRecordChanged;
            _lobbyPlayerCount.OnChange += OnLobbyPlayerCountChanged;
            _lobbyCountdownTimer.OnChange += OnLobbyCountdownTimerChanged;
            UpdateLobbyUIOnJoin();
        }
    }

    private void OnBestFloorRecordChanged(int prev, int next, bool asServer) { }

    private void OnLobbyPlayerCountChanged(int prev, int next, bool asServer)
    {
        if (asServer) return;
        UpdateLobbyUISync();
    }

    private void OnLobbyCountdownTimerChanged(float prev, float next, bool asServer)
    {
        if (asServer) return;
        if (_lobbyCountdownText != null)
        {
            int secs = Mathf.CeilToInt(next);
            if (secs > 0)
            {
                _lobbyCountdownText.text = $"{secs}s";
                _lobbyCountdownText.gameObject.SetActive(true);
            }
            else
            {
                _lobbyCountdownText.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateLobbyUIOnJoin()
    {
        if (_currentState.Value == TowerState.Lobby)
        {
            if (_lobbyUI != null)
                _lobbyUI.SetActive(true);
            UpdateLobbyUISync();
        }
        else
        {
            if (_lobbyUI != null)
                _lobbyUI.SetActive(false);
        }
    }

    private void UpdateLobbyUISync()
    {
        if (_lobbyStatusText != null)
        {
            if (_lobbyPlayerCount.Value < 2)
                _lobbyStatusText.text = "Waiting for players...";
            else
                _lobbyStatusText.text = "Players ready!";
        }
    }

    #region Player Connection

    private void OnPlayerConnectionChanged(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (!base.IsServerInitialized) return;

        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
        {
            _playerCount = base.ServerManager.Clients.Count;
            _lobbyPlayerCount.Value = _playerCount;
            Debug.Log($"[Tower] Player connected. Count: {_playerCount}");

            if (_currentState.Value == TowerState.Lobby && !_configError && _lobbySpawnPoints != null && _lobbySpawnPoints.Length > 0)
            {
                StartCoroutine(DelayedTeleportToLobby(conn));
            }

            if (_currentState.Value == TowerState.Lobby && _playerCount == 2 && !_configError)
            {
                StartLobbyCountdown();
            }
        }
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
        {
            _playerCount = base.ServerManager.Clients.Count;
            _lobbyPlayerCount.Value = _playerCount;
            Debug.Log($"[Tower] Player disconnected. Count: {_playerCount}");

            if (_currentState.Value == TowerState.Lobby && _lobbyCountdownCoroutine != null)
            {
                StopCoroutine(_lobbyCountdownCoroutine);
                _lobbyCountdownCoroutine = null;
                _lobbyCountdownTimer.Value = 0f;
                NotifyLobbyCountdownObserversRpc(0);
                NotifyLobbyStatusObserversRpc("Waiting for players...");
            }

            if (_playerCount < 2 && (_currentState.Value == TowerState.FloorActive ||
                                     _currentState.Value == TowerState.GeneratingFloor))
            {
                EndRun(RunEndReason.Disconnected);
            }
        }
    }

    #endregion

    #region Lobby

    private IEnumerator DelayedTeleportToLobby(NetworkConnection conn)
    {
        yield return null;

        TeleportSinglePlayerToLobby(conn);
    }

    private void StartLobbyCountdown()
    {
        if (_currentState.Value != TowerState.Lobby) return;
        if (_lobbyCountdownCoroutine != null) return;

        _lobbyCountdownCoroutine = StartCoroutine(LobbyCountdownCoroutine());
    }

    private IEnumerator LobbyCountdownCoroutine()
    {
        float timer = _lobbyCountdownDuration;
        Debug.Log($"[Tower] Lobby countdown: {timer}s");

        while (timer > 0)
        {
            if (_lobbyPlayerCount.Value < 2)
            {
                Debug.Log("[Tower] Countdown cancelled: not enough players");
                _lobbyCountdownTimer.Value = 0f;
                NotifyLobbyCountdownObserversRpc(0);
                NotifyLobbyStatusObserversRpc("Waiting for players...");
                yield break;
            }

            _lobbyCountdownTimer.Value = timer;
            NotifyLobbyCountdownObserversRpc(Mathf.CeilToInt(timer));
            NotifyLobbyStatusObserversRpc($"Starting in {Mathf.CeilToInt(timer)}s");
            yield return new WaitForSeconds(1f);
            timer--;
        }

        _lobbyCountdownCoroutine = null;
        _lobbyCountdownTimer.Value = 0f;

        NotifyLobbyCountdownObserversRpc(0);
        NotifyLobbyStatusObserversRpc("Enter the starting room!");
        TeleportToStartingRoom();
    }

    private void TeleportToStartingRoom()
    {
        if (_startingRoomPosition == null)
        {
            Debug.LogError("[Tower] Starting room position not set!");
            TeleportToLobby();
            return;
        }

        Vector3 pos = _startingRoomPosition.position;
        Quaternion rot = _startingRoomPosition.rotation;

        var players = GetAllPlayerNetworks();
        for (int i = 0; i < players.Count; i++)
        {
            var nob = players[i].GetComponent<NetworkObject>();
            if (nob != null)
            {
                SetPlayerTransform(nob, pos + Vector3.right * (i == 0 ? -1f : 1f), rot);
                TeleportPlayerObserversRpc(nob.ObjectId, pos + Vector3.right * (i == 0 ? -1f : 1f), rot);
            }
        }

        NotifyTeleportToStartObserversRpc();
    }

    public void TryStartRunFromDoor()
    {
        if (!base.IsServerInitialized) return;
        if (_currentState.Value != TowerState.Lobby) return;
        if (_runStartedFromDoor) return;

        Debug.Log("[Tower] Both players at door! Starting run.");
        _runStartedFromDoor = true;
        StartRun();
    }

    private void TeleportSinglePlayerToLobby(NetworkConnection conn)
    {
        if (_lobbySpawnPoints == null || _lobbySpawnPoints.Length == 0) return;

        int playerIndex = 0;
        foreach (var nob in conn.Objects)
        {
            if (nob.GetComponent<PlayerNetwork>() != null)
            {
                Transform sp = _lobbySpawnPoints[Mathf.Min(playerIndex, _lobbySpawnPoints.Length - 1)];
                SetPlayerTransform(nob, sp.position, sp.rotation);
                TeleportPlayerObserversRpc(nob.ObjectId, sp.position, sp.rotation);
                playerIndex++;
            }
        }
    }

    public void TeleportToLobby()
    {
        if (_lobbySpawnPoints == null || _lobbySpawnPoints.Length == 0)
        {
            Debug.LogWarning("[Tower] No lobby spawn points set!");
            return;
        }

        var players = GetAllPlayerNetworks();
        for (int i = 0; i < players.Count; i++)
        {
            Transform sp = _lobbySpawnPoints[Mathf.Min(i, _lobbySpawnPoints.Length - 1)];
            var nob = players[i].GetComponent<NetworkObject>();
            if (nob != null)
            {
                SetPlayerTransform(nob, sp.position, sp.rotation);
                TeleportPlayerObserversRpc(nob.ObjectId, sp.position, sp.rotation);
            }
        }
    }

    #endregion

    #region Run Flow

    private void StartRun()
    {
        if (_currentState.Value != TowerState.Lobby) return;

        Debug.Log("[Tower] Starting new run!");
        _currentFloor.Value = 1;
        _bossesKilled.Value = 0;

        LoadBestRecord();

        NotifyRunStartedObserversRpc();
        GenerateFloor();
    }

    private void GenerateFloor()
    {
        if (!base.IsServerInitialized) return;

        _currentState.Value = TowerState.GeneratingFloor;
        Debug.Log($"[Tower] Generating floor {_currentFloor.Value}...");

        _difficultyManager.SetFloor(_currentFloor.Value);

        if (_stateCoroutine != null) StopCoroutine(_stateCoroutine);
        _stateCoroutine = StartCoroutine(GenerateFloorCoroutine());
    }

    private IEnumerator GenerateFloorCoroutine()
    {
        GameObject roomPrefab = GetRoomPrefabForFloor();

        if (roomPrefab == null)
        {
            Debug.LogError($"[Tower] No room prefab for floor {_currentFloor.Value}!");
            EndRun(RunEndReason.Error);
            yield break;
        }

        NotifyFloorGeneratingObserversRpc(_currentFloor.Value);

        yield return new WaitForSeconds(_transitionDelay);

        if (_currentRoomGO != null)
        {
            var room = _currentRoomGO.GetComponent<BaseRoom>();
            if (room != null) room.CleanupRoom();

            var nob = _currentRoomGO.GetComponent<NetworkObject>();
            if (nob != null)
                base.Despawn(nob);
            else
                Destroy(_currentRoomGO);
            _currentRoomGO = null;
            _currentRoom = null;
        }

        yield return null;

        Vector3 spawnPos = _roomAnchor != null ? _roomAnchor.position : Vector3.zero;
        _currentRoomGO = Instantiate(roomPrefab, spawnPos, Quaternion.identity);
        var roomNob = _currentRoomGO.GetComponent<NetworkObject>();
        if (roomNob != null)
            base.Spawn(roomNob);
        else
            Debug.LogError("[Tower] Room prefab missing NetworkObject!");

        _currentRoom = _currentRoomGO.GetComponent<BaseRoom>();
        if (_currentRoom != null)
        {
            _currentRoom.RoomIndex = _currentFloor.Value;
            _currentRoom.OnRoomCompleted += OnRoomCompleted;
            _currentRoom.InitializeRoom();

            yield return null;
            TeleportPlayersToRoom();

            _currentRoom.OnPlayersEntered(GetAllPlayerNetworks());
        }

        _currentState.Value = TowerState.FloorActive;
        _tetherFloorActiveTime = Time.time;
        _tetherTimer = _tetherCheckInterval;
        NotifyFloorStartedObserversRpc(_currentFloor.Value, _currentRoom.RoomType.ToString());
        Debug.Log($"[Tower] Floor {_currentFloor.Value} active: {_currentRoom.RoomType}");
    }

    private GameObject GetRoomPrefabForFloor()
    {
        if (_roomDatabase == null)
        {
            Debug.LogError("[Tower] RoomDatabase is not assigned!");
            return null;
        }

        bool isBossFloor = _currentFloor.Value % 5 == 0;

        GameObject prefab;
        if (isBossFloor)
            prefab = _roomDatabase.GetRandomBossRoom(_currentFloor.Value);
        else
            prefab = _roomDatabase.GetRandomNonBossRoom(_currentFloor.Value);

        if (prefab == null)
            Debug.LogError($"[Tower] No room prefab found for floor {_currentFloor.Value} " +
                           $"(boss: {isBossFloor}). Check RoomDatabase.");

        return prefab;
    }

    private void TeleportPlayersToRoom()
    {
        if (_currentRoom == null) return;

        Transform[] spawns = _currentRoom.PlayerSpawnPoints;
        if (spawns == null || spawns.Length == 0)
        {
            Debug.LogWarning("[Tower] No player spawn points in room!");
            return;
        }

        var players = GetAllPlayerNetworks();
        for (int i = 0; i < players.Count; i++)
        {
            Transform spawnPoint = spawns[Mathf.Min(i, spawns.Length - 1)];
            var nob = players[i].GetComponent<NetworkObject>();
            if (nob != null)
            {
                SetPlayerTransform(nob, spawnPoint.position, spawnPoint.rotation);
                TeleportPlayerObserversRpc(nob.ObjectId, spawnPoint.position, spawnPoint.rotation);
            }
        }
    }

    private void SetPlayerTransform(NetworkObject nob, Vector3 position, Quaternion rotation)
    {
        var cc = nob.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        nob.transform.position = position;
        nob.transform.rotation = rotation;
        if (cc != null) cc.enabled = true;
    }

    [ObserversRpc]
    private void TeleportPlayerObserversRpc(int networkObjectId, Vector3 position, Quaternion rotation)
    {
        foreach (var player in FindObjectsOfType<PlayerNetwork>())
        {
            var nob = player.GetComponent<NetworkObject>();
            if (nob != null && nob.ObjectId == networkObjectId)
            {
                var cc = nob.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                nob.transform.position = position;
                nob.transform.rotation = rotation;
                if (cc != null) cc.enabled = true;
                break;
            }
        }
    }

    private void OnRoomCompleted(BaseRoom room)
    {
        if (!base.IsServerInitialized) return;
        if (room != _currentRoom) return;
        if (_currentState.Value != TowerState.FloorActive) return;

        _currentState.Value = TowerState.FloorComplete;
        Debug.Log($"[Tower] Floor {_currentFloor.Value} completed!");

        AutoSaveProgress();

        bool isBossFloor = _currentFloor.Value % 5 == 0;
        if (isBossFloor)
        {
            _bossesKilled.Value++;

            if (_bossesKilled.Value >= _maxBosses)
            {
                EndRun(RunEndReason.Victory);
                return;
            }
        }

        NotifyFloorCompleteObserversRpc(_currentFloor.Value);
    }

    public void OnPlayerEnteredExit()
    {
        if (!base.IsServerInitialized) return;
        if (_currentState.Value != TowerState.FloorComplete) return;

        _currentFloor.Value++;
        GenerateFloor();
    }

    #endregion

    #region Run End

    public enum RunEndReason
    {
        AllPlayersDead,
        Victory,
        Disconnected,
        Error
    }

    private void EndRun(RunEndReason reason)
    {
        if (!base.IsServerInitialized) return;
        if (_currentState.Value == TowerState.RunEnded ||
            _currentState.Value == TowerState.RunVictory) return;

        _currentState.Value = reason == RunEndReason.Victory
            ? TowerState.RunVictory
            : TowerState.RunEnded;

        Debug.Log($"[Tower] Run ended: {reason}");

        if (reason == RunEndReason.Error)
            _configError = true;

        int coinsEarned = CalculateCoins(reason);
        SaveRunResults(coinsEarned);

        NotifyRunEndedObserversRpc(reason.ToString(), _currentFloor.Value, _bossesKilled.Value, coinsEarned);

        if (_stateCoroutine != null) StopCoroutine(_stateCoroutine);

        float delay = reason == RunEndReason.AllPlayersDead ? 1f : 5f;
        StartCoroutine(ReturnToLobbyCoroutine(delay));
    }

    private IEnumerator ReturnToLobbyCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_currentRoomGO != null)
        {
            var room = _currentRoomGO.GetComponent<BaseRoom>();
            if (room != null) room.CleanupRoom();

            var nob = _currentRoomGO.GetComponent<NetworkObject>();
            if (nob != null) base.Despawn(nob);
            _currentRoomGO = null;
            _currentRoom = null;
        }

        ResetPlayers();
        _currentState.Value = TowerState.Lobby;
        _currentFloor.Value = 1;
        _bossesKilled.Value = 0;
        _runStartedFromDoor = false;

        NotifyReturnToLobbyObserversRpc();

        TeleportToStartingRoom();
        _lobbyPlayerCount.Value = _playerCount;
        NotifyLobbyStatusObserversRpc("Enter the door to start a new run!");
    }

    public void CheckAllPlayersDead()
    {
        if (!base.IsServerInitialized) return;
        if (_currentState.Value != TowerState.FloorActive) return;

        var players = GetAllPlayerNetworks();
        bool allDead = players.Count > 0 && players.All(p => !p.IsAlive.Value);

        if (allDead)
        {
            Debug.Log("[Tower] All players dead! Ending run.");
            EndRun(RunEndReason.AllPlayersDead);
        }
    }

    #endregion

    #region Coins & Save

    private int CalculateCoins(RunEndReason reason)
    {
        if (reason == RunEndReason.Error || reason == RunEndReason.Disconnected)
            return 0;

        int floorsCleared = _currentFloor.Value - 1;
        return floorsCleared * 5;
    }

    private void AutoSaveProgress()
    {
        int currentRecord = LoadBestRecordFromDisk();
        if (_currentFloor.Value > currentRecord)
        {
            SaveBestRecordToDisk(_currentFloor.Value);
            _bestFloorRecord.Value = _currentFloor.Value;
        }
    }

    private void SaveRunResults(int coins)
    {
        int totalCoins = LoadCoinsFromDisk();
        totalCoins += coins;
        SaveCoinsToDisk(totalCoins);

        int currentRecord = LoadBestRecordFromDisk();
        int floorsCleared = _currentFloor.Value - 1;
        if (floorsCleared > currentRecord)
        {
            SaveBestRecordToDisk(floorsCleared);
            _bestFloorRecord.Value = floorsCleared;
        }
    }

    private void LoadBestRecord()
    {
        int record = LoadBestRecordFromDisk();
        _bestFloorRecord.Value = record;
    }

    private void ResetPlayers()
    {
        if (!base.IsServerInitialized) return;

        foreach (var conn in base.ServerManager.Clients.Values)
        {
            foreach (var nob in conn.Objects)
            {
                var pn = nob.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    pn.HP.Value = 100;
                    pn.IsAlive.Value = true;
                    pn.StopAllCoroutines();
                }

                var shooting = nob.GetComponent<PlayerShooting>();
                if (shooting != null)
                {
                    shooting.ResetAmmo();
                }
            }
        }
    }

    #endregion

    #region Disk I/O

    private string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "tower_save.json");

    [System.Serializable]
    private class SaveData
    {
        public int Coins;
        public int BestFloor;
    }

    private int LoadCoinsFromDisk()
    {
        var data = LoadSaveData();
        return data?.Coins ?? 0;
    }

    private void SaveCoinsToDisk(int coins)
    {
        var data = LoadSaveData() ?? new SaveData();
        data.Coins = coins;
        System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(data));
    }

    private int LoadBestRecordFromDisk()
    {
        var data = LoadSaveData();
        return data?.BestFloor ?? 0;
    }

    private void SaveBestRecordToDisk(int floor)
    {
        var data = LoadSaveData() ?? new SaveData();
        data.BestFloor = floor;
        System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(data));
    }

    private SaveData LoadSaveData()
    {
        if (!System.IO.File.Exists(SavePath)) return null;
        try
        {
            return JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(SavePath));
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Damage

    public void ServerApplyDamage(int targetClientId, int damage, int attackerId)
    {
        if (!base.IsServerInitialized) return;
        if (damage <= 0) return;

        foreach (var conn in base.ServerManager.Clients)
        {
            if (conn.Key == targetClientId)
            {
                foreach (var nob in conn.Value.Objects)
                {
                    PlayerNetwork pn = nob.GetComponent<PlayerNetwork>();
                    if (pn != null)
                    {
                        pn.ApplyDamage(damage, attackerId);
                    }
                }
                break;
            }
        }
    }

    #endregion

    #region Helpers

    private List<PlayerNetwork> GetAllPlayerNetworks()
    {
        var list = new List<PlayerNetwork>();
        foreach (var conn in base.ServerManager.Clients.Values)
        {
            foreach (var nob in conn.Objects)
            {
                var pn = nob.GetComponent<PlayerNetwork>();
                if (pn != null)
                    list.Add(pn);
            }
        }
        return list;
    }

    #endregion

    #region RPCs

    [ObserversRpc]
    private void NotifyRunStartedObserversRpc()
    {
        Debug.Log("[Tower] Run started!");
        if (_lobbyUI != null)
            _lobbyUI.SetActive(false);
    }

    [ObserversRpc]
    private void NotifyFloorGeneratingObserversRpc(int floor)
    {
        Debug.Log($"[Tower] Generating floor {floor}...");
    }

    [ObserversRpc]
    private void NotifyFloorStartedObserversRpc(int floor, string roomType)
    {
        Debug.Log($"[Tower] Floor {floor} ({roomType}) started!");
        if (_floorTransitionUI != null)
            _floorTransitionUI.SetActive(false);
    }

    [ObserversRpc]
    private void NotifyFloorCompleteObserversRpc(int floor)
    {
        Debug.Log($"[Tower] Floor {floor} complete!");
    }

    [ObserversRpc]
    private void NotifyRunEndedObserversRpc(string reason, int floor, int bosses, int coins)
    {
        Debug.Log($"[Tower] Run ended: {reason}, Floor: {floor}, Bosses: {bosses}, Coins: {coins}");
    }

    [ObserversRpc]
    private void NotifyReturnToLobbyObserversRpc()
    {
        Debug.Log("[Tower] Returned to lobby");
    }

    [ObserversRpc]
    private void NotifyLobbyStatusObserversRpc(string status)
    {
        if (_lobbyStatusText != null)
            _lobbyStatusText.text = status;
    }

    [ObserversRpc]
    private void NotifyLobbyCountdownObserversRpc(int seconds)
    {
        if (_lobbyCountdownText != null)
        {
            if (seconds > 0)
            {
                _lobbyCountdownText.text = $"{seconds}s";
                _lobbyCountdownText.gameObject.SetActive(true);
            }
            else
            {
                _lobbyCountdownText.gameObject.SetActive(false);
            }
        }
    }

    [ObserversRpc]
    private void NotifyTeleportToStartObserversRpc()
    {
        if (_lobbyUI != null)
            _lobbyUI.SetActive(false);
    }

    private void UpdateLobbyUI()
    {
        if (_lobbyUI != null)
            _lobbyUI.SetActive(true);
        if (_lobbyStatusText != null)
            _lobbyStatusText.text = _lobbyPlayerCount.Value < 2 ? "Waiting for players..." : "Players ready!";
        if (_lobbyCountdownText != null)
            _lobbyCountdownText.gameObject.SetActive(false);
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (base.ServerManager != null)
            base.ServerManager.OnRemoteConnectionState -= OnPlayerConnectionChanged;

        _bestFloorRecord.OnChange -= OnBestFloorRecordChanged;
        _lobbyPlayerCount.OnChange -= OnLobbyPlayerCountChanged;
        _lobbyCountdownTimer.OnChange -= OnLobbyCountdownTimerChanged;
    }

    #endregion
}
