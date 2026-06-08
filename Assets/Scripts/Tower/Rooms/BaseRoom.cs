using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseRoom : NetworkBehaviour
{
    public event Action<BaseRoom> OnRoomCompleted;

    [Header("Room Settings")]
    [SerializeField] private RoomType _roomType;
    [SerializeField] protected Transform[] _playerSpawnPoints;
    [SerializeField] protected Transform _exitDoor;
    [SerializeField] protected float _autoCloseDelay = 1f;

    public RoomType RoomType => _roomType;
    public bool IsCompleted { get; protected set; }
    public int RoomIndex { get; set; }
    public Transform[] PlayerSpawnPoints => _playerSpawnPoints;

    protected List<PlayerNetwork> _playersInRoom = new();

    public virtual void InitializeRoom()
    {
        IsCompleted = false;
        if (_exitDoor != null)
            _exitDoor.gameObject.SetActive(false);
    }

    public virtual void OnPlayersEntered(List<PlayerNetwork> players)
    {
        _playersInRoom = players;
    }

    protected virtual void CompleteRoom()
    {
        if (IsCompleted) return;
        IsCompleted = true;

        if (_exitDoor != null)
            _exitDoor.gameObject.SetActive(true);

        OnRoomCompleted?.Invoke(this);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (_playerSpawnPoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var sp in _playerSpawnPoints)
            {
                if (sp != null)
                    Gizmos.DrawWireSphere(sp.position, 0.5f);
            }
        }
    }
}
