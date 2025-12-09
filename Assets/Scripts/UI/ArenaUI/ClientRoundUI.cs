using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class ClientRoundUI : MonoBehaviour
{
    public static ClientRoundUI Instance { get; private set; }

    private readonly string centerText = "centerText";
    private readonly string countdownText = "countdownText";

    private UIDocument _uiDoc;
    private Label _centerLabel;
    private Label _countdownLabel;
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

        HideAll();
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
    }

    public void HidePersistent()
    {
        _persistent = false;
        HideAll();
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
