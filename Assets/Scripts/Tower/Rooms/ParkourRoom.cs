using System.Collections.Generic;
using UnityEngine;

public class ParkourRoom : BaseRoom
{
    [Header("Parkour Settings")]
    [SerializeField] private Transform _finishLineTrigger;
    [SerializeField] private float _minBothAliveTime = 2f;

    private readonly HashSet<int> _playersAtFinish = new();

    public override void InitializeRoom()
    {
        base.InitializeRoom();
        _playersAtFinish.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;
        if (IsCompleted) return;

        PlayerNetwork player = other.GetComponent<PlayerNetwork>();
        if (player == null) return;

        if (_playersAtFinish.Contains(player.Owner.ClientId)) return;
        _playersAtFinish.Add(player.Owner.ClientId);

        if (_playersAtFinish.Count >= 2)
        {
            CompleteRoom();
        }
    }
}
