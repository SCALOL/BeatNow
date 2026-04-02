using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Assign UI Element")]
    public Slider DurationSlider;
    public TextMeshProUGUI ScoreCountText;
    public TextMeshProUGUI ComboCountText;
    public TextMeshProUGUI CountdownText;
    public CanvasGroup CountDowncanvasGroup;
    public CanvasGroup ComboCounterCanvasGroup;
    public GameObject GameOverCanvas;
    public GameObject PauseCanvas;

    [Header("Monitor Variable")]
    public int DefenseCounter = 0;
    public int ComboCounter = 0;
    public int ScoreCounter = 0;

    [Header("UI Text Format")]
    public string ComboCounterTextFormat;

    // ── FIX: made public so MidiRhythmManager can read it ────────────────────
    public float fadeDuration = 1f;

    public static UIManager Instance { get; private set; }

    // ── Coroutine handles so we never accidentally kill unrelated coroutines ──
    private Coroutine _countdownFadeCoroutine;
    private Coroutine _comboFadeCoroutine;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ComboCounter = 0;
        DefenseCounter = 0;
        ScoreCounter = 0;

        PauseCanvas.SetActive(false);
        GameOverCanvas.SetActive(false);
    }

    // ── Defense / Score ───────────────────────────────────────────────────────
    public void AddDefenseCounter()
    {
        ComboCounter++;
        DefenseCounter++;
        UpdateUICounterText();
    }

    public void NormalScoreAdd() => ScoreMultiply(MidiRhythmManager.NoteType.Normal);
    public void HealScoreAdd() => ScoreMultiply(MidiRhythmManager.NoteType.Heal);
    public void CriticalScoreAdd() => ScoreMultiply(MidiRhythmManager.NoteType.Critical);

    private void ScoreMultiply(MidiRhythmManager.NoteType notetype)
    {
        switch (notetype)
        {
            case MidiRhythmManager.NoteType.Normal: ScoreCounter += 100 * ComboCounter; break;
            case MidiRhythmManager.NoteType.Heal: ScoreCounter += 150 * ComboCounter; break;
            case MidiRhythmManager.NoteType.Critical: ScoreCounter += 200 * ComboCounter; break;
        }
    }

    public void UpdateUICounterText()
    {
        ComboCountText.text = ComboCounter + ComboCounterTextFormat;
        ScoreCountText.text = ScoreCounter.ToString("D6");
    }

    public void UpdateSongProgress(int progress)
    {
        DurationSlider.value = progress;
    }

    public void ResetComboCounter()
    {
        ComboCounter = 0;
    }

    // ── Countdown UI ──────────────────────────────────────────────────────────
    public void ActivateCountdownUI()
    {
        CountDowncanvasGroup.alpha = 1f;
    }

    public void SetCountdown(int number)
    {
        CountdownText.text = number.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FIX: was Time.deltaTime — broken on WebGL because timeScale can be 0
    //      during first frames of a scene load, making elapsedTime stuck at 0.
    //      Time.unscaledDeltaTime runs regardless of timeScale.
    // ─────────────────────────────────────────────────────────────────────────
    public IEnumerator FadeOutAndLoadScene()
    {
        // Stop any existing countdown fade so they don't fight each other
        if (_countdownFadeCoroutine != null)
            StopCoroutine(_countdownFadeCoroutine);

        _countdownFadeCoroutine = StartCoroutine(FadeOutCountdownRoutine());
        yield return _countdownFadeCoroutine;
    }

    private IEnumerator FadeOutCountdownRoutine()
    {
        float elapsed = 0f;
        float startAlpha = CountDowncanvasGroup.alpha;

        while (elapsed < fadeDuration)
        {
            // ✅ unscaledDeltaTime — works even when Time.timeScale == 0
            elapsed += Time.unscaledDeltaTime;
            CountDowncanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            yield return null;
        }

        CountDowncanvasGroup.alpha = 0f;
        _countdownFadeCoroutine = null;
    }

    // ── Combo counter fade ────────────────────────────────────────────────────
    public void ActivateFadeComboCounter()
    {
        // Stop only the combo fade coroutine — not ALL coroutines
        // (StopAllCoroutines was killing FadeOutAndLoadScene on desktop too,
        //  it just wasn't noticeable because desktop has no timeScale=0 issue)
        if (_comboFadeCoroutine != null)
            StopCoroutine(_comboFadeCoroutine);

        ComboCounterCanvasGroup.alpha = 1f;
        _comboFadeCoroutine = StartCoroutine(FadeOutComboCounter());
    }

    public IEnumerator FadeOutComboCounter()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // ✅ unscaled
            ComboCounterCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        ComboCounterCanvasGroup.alpha = 0f;
        _comboFadeCoroutine = null;
    }

    // ── Pause / Game Over ─────────────────────────────────────────────────────
    public void UIPauseToggle()
    {
        PauseCanvas.SetActive(!PauseCanvas.activeSelf);
    }

    public void ActivateGameOverCanvas(bool toggle)
    {
        GameOverCanvas.SetActive(toggle);
    }

    public void UIFinish()
    {
        ActivateGameOverCanvas(true);
    }
}