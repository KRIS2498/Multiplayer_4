using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private Camera _menuCamera;
    [SerializeField] private GameObject _menuPanel;

    // Сохраняем ник локально до появления сетевого объекта игрока.
    public static string PlayerNickname { get; private set; } = "Player";

    public void StartAsHost()
    {
        SaveNickname();
        if (NetworkManager.Singleton != null)
        {
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
        }
        if (_menuCamera != null)
            _menuCamera.gameObject.SetActive(false);
        // Хост одновременно является сервером и клиентом.
        NetworkManager.Singleton.StartHost();
        // Отключаем меню после запуска
        _menuPanel.SetActive(false);
    }

    public void StartAsClient()
    {
        SaveNickname();
        if (NetworkManager.Singleton != null)
        {
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
        }
        if (_menuCamera != null)
            _menuCamera.gameObject.SetActive(false);
        // Клиент только подключается к уже запущенному хосту.
        NetworkManager.Singleton.StartClient();
        // Отключаем меню после запуска
        _menuPanel.SetActive(false);
    }

    private void SaveNickname()
    {
        // Нормализуем ввод, чтобы сервер не получил пустую строку.
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Player" : rawValue.Trim();
    }
}