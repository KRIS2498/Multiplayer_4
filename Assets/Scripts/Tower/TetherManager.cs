using FishNet.Object;
using System.Linq;
using UnityEngine;

public class TetherManager : NetworkBehaviour
{
    [Header("Tether Settings")]
    [SerializeField] private float _maxDistance = 10f;
    [SerializeField] private float _damagePerSecond = 5f;
    [SerializeField] private float _checkInterval = 0.5f;

    [Header("Visual")]
    [SerializeField] private Color _safeColor = Color.cyan;
    [SerializeField] private Color _damageColor = Color.red;

    private LineRenderer _line;
    private float _tetherTimer;
    private PlayerNetwork[] _cachedPlayers = new PlayerNetwork[2];

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        if (_line == null)
            _line = gameObject.AddComponent<LineRenderer>();

        _line.positionCount = 2;
        _line.startWidth = 0.1f;
        _line.endWidth = 0.1f;
        _line.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
        if (!base.IsSpawned) return;

        FindPlayers();

        if (_cachedPlayers[0] == null || _cachedPlayers[1] == null)
        {
            _line.enabled = false;
            return;
        }

        TowerManager tower = TowerManager.Instance;
        bool isFloorActive = tower != null &&
            tower.CurrentState == TowerManager.TowerState.FloorActive &&
            _cachedPlayers[0].IsAlive.Value &&
            _cachedPlayers[1].IsAlive.Value;

        if (!isFloorActive)
        {
            _line.enabled = false;
            return;
        }

        _line.enabled = true;

        Vector3 p1Pos = _cachedPlayers[0].transform.position + Vector3.up * 1.5f;
        Vector3 p2Pos = _cachedPlayers[1].transform.position + Vector3.up * 1.5f;
        _line.SetPosition(0, p1Pos);
        _line.SetPosition(1, p2Pos);

        if (base.IsServerInitialized)
        {
            _tetherTimer -= Time.deltaTime;
            if (_tetherTimer <= 0)
            {
                _tetherTimer = _checkInterval;
                CheckTether();
            }
        }
    }

    private void FindPlayers()
    {
        var players = FindObjectsOfType<PlayerNetwork>();
        if (players.Length >= 2)
        {
            _cachedPlayers[0] = players[0];
            _cachedPlayers[1] = players[1];
        }
    }

    private void CheckTether()
    {
        float distance = Vector3.Distance(
            _cachedPlayers[0].transform.position,
            _cachedPlayers[1].transform.position);

        _line.startColor = distance > _maxDistance ? _damageColor : _safeColor;
        _line.endColor = distance > _maxDistance ? _damageColor : _safeColor;

        if (distance > _maxDistance)
        {
            float damage = _damagePerSecond * _checkInterval;
            int damageInt = Mathf.Max(1, Mathf.RoundToInt(damage));

            TowerManager tower = TowerManager.Instance;
            if (tower != null)
            {
                tower.ServerApplyDamage(_cachedPlayers[0].Owner.ClientId, damageInt, -1);
                tower.ServerApplyDamage(_cachedPlayers[1].Owner.ClientId, damageInt, -1);
            }

            Debug.Log($"[Tether] Too far ({distance:F1}m > {_maxDistance}m) — {damageInt} dmg");
        }
        else
        {
            _line.startColor = _safeColor;
            _line.endColor = _safeColor;
        }
    }
}
