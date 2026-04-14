using Unity.Netcode;
using UnityEngine;

public class PlayerCameraSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject _cameraPrefab;

    private GameObject _localCamera;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Спавним камеру только для владельца
            _localCamera = Instantiate(_cameraPrefab, transform.position, Quaternion.identity);
            _localCamera.transform.SetParent(transform);
            _localCamera.transform.localPosition = new Vector3(0, 1.5f, 0);

            // AudioListener активен только на этой камере
            var audioListener = _localCamera.GetComponent<AudioListener>();
            if (audioListener == null)
                _localCamera.AddComponent<AudioListener>();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_localCamera != null)
            Destroy(_localCamera);
    }
}