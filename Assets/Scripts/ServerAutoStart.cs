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

            // НЕ отключаем Canvas полностью! Просто отключаем ненужные элементы
            DisableUnnecessaryComponents();

            if (InstanceFinder.NetworkManager != null)
            {
                InstanceFinder.ServerManager.StartConnection();
                Debug.Log("[Server] Server started on port 7770");
            }
        }
    }

    private void DisableUnnecessaryComponents()
    {
        // Отключаем только MAIN камеру (рендеринг не нужен)
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);

        // Отключаем Audio Listener (звук не нужен)
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        foreach (AudioListener listener in listeners)
            listener.enabled = false;

        // Canvas НЕ отключаем! Он нужен для GameManager UI
        // Но отключаем кнопки управления, которые не нужны на сервере
        ConnectionUI connectionUI = FindObjectOfType<ConnectionUI>();
        if (connectionUI != null)
            connectionUI.gameObject.SetActive(false);
    }
}