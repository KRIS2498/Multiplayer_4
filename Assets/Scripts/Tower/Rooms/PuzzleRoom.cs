using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleRoom : BaseRoom
{
    [Header("Puzzle Settings")]
    [SerializeField] private int _requiredActivations = 3;
    [SerializeField] private GameObject _puzzleElementPrefab;
    [SerializeField] private Transform[] _puzzleElementPoints;

    private int _activatedCount;

    public override void InitializeRoom()
    {
        base.InitializeRoom();
        _activatedCount = 0;

        if (!base.IsServerInitialized) return;

        SpawnPuzzleElements();
    }

    private void SpawnPuzzleElements()
    {
        foreach (var point in _puzzleElementPoints)
        {
            if (point == null || _puzzleElementPrefab == null) continue;

            GameObject element = Instantiate(_puzzleElementPrefab, point.position, point.rotation);
            NetworkObject nob = element.GetComponent<NetworkObject>();
            if (nob != null)
            {
                base.Spawn(nob);
                var puzzler = element.GetComponent<PuzzleElement>();
                if (puzzler != null)
                    puzzler.OnActivated += OnElementActivated;
            }
        }
    }

    private void OnElementActivated()
    {
        _activatedCount++;
        if (_activatedCount >= _requiredActivations)
        {
            CompleteRoom();
        }
    }
}

public class PuzzleElement : NetworkBehaviour
{
    public event System.Action OnActivated;

    [SerializeField] private GameObject _activatedVisual;
    private bool _isActivated;

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;
        if (_isActivated) return;

        if (other.GetComponent<PlayerNetwork>() != null)
        {
            _isActivated = true;
            if (_activatedVisual != null)
                _activatedVisual.SetActive(true);

            OnActivated?.Invoke();
            ActivatedClientRpc();
        }
    }

    [ObserversRpc]
    private void ActivatedClientRpc()
    {
        if (_activatedVisual != null)
            _activatedVisual.SetActive(true);
    }
}
