using UnityEngine;
using System.Collections;
using FishNet.Managing;

public class SimpleLatencySimulator : MonoBehaviour
{
    [Header("Latency Settings")]
    [SerializeField] private bool _enableSimulation = false;
    [SerializeField] private float _latencyMS = 200f;

    [Header("Controls")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F3;
    [SerializeField] private KeyCode _increaseKey = KeyCode.F4;
    [SerializeField] private KeyCode _decreaseKey = KeyCode.F5;

    private NetworkManager _networkManager;
    private bool _wasEnabled = false;

    private void Start()
    {
        _networkManager = GetComponent<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogWarning("NetworkManager not found on this object!");
        }
        else
        {
            StartCoroutine(LatencyCoroutine());
        }
    }

    private void Update()
    {
        // Включение/выключение задержки
        if (Input.GetKeyDown(_toggleKey))
        {
            _enableSimulation = !_enableSimulation;
            Debug.Log($"Latency simulation: {(_enableSimulation ? "ENABLED" : "DISABLED")} - {_latencyMS}ms");
        }

        // Увеличение задержки
        if (Input.GetKeyDown(_increaseKey))
        {
            _latencyMS = Mathf.Min(500, _latencyMS + 25);
            if (_enableSimulation)
                Debug.Log($"Latency increased to {_latencyMS}ms");
        }

        // Уменьшение задержки
        if (Input.GetKeyDown(_decreaseKey))
        {
            _latencyMS = Mathf.Max(0, _latencyMS - 25);
            if (_enableSimulation)
                Debug.Log($"Latency decreased to {_latencyMS}ms");
        }

        // Отображаем текущий статус в консоли раз в 5 секунд (если включено)
        if (_enableSimulation && !_wasEnabled)
        {
            Debug.Log($"[Latency Simulator] Active: {_latencyMS}ms delay");
            _wasEnabled = true;
        }
        else if (!_enableSimulation && _wasEnabled)
        {
            Debug.Log("[Latency Simulator] Inactive");
            _wasEnabled = false;
        }
    }

    private IEnumerator LatencyCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.05f); // Проверка каждые 50мс

            if (!_enableSimulation) continue;
            if (_networkManager == null) continue;

            // Симулируем задержку на серверных операциях
            float delay = _latencyMS / 1000f;
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private void OnGUI()
    {
        if (!_enableSimulation) return;

        // Отображаем информацию в левом верхнем углу
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.yellow;
        style.fontStyle = FontStyle.Bold;

        GUI.Label(new Rect(10, 10, 300, 30), $"zaderjka: {_latencyMS} ms", style);
        GUI.Label(new Rect(10, 40, 300, 25), $"Press F3: vkl/vikl | F4/F5: +/- 25ms", style);
    }
}