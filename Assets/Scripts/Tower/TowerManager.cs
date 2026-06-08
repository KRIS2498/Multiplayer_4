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

    [Header("Run Settings")]
    [SerializeField] private int _maxBosses = 5;
    [SerializeField] private float _transitionDelay = 2f;

    [Header("UI")]
    [SerializeField] private GameObject _floorTransitionUI;
    [SerializeField] private GameObject _runEndUI;

    private readonly SyncVar<TowerState> _currentState = new(TowerState.Lobby);
    private readonly SyncVar<int> _currentFloor = new(1);
    private readonly SyncVar<int> _bossesKilled = new(0);
    private readonly SyncVar<int> _bestFloorRecord = new(0);

    private BaseRoom _currentRoom;
    private GameObject _currentRoomGO;
    private Coroutine _stateCoroutine;

    private int _playerCount;

    public TowerState CurrentState => _currentState.Value;
    public int CurrentFloor => _currentFloor.Value;
    public int BossesKilled => _bossesKilled.Value;
    public int BestFloorRecord => _bestFloorRecord.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        }
    }

    private void OnBestFloorRecordChanged(int prev, int next, bool asServer) { }

    #region Player Connection

    private void OnPlayerConnectionChanged(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (!base.IsServerInitialized) return;

        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
        {
            _playerCount = base.ServerManager.Clients.Count;
            Debug.Log($"[Tower] Player connected. Count: {_playerCount}");

            if (_currentState.Value == TowerState.Lobby && _playerCount == 2)
            {
                StartRun();
            }
        }
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
        {
            _playerCount = base.ServerManager.Clients.Count;
            Debug.Log($"[Tower] Player disconnected. Count: {_playerCount}");

            if (_playerCount < 2 && (_currentState.Value == TowerState.FloorActive ||
                                     _currentState.Value == TowerState.GeneratingFloor))
            {
                EndRun(RunEndReason.Disconnected);
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
        NotifyFloorStartedObserversRpc(_currentFloor.Value, _currentRoom.RoomType.ToString());
        Debug.Log($"[Tower] Floor {_currentFloor.Value} active: {_currentRoom.RoomType}");
    }

    private GameObject GetRoomPrefabForFloor()
    {
        bool isBossFloor = _currentFloor.Value % 5 == 0;

        if (isBossFloor)
            return _roomDatabase.GetRandomBossRoom(_currentFloor.Value);
        else
            return _roomDatabase.GetRandomNonBossRoom(_currentFloor.Value);
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

    [ObserversRpc(ExcludeServer = true)]
    private void TeleportPlayerObserversRpc(int networkObjectId, Vector3 position, Quaternion rotation)
    {
        if (base.ServerManager.Objects.Spawned.TryGetValue(networkObjectId, out NetworkObject nob))
        {
            SetPlayerTransform(nob, position, rotation);
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

        StartCoroutine(AdvanceFloorCoroutine());
    }

    private IEnumerator AdvanceFloorCoroutine()
    {
        NotifyFloorCompleteObserversRpc(_currentFloor.Value);
        yield return new WaitForSeconds(_transitionDelay);

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

        int coinsEarned = CalculateCoins(reason);
        SaveRunResults(coinsEarned);

        NotifyRunEndedObserversRpc(reason.ToString(), _currentFloor.Value, _bossesKilled.Value, coinsEarned);

        if (_stateCoroutine != null) StopCoroutine(_stateCoroutine);
        StartCoroutine(ReturnToLobbyCoroutine());
    }

    private IEnumerator ReturnToLobbyCoroutine()
    {
        yield return new WaitForSeconds(5f);

        if (_currentRoomGO != null)
        {
            var nob = _currentRoomGO.GetComponent<NetworkObject>();
            if (nob != null) base.Despawn(nob);
            _currentRoomGO = null;
            _currentRoom = null;
        }

        ResetPlayers();
        _currentState.Value = TowerState.Lobby;
        _currentFloor.Value = 1;
        _bossesKilled.Value = 0;

        NotifyReturnToLobbyObserversRpc();

        if (_playerCount == 2)
            StartRun();
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

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (base.ServerManager != null)
            base.ServerManager.OnRemoteConnectionState -= OnPlayerConnectionChanged;

        _bestFloorRecord.OnChange -= OnBestFloorRecordChanged;
    }

    #endregion
}
