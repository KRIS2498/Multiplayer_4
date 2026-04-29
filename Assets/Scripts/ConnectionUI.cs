using TMPro;
using FishNet.Managing;
using FishNet.Connection;
using UnityEngine;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nicknameInput;
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

        if (_networkManager == null)
        {
            Debug.LogError("NetworkManager не назначен в ConnectionUI!");
            return;
        }

        // Отключаем камеру меню
        if (_menuCamera != null)
            _menuCamera.gameObject.SetActive(false);

        // Подключение к серверу
        _networkManager.ClientManager.StartConnection();

        // Отключаем меню после запуска
        _menuPanel.SetActive(false);
    }

    private void SaveNickname()
    {
        // Нормализуем ввод
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Player" : rawValue.Trim();
    }
}