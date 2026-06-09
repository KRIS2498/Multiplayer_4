using FishNet;
using UnityEngine;

public class ServerAutoStart : MonoBehaviour
{
    [SerializeField] private bool _autoStartInEditor = false;

    private void Start()
    {
        bool isHeadless = System.Environment.CommandLine.Contains("-batchmode")
                          || System.Environment.CommandLine.Contains("-nographics")
                          || Application.isBatchMode;

        if (isHeadless || _autoStartInEditor)
        {
            Debug.Log("[Server] Starting dedicated server...");
            DisableUnnecessaryComponents();
            StartCoroutine(WaitAndStartServer());
        }
    }

    private System.Collections.IEnumerator WaitAndStartServer()
    {
        float timeout = 5f;
        while (InstanceFinder.NetworkManager == null && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (InstanceFinder.NetworkManager != null)
        {
            InstanceFinder.ServerManager.StartConnection();
            Debug.Log("[Server] Server started on port 7770");
        }
        else
        {
            Debug.LogError("[Server] NetworkManager not found after 5s! Server not started.");
        }
    }

    private void DisableUnnecessaryComponents()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);

        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        foreach (AudioListener listener in listeners)
            listener.enabled = false;

        ConnectionUI connectionUI = FindObjectOfType<ConnectionUI>();
        if (connectionUI != null)
            connectionUI.gameObject.SetActive(false);
    }
}