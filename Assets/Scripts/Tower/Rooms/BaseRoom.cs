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
    [SerializeField] protected GameObject _exitDoorModel;
    [SerializeField] protected Collider _exitTrigger;

    public RoomType RoomType => _roomType;
    public bool IsCompleted { get; protected set; }
    public int RoomIndex { get; set; }
    public Transform[] PlayerSpawnPoints => _playerSpawnPoints;

    protected List<PlayerNetwork> _playersInRoom = new();

    public virtual void InitializeRoom()
    {
        IsCompleted = false;

        if (_exitDoorModel != null)
            _exitDoorModel.SetActive(true);

        if (_exitTrigger != null)
            _exitTrigger.enabled = false;
    }

    public virtual void OnPlayersEntered(List<PlayerNetwork> players)
    {
        _playersInRoom = players;
    }

    protected virtual void CompleteRoom()
    {
        if (IsCompleted) return;
        IsCompleted = true;

        if (_exitTrigger != null)
            _exitTrigger.enabled = true;

        OnRoomCompleted?.Invoke(this);
    }

    public virtual void CleanupRoom() { }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;
        if (!IsCompleted) return;
        if (_exitTrigger == null) return;
        if (other.GetComponent<PlayerNetwork>() == null) return;

        TowerManager.Instance?.OnPlayerEnteredExit();
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

        if (_exitTrigger != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_exitTrigger.bounds.center, _exitTrigger.bounds.size);
        }
    }
}
