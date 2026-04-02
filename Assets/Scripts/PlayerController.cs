using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] float frontOffset = 0;
    Camera mainCam;

    [Header("Miss Event Trigger")]
    [Tooltip("What happen when bullet hit player?")]
    public UnityEvent onMissDefense;

    // ── Low-pass Filter on Hit ────────────────────────────────────────────────
    [Header("Low-pass Filter on Hit")]
    [Tooltip("The AudioLowPassFilter sitting on your music AudioSource (MidiRhythmManager object).")]
    public AudioMixer audioMixer;
    public string exposedParameterName = "MusicLowpassCutoff";
    [Tooltip("Cutoff frequency dropped to this value on hit (Hz). Lower = more muffled. Range 10–22000.")]
    [Range(10f, 22000f)]
    public float hitCutoffFrequency = 500f;

    [Tooltip("How long (seconds) until the filter fully recovers back to 22000 Hz.")]
    public float recoveryDuration = 2f;

    [Tooltip("Recovery curve shape. Left = just after hit, Right = fully recovered.")]
    public AnimationCurve recoveryCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    // ── Private ───────────────────────────────────────────────────────────────
    private float DEFAULT_CUTOFF = 22000f;   // Unity's "no filter" value
    private Coroutine _recoveryCoroutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        mainCam = Camera.main;
        DEFAULT_CUTOFF = audioMixer != null ? audioMixer.GetFloat(exposedParameterName, out var cutoff) ? cutoff : DEFAULT_CUTOFF : DEFAULT_CUTOFF;
    }

    // Update is called once per frame
    void Update()
    {

        MouseController();
    }

    private Vector2 MouseController()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, Mathf.Abs(mainCam.transform.position.z)));
        Vector2 direction = (Vector2)mouseWorldPos - (Vector2)transform.position;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0, 0, angle + frontOffset);
        return mousePos;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<Bullet2D>(out var bullet)) return;

        onMissDefense?.Invoke();
        ApplyLowPassHit();
    }
    private void ApplyLowPassHit()
    {
        if (audioMixer == null) return;

        // Cancel any ongoing recovery so a second hit resets from the top
        if (_recoveryCoroutine != null)
        {
            StopCoroutine(_recoveryCoroutine);
            _recoveryCoroutine = null;
        }

        // Instant drop
        SetMixerCutoff(hitCutoffFrequency);

        _recoveryCoroutine = StartCoroutine("RecoverLowPass");
    }
    private IEnumerable RecoverLowPass()
    {
        float elapsed = 0f;

        while (elapsed < recoveryDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / recoveryDuration);

            // Use the curve so you can shape the recovery (e.g. fast at first, slow at end)
            float curvedT = recoveryCurve.Evaluate(t);
            SetMixerCutoff(Mathf.Lerp(hitCutoffFrequency, DEFAULT_CUTOFF, curvedT));

            yield return null;
        }

        // Guarantee full recovery
        SetMixerCutoff(DEFAULT_CUTOFF);
        _recoveryCoroutine = null;
    }
    private void SetMixerCutoff(float frequency)
    {
        if (audioMixer == null) return;
 
        bool ok = audioMixer.SetFloat(exposedParameterName, frequency);
 
        if (!ok)
            Debug.LogError($"[PlayerController] AudioMixer parameter '{exposedParameterName}' not found. " +
                           $"Right-click the Cutoff knob in the Mixer → Expose to script, " +
                           $"then rename it to '{exposedParameterName}' in the Exposed Parameters list.");
    }
}
    