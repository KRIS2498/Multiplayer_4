using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private int _requiredPlayers = 2;
    [SerializeField] private float _countdownTime = 3f;
    [SerializeField] private float _matchDuration = 60f;
    [SerializeField] private int _winningScore = 10;
    [SerializeField] private float _resultsDisplayTime = 5f;

    [Header("UI Elements (under Canvas)")]
    [SerializeField] private GameObject _waitingPanel;
    [SerializeField] private Text _waitingText;
    [SerializeField] private Text _timerText;
    [SerializeField] private Text _gameTimerText;

    // SyncVars
    private readonly SyncVar<GameState> _currentState = new SyncVar<GameState>();
    private readonly SyncVar<int> _connectedPlayers = new SyncVar<int>();
    private readonly SyncVar<float> _syncTimer = new SyncVar<float>();

    // Server-side fields
    private float _currentCountdown;
    private bool _isCountingDown;
    private float _matchTimer;
    private bool _isMatchRunning;

    // ������ ���� � ���� �������
    private Dictionary<int, int> _playerScores = new Dictionary<int, int>();
    private Dictionary<int, string> _playerNames = new Dictionary<int, string>();

    public enum GameState
    {
        WaitingForPlayers,
        Countdown,
        InProgress,
        ShowingResults
    }

    public GameState CurrentState
    {
        get => _currentState.Value;
        private set => _currentState.Value = value;
    }

    public int ConnectedPlayers => _connectedPlayers.Value;

    #region Network Callbacks

    private bool IsTowerMode => TowerManager.Instance != null;

    private void Start()
    {
        if (IsTowerMode)
            HideAllPanels();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (IsTowerMode) return;

        base.ServerManager.OnRemoteConnectionState += OnPlayerConnectionChanged;

        _connectedPlayers.Value = base.ServerManager.Clients.Count;
        _syncTimer.Value = _matchDuration;

        _connectedPlayers.OnChange += OnConnectedPlayersChanged;
        _currentState.OnChange += OnCurrentStateChanged;
        _syncTimer.OnChange += OnSyncTimerChanged;

        Debug.Log($"[Server] GameManager initialized");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsTowerMode)
        {
            HideAllPanels();
            return;
        }

        if (IsClient)
        {
            _connectedPlayers.OnChange += OnConnectedPlayersChanged;
            _currentState.OnChange += OnCurrentStateChanged;
            _syncTimer.OnChange += OnSyncTimerChanged;
        }

        UpdateUIByState();
    }

    private void OnPlayerConnectionChanged(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (!base.IsServerInitialized) return;

        _connectedPlayers.Value = base.ServerManager.Clients.Count;

        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
        {
            string playerName = GetPlayerNickname(conn);
            _playerScores[conn.ClientId] = 0;  // ? ����� conn.ClientId ��������
            _playerNames[conn.ClientId] = playerName;
            Debug.Log($"[Server] Player {playerName} connected");
        }
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
        {
            if (_playerScores.ContainsKey(conn.ClientId))  // ? ����� conn.ClientId ��������
            {
                _playerScores.Remove(conn.ClientId);
                _playerNames.Remove(conn.ClientId);
            }
        }

        if (CurrentState == GameState.WaitingForPlayers && _connectedPlayers.Value >= _requiredPlayers)
        {
            StartCountdown();
        }
    }

    private string GetPlayerNickname(NetworkConnection conn)
    {
        foreach (var nob in conn.Objects)
        {
            PlayerNetwork pn = nob.GetComponent<PlayerNetwork>();
            if (pn != null)
            {
                return pn.Nickname.Value;
            }
        }
        return $"Player_{conn.ClientId}";
    }

    #endregion

    #region Match Flow

    private void StartCountdown()
    {
        if (CurrentState != GameState.WaitingForPlayers) return;

        CurrentState = GameState.Countdown;
        _currentCountdown = _countdownTime;
        _isCountingDown = true;

        Debug.Log($"[Server] Countdown started");

        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        while (_isCountingDown && _currentCountdown > 0)
        {
            NotifyCountdownUpdate(_currentCountdown);
            yield return new WaitForSeconds(1f);
            _currentCountdown--;
        }

        if (_isCountingDown)
        {
            StartMatch();
        }
    }

    private void UpdateCountdownUI(float time)
    {
        NotifyCountdownUpdate(time);
    }

    private void StartMatch()
    {
        if (CurrentState != GameState.Countdown) return;

        CurrentState = GameState.InProgress;
        _matchTimer = _matchDuration;
        _syncTimer.Value = _matchDuration;
        _isCountingDown = false;
        _isMatchRunning = true;

        // ���������� ����
        foreach (var clientId in _playerScores.Keys.ToList())
        {
            _playerScores[clientId] = 0;
        }

        Debug.Log("[Server] Match started!");
        NotifyMatchStarted();
    }

    private void Update()
    {
        if (!base.IsServerInitialized) return;

        if (CurrentState == GameState.InProgress && _isMatchRunning)
        {
            _matchTimer -= Time.deltaTime;
            _syncTimer.Value = _matchTimer;

            if (_matchTimer <= 0f)
            {
                _isMatchRunning = false;
                EndMatch("Time's up!");
                return;
            }

            CheckWinCondition();
        }
    }

    private void CheckWinCondition()
    {
        foreach (var score in _playerScores)
        {
            if (score.Value >= _winningScore)
            {
                string winnerName = _playerNames.ContainsKey(score.Key) ? _playerNames[score.Key] : $"Player_{score.Key}";
                _isMatchRunning = false;
                EndMatch($"{winnerName} won!");
                return;
            }
        }
    }

    private void EndMatch(string reason)
    {
        if (CurrentState != GameState.InProgress) return;

        CurrentState = GameState.ShowingResults;
        Debug.Log($"[Server] Match ended: {reason}");

        string results = CollectResults();
        ShowResultsToClients(results);

        StartCoroutine(ReturnToLobbyCoroutine());
    }

    // � ������ CollectResults() - ������ ~240-250
    private string CollectResults()
    {
        Debug.Log("[GameManager] Collecting results...");

        // ������� ������ ����������
        var finalScores = new Dictionary<int, int>();
        var finalNames = new Dictionary<int, string>();

        // �������� ������ �������� �������
        foreach (var conn in base.ServerManager.Clients)
        {
            int clientId = conn.Key;
            foreach (var nob in conn.Value.Objects)
            {
                PlayerNetwork pn = nob.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    finalScores[clientId] = pn.Score.Value;
                    finalNames[clientId] = pn.Nickname.Value;
                    Debug.Log($"[GameManager] Player {clientId}: {pn.Nickname.Value} has {pn.Score.Value} kills");
                }
            }
        }

        // ��������� �������� �������
        _playerScores = finalScores;
        _playerNames = finalNames;

        var sortedScores = _playerScores.OrderByDescending(x => x.Value).ToList();

        string results = "=== MATCH RESULTS ===\n\n";
        for (int i = 0; i < sortedScores.Count; i++)
        {
            string medal = i == 0 ? "?? " : (i == 1 ? "?? " : "?? ");
            string playerName = _playerNames.ContainsKey(sortedScores[i].Key)
                ? _playerNames[sortedScores[i].Key]
                : $"Player_{sortedScores[i].Key}";

            results += $"{medal}{playerName}: {sortedScores[i].Value} kills\n";
        }

        return results;
    }

    private IEnumerator ReturnToLobbyCoroutine()
    {
        yield return new WaitForSeconds(_resultsDisplayTime);
        ResetToLobby();
    }

    private void ResetToLobby()
    {
        Debug.Log("[Server] Resetting to lobby...");

        // ���������� ����
        foreach (var clientId in _playerScores.Keys.ToList())
        {
            _playerScores[clientId] = 0;
        }

        // ���������� ���� �������
        foreach (var conn in base.ServerManager.Clients.Values)
        {
            foreach (var nob in conn.Objects)
            {
                PlayerNetwork pn = nob.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    pn.HP.Value = 100;
                    pn.Score.Value = 0;
                    pn.IsAlive.Value = true;
                    pn.StopAllCoroutines();

                    // ? ���������� �������
                    PlayerShooting shooting = nob.GetComponent<PlayerShooting>();
                    if (shooting != null)
                    {
                        shooting.ResetAmmo();
                    }

                    Transform[] spawnPoints = FindSpawnPoints();
                    if (spawnPoints.Length > 0)
                    {
                        CharacterController cc = pn.GetComponent<CharacterController>();
                        if (cc != null) cc.enabled = false;
                        pn.transform.position = spawnPoints[Random.Range(0, spawnPoints.Length)].position;
                        if (cc != null) cc.enabled = true;
                    }
                }
            }
        }

        _matchTimer = _matchDuration;
        _syncTimer.Value = _matchDuration;
        CurrentState = GameState.WaitingForPlayers;
        _connectedPlayers.Value = base.ServerManager.Clients.Count;

        if (_connectedPlayers.Value >= _requiredPlayers)
        {
            StartCountdown();
        }

        NotifyLobbyReset();
}

    public void ApplyDamageToPlayer(int targetClientId, int damage, int attackerId)
    {
        if (!base.IsServerInitialized) return;

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
                        Debug.Log($"[GameManager] Applied {damage} damage to player {targetClientId}");
                    }
                }
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ServerApplyDamage(int targetClientId, int damage, int attackerId)
    {
        ApplyDamageToPlayer(targetClientId, damage, attackerId);
    }

    public void OnPlayerKilled(int killerClientId, int victimClientId)
    {
        Debug.Log($"[GameManager] OnPlayerKilled called! Killer: {killerClientId}, Victim: {victimClientId}, Current State: {CurrentState}");

        if (!base.IsServerInitialized) return;
        if (CurrentState != GameState.InProgress) return;
        if (killerClientId == victimClientId) return;
        if (killerClientId == -1 || victimClientId == -1)
        {
            Debug.Log("[GameManager] Invalid killer or victim ID (-1), ignoring");
            return;
        }

        if (!_playerScores.ContainsKey(killerClientId))
        {
            _playerScores[killerClientId] = 0;
        }

        _playerScores[killerClientId]++;
        Debug.Log($"[GameManager] Player {killerClientId} now has {_playerScores[killerClientId]} kills");

        PlayerNetwork[] allPlayers = FindObjectsOfType<PlayerNetwork>();
        foreach (PlayerNetwork pn in allPlayers)
        {
            if (pn.Owner.ClientId == killerClientId)
            {
                pn.Score.Value = _playerScores[killerClientId];
                Debug.Log($"[GameManager] Updated PlayerNetwork.Score for {killerClientId}");
            }
        }
    }

    private Transform[] FindSpawnPoints()
    {
        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");
        Transform[] spawnPoints = new Transform[spawnPointObjects.Length];
        for (int i = 0; i < spawnPointObjects.Length; i++)
            spawnPoints[i] = spawnPointObjects[i].transform;
        return spawnPoints;
    }

    #endregion

    #region RPCs

    [ObserversRpc]
    private void NotifyCountdownUpdate(float remainingTime)
    {
        if (IsClient && CurrentState == GameState.Countdown && _timerText != null)
        {
            _timerText.text = $"Starting in: {Mathf.CeilToInt(remainingTime)}";
        }
    }

    [ObserversRpc]
    private void NotifyMatchStarted()
    {
        if (IsClient)
        {
            Debug.Log("[Client] Match started!");
            UpdateUIByState();
        }
    }

    [ObserversRpc]
    private void ShowResultsToClients(string results)
    {
        // Results are now handled by TowerManager
    }

    [ObserversRpc]
    private void NotifyLobbyReset()
    {
        if (IsClient)
        {
            Debug.Log("[Client] Returning to lobby...");
            if (_timerText != null) _timerText.text = "";
            UpdateUIByState();
        }
    }

    #endregion

    #region UI Updates

    private void OnConnectedPlayersChanged(int prev, int next, bool asServer)
    {
        if (IsClient)
        {
            UpdateUIByState();
        }
    }

    private void OnCurrentStateChanged(GameState prev, GameState next, bool asServer)
    {
        if (IsClient)
        {
            UpdateUIByState();
        }
    }

    private void OnSyncTimerChanged(float prev, float next, bool asServer)
    {
        if (IsClient && CurrentState == GameState.InProgress && _gameTimerText != null)
        {
            int minutes = Mathf.FloorToInt(next / 60);
            int seconds = Mathf.FloorToInt(next % 60);
            _gameTimerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void HideAllPanels()
    {
        if (_waitingPanel != null) _waitingPanel.SetActive(false);
        if (_timerText != null) _timerText.text = "";
        if (_gameTimerText != null) _gameTimerText.text = "";
    }

    private void UpdateUIByState()
    {
        if (IsTowerMode)
        {
            HideAllPanels();
            return;
        }

        switch (CurrentState)
        {
            case GameState.WaitingForPlayers:
                if (_waitingPanel != null) _waitingPanel.SetActive(true);
                if (_waitingText != null)
                {
                    _waitingText.text = $"Waiting for players: {_connectedPlayers.Value}/{_requiredPlayers}";
                }
                if (_timerText != null)
                {
                    _timerText.text = "";
                }
                break;

            case GameState.Countdown:
                if (_waitingPanel != null) _waitingPanel.SetActive(true);
                if (_waitingText != null)
                {
                    _waitingText.text = "All players ready!";
                }
                break;

            case GameState.InProgress:
                if (_waitingPanel != null) _waitingPanel.SetActive(false);
                break;

            case GameState.ShowingResults:
                if (_waitingPanel != null) _waitingPanel.SetActive(false);
                break;
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (base.IsServerInitialized && base.ServerManager != null)
        {
            base.ServerManager.OnRemoteConnectionState -= OnPlayerConnectionChanged;
        }

        _connectedPlayers.OnChange -= OnConnectedPlayersChanged;
        _currentState.OnChange -= OnCurrentStateChanged;
        _syncTimer.OnChange -= OnSyncTimerChanged;
    }

    #endregion
}