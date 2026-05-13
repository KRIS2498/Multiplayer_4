using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using TMPro;
using UnityEngine;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private TMP_InputField _ipInput;
    [SerializeField] private Camera _menuCamera;
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private NetworkManager _networkManager; // Перетащите FishNetManager сюда

    // Сохраняем ник локально до появления сетевого объекта игрока.
    public static string PlayerNickname { get; private set; } = "Player";

    public void StartAsHost()
    {
        SaveNickname();

        if (_networkManager == null)
        {
            Debug.LogError("NetworkManager не назначен в ConnectionUI!");
            return;
        }

        // Отключаем камеру меню
        if (_menuCamera != null)
            _menuCamera.gameObject.SetActive(false);

        // Запуск сервера и клиента на хосте
        _networkManager.ServerManager.StartConnection();
        _networkManager.ClientManager.StartConnection();

        // Отключаем меню после запуска
        _menuPanel.SetActive(false);
    }

    public void StartAsClient()
    {
        SaveNickname();

        string serverIP = _ipInput != null && !string.IsNullOrWhiteSpace(_ipInput.text)
            ? _ipInput.text
            : "localhost";

        Debug.Log($"Connecting to server at: {serverIP}");

        // Правильный способ получить транспорт Tugboat
        Tugboat tugboat = _networkManager.TransportManager.GetTransport<Tugboat>();
        if (tugboat != null)
        {
            tugboat.SetClientAddress(serverIP);
            Debug.Log($"Client address set to: {serverIP}");
        }
        else
        {
            Debug.LogWarning("Tugboat transport not found!");
        }

        if (_menuCamera != null)
            _menuCamera.gameObject.SetActive(false);

        _networkManager.ClientManager.StartConnection();

        _menuPanel.SetActive(false);

        Debug.Log($"Client started with nickname: {PlayerNickname}, connecting to {serverIP}");
    }

    private void SaveNickname()
    {
        // Нормализуем ввод
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Player" : rawValue.Trim();
    }
}