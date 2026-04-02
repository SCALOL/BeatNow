using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to your Player (or a child trigger collider on the Player).
/// Detects Bullet2D arrivals and scores them by proximity to the nearest
/// mapped MIDI note — so "perfect" means the bullet arrived exactly on beat.
///
/// Setup:
///   1. Tag this GameObject "Player"
///   2. Collider2D → Is Trigger ✅
///   3. Assign MidiRhythmManager in Inspector
///   4. Wire Unity Events to flash effects, HP loss, score popups, etc.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RhythmHitZone2D : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("References")]
    public MidiRhythmManager midiManager;

    [Header("Timing Windows  (seconds from nearest note)")]
    [Tooltip("≤ this offset = PERFECT")]
    public float perfectWindow = 0.08f;

    [Tooltip("≤ this offset = GOOD  (anything larger = MISS)")]
    public float goodWindow    = 0.15f;

    [Header("Generic Rating Events  (any NoteType)")]
    public UnityEvent onDefense;

    [Header("Per-NoteType DefenseType Events")]
    [Tooltip("Wire these to unique effects per bullet type.")]
    public UnityEvent onNormalDefense;
    public UnityEvent onHealDefense;
    public UnityEvent onCriticalDefense;

    [Header("Debug")]
    public bool showLog = true;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    // ── 2D Trigger ────────────────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only score Bullet2D colliders
        if (!other.TryGetComponent<Bullet2D>(out var bullet)) return;

        ApplyRating(bullet.NoteType, bullet.Velocity);
        UIManager.Instance.ActivateFadeComboCounter();
    }

    // ── Timing Evaluation ─────────────────────────────────────────────────────
    /// <summary>
    /// Finds the nearest note timestamp in the MIDI schedule to the current
    /// AudioSource time and returns a rating based on the configured windows.
    /// </summary>

    // ── Apply Rating ──────────────────────────────────────────────────────────
    private void ApplyRating(MidiRhythmManager.NoteType type, int velocity)
    {
        if (showLog)
            Debug.Log($"[RhythmHitZone2D] {type} | vel={velocity}");

        onDefense?.Invoke();
        FireTypedEvent(type);
    }

    private void FireTypedEvent(MidiRhythmManager.NoteType type)
    {
        switch (type)
        {
            case MidiRhythmManager.NoteType.Normal:   onNormalDefense?.Invoke();   break;
            case MidiRhythmManager.NoteType.Heal: onHealDefense?.Invoke(); break;
            case MidiRhythmManager.NoteType.Critical: onCriticalDefense?.Invoke(); break;
        }
    }
}
