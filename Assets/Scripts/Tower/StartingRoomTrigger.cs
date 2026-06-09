using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

public class StartingRoomTrigger : NetworkBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI _doorPromptText;
    [SerializeField] private string _waitingPrompt = "Waiting for partner...";
    [SerializeField] private string _readyPrompt = "Both ready! Starting run...";

    private readonly HashSet<int> _playersInside = new();

    private void OnTriggerEnter(Collider other)
    {
        PlayerNetwork pn = other.GetComponentInParent<PlayerNetwork>();
        if (pn == null) return;

        _playersInside.Add(pn.Owner.ClientId);
        UpdateDoorPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerNetwork pn = other.GetComponentInParent<PlayerNetwork>();
        if (pn == null) return;

        _playersInside.Remove(pn.Owner.ClientId);
        UpdateDoorPrompt();
    }

    private void UpdateDoorPrompt()
    {
        TowerManager tm = TowerManager.Instance;
        if (tm == null) return;

        bool bothInsideAndLobby = _playersInside.Count >= 2 && tm.CurrentState == TowerManager.TowerState.Lobby;

        if (_doorPromptText != null)
        {
            _doorPromptText.text = bothInsideAndLobby ? _readyPrompt : _waitingPrompt;
        }

        if (bothInsideAndLobby && base.IsServer)
            tm.TryStartRunFromDoor();
    }

    private void Reset()
    {
        _waitingPrompt = "Waiting for partner...";
        _readyPrompt = "Both ready! Starting run...";
    }
}
