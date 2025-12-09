using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class ClientRoundUI : MonoBehaviour
{
    public static ClientRoundUI Instance { get; private set; }

    private readonly string centerText = "centerText";
    private readonly string countdownText = "countdownText";
    private readonly string joinCodeLabel = "joinCodeLabel";
    private readonly string joinCodeInputDesc = "joinCodeInput";
    private readonly string joinButtonDesc = "joinButton";

    private UIDocument _uiDoc;
    private Label _centerLabel;
    private Label _countdownLabel;
    private Label _joinCodeLabel;
    private TextField _joinCodeInput;
    private Button _joinButton;
    private Button _hostButton;
    private Coroutine _countdownCoroutine;
    private bool _persistent = false;

    void Awake()
    {
        Instance = this;
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (!TryGetComponent(out _uiDoc))
            return;

        var root = _uiDoc.rootVisualElement;
        _centerLabel = root.Q<Label>(centerText);
        _countdownLabel = root.Q<Label>(countdownText);
        _joinCodeLabel = root.Q<Label>(joinCodeLabel);
        _joinCodeInput = root.Q<TextField>(joinCodeInputDesc);
        _joinButton = root.Q<Button>(joinButtonDesc);
        _hostButton = root.Q<Button>("hostButton");

        if (_joinButton != null)
            _joinButton.clicked += OnJoinClicked;
        if (_hostButton != null)
            _hostButton.clicked += OnHostClicked;

        HideAll();
        SetInputVisible(true);
    }

    private void OnJoinClicked()
    {
        if (_joinCodeInput == null) return;
        string code = _joinCodeInput.value;
        if (string.IsNullOrEmpty(code)) return;

        var net = FindAnyObjectByType<NetworkManagerController>();
        if (net != null)
        {
            net.JoinRelayGame(code);
        }
    }

    private void OnHostClicked()
    {
        var net = FindAnyObjectByType<NetworkManagerController>();
        if (net != null)
        {
            net.HostRelayGame();
        }
    }

    private void SetInputVisible(bool visible)
    {
        var style = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_joinCodeInput != null) _joinCodeInput.style.display = style;
        if (_joinButton != null) _joinButton.style.display = style;
        if (_hostButton != null) _hostButton.style.display = style;
    }

    public void ShowDeathAndStartCountdown(string message, float seconds)
    {
        if (_uiDoc == null)
            return;

        _persistent = false;

        if (_countdownCoroutine != null)
            StopCoroutine(_countdownCoroutine);

        _centerLabel.text = message;
        _centerLabel.style.display = DisplayStyle.Flex;

        _countdownCoroutine = StartCoroutine(CountdownRoutine(seconds));
    }

    public void ShowPersistent(string message)
    {
        if (_uiDoc == null) return;
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        _centerLabel.text = message;
        _centerLabel.style.display = DisplayStyle.Flex;
        if (_countdownLabel != null)
            _countdownLabel.style.display = DisplayStyle.None;

        _persistent = true;
        SetInputVisible(false);
    }

    public void ShowServerIP(string code)
    {
        if (_uiDoc == null) return;

        ShowPersistent("Waiting for player...");
        
        if (_joinCodeLabel != null)
        {
            _joinCodeLabel.text = $"HOST CODE: {code}";
            _joinCodeLabel.style.display = DisplayStyle.Flex;
        }
        
        SetInputVisible(false);
    }

    public void HidePersistent()
    {
        _persistent = false;
        HideAll();
        SetInputVisible(false); 
    }

    private IEnumerator CountdownRoutine(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            _countdownLabel.text = Mathf.CeilToInt(remaining).ToString();
            _countdownLabel.style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        _countdownLabel.text = "0";
        yield return new WaitForSeconds(0.5f);

        HideAll();
        SetInputVisible(true);

        _countdownCoroutine = null;
    }

    public void HideAll()
    {
        if (_centerLabel != null)
            _centerLabel.style.display = DisplayStyle.None;
        if (_countdownLabel != null)
            _countdownLabel.style.display = DisplayStyle.None;
    }
}
