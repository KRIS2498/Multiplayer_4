using UnityEngine;

public class TetherManager : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color _safeColor = Color.cyan;
    [SerializeField] private Color _damageColor = Color.red;

    private LineRenderer _line;
    private PlayerNetwork _player1;
    private PlayerNetwork _player2;

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
        FindPlayers();
        if (_player1 == null || _player2 == null)
        {
            if (_line.enabled) _line.enabled = false;
            return;
        }

        TowerManager tower = TowerManager.Instance;
        bool isFloorActive = tower != null &&
            tower.CurrentState == TowerManager.TowerState.FloorActive &&
            _player1.IsAlive.Value && _player2.IsAlive.Value;

        if (!isFloorActive)
        {
            if (_line.enabled) _line.enabled = false;
            return;
        }

        _line.enabled = true;

        Vector3 p1Pos = _player1.transform.position + Vector3.up * 1.5f;
        Vector3 p2Pos = _player2.transform.position + Vector3.up * 1.5f;
        _line.SetPosition(0, p1Pos);
        _line.SetPosition(1, p2Pos);

        float distance = Vector3.Distance(_player1.transform.position, _player2.transform.position);
        bool isTooFar = distance > tower.TetherMaxDistance;

        _line.startColor = isTooFar ? _damageColor : _safeColor;
        _line.endColor = isTooFar ? _damageColor : _safeColor;
    }

    private void FindPlayers()
    {
        var players = FindObjectsOfType<PlayerNetwork>();
        if (players.Length < 2)
        {
            _player1 = null;
            _player2 = null;
            return;
        }

        _player1 = players[0];
        _player2 = players[1];
    }
}
