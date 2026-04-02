using UnityEngine;

/// <summary>
/// 2D bullet that travels toward the player, arrives on a beat (next note),
/// and disappears via OnTriggerEnter2D.
///
/// Prefab requirements:
///   • Sprite pointing UP  (transform.up is the travel direction)
///   • CircleCollider2D    → Is Trigger ✅
///   • Rigidbody2D         → Body Type: Kinematic, Simulated ✅
///   • This script
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Bullet2D : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Optional particle played on destruction.")]
    public GameObject hitParticlePrefab;

    [Tooltip("Color shifts from left (spawn) to right (arrival) as bullet travels.")]
    public Gradient travelColorGradient;

    [Tooltip("Scale multiplier over the bullet's lifetime.")]
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.8f, 1f, 1.3f);

    [Header("Behaviour")]
    [Tooltip("If true, bullet steers toward the player's CURRENT position each frame.")]
    public bool isHoming = false;
    public float homingTurnSpeed = 180f;    // degrees per second

    [Tooltip("Auto-destroy after this many seconds if nothing is hit.")]
    public float maxLifetime = 8f;

    // ── Read-only after init ──────────────────────────────────────────────────
    /// <summary>The NoteType that spawned this bullet (usable by RhythmHitZone2D).</summary>
    public MidiRhythmManager.NoteType NoteType { get; private set; }
    /// <summary>MIDI velocity 0-127 of the original note.</summary>
    public int Velocity { get; private set; }

    // ── Private state ─────────────────────────────────────────────────────────
    private Transform _target;
    private float _travelDuration;
    private float _elapsed;
    private Vector2 _startPos;
    private Vector2 _targetPos;      // snapshot of player pos at spawn (non-homing)
    private BulletSpawner2D _spawner;
    private bool _initialized;
    private bool _dead;

    private SpriteRenderer _sprite;
    private Vector3 _baseScale;

    // ── Initialization ────────────────────────────────────────────────────────
    public void Initialize(
        Transform target,
        float travelDuration,
        MidiRhythmManager.NoteType noteType,
        int velocity,
        BulletSpawner2D spawner)
    {
        _target = target;
        _travelDuration = Mathf.Max(travelDuration, 0.05f);
        NoteType = noteType;
        Velocity = velocity;
        _spawner = spawner;
        _startPos = transform.position;
        _targetPos = target.position;          // snapshot for non-homing
        _baseScale = transform.localScale;
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _initialized = true;

        // Rotate sprite to face the player (sprite art must point UP)
        Vector2 dir = _targetPos - _startPos;
        if (dir != Vector2.zero)
            transform.up = dir.normalized;
    }

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (!_initialized || _dead || Time.timeScale == 0f) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _travelDuration);

        Move(t);
        UpdateVisuals(t);
    }

    // ── Movement ──────────────────────────────────────────────────────────────
    private void Move(float t)
    {
        
        if (isHoming && _target != null)
        {
            // Rotate toward current player position
            Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
            if (toTarget != Vector2.zero)
            {
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
                float currentAngle = transform.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle,
                                                             homingTurnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }

            float speed = Vector2.Distance(_startPos, (Vector2)_target.position) / _travelDuration;
            transform.position += transform.up * speed * Time.deltaTime;
        }
        else
        {
            // Linear interpolation to the snapshotted position — arrives exactly on next note
            transform.position = Vector2.Lerp(_startPos, _targetPos, t);
        }
    }

    // ── Visuals ───────────────────────────────────────────────────────────────
    private void UpdateVisuals(float t)
    {
        if (_sprite != null && travelColorGradient != null)
            _sprite.color = travelColorGradient.Evaluate(t);

        transform.localScale = _baseScale * scaleCurve.Evaluate(t);
    }

    // ── 2D Trigger — the core "disappear on trigger" requirement ──────────────
    /// <summary>
    /// Bullet vanishes when it enters ANY 2D trigger.
    /// Tag the player collider "Player" so RhythmHitZone2D can distinguish it.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[Bullet2D] Triggered by {other.name} (tag={other.tag})");
        if (_dead) return;
        Die(other.CompareTag("Player") || other.CompareTag("Shield"));
    }

    // ── Death ─────────────────────────────────────────────────────────────────
    private void Die(bool hitPlayer)
    {
        if (_dead) return;
        _dead = true;

        if (hitParticlePrefab != null)
            Destroy(Instantiate(hitParticlePrefab, transform.position, Quaternion.identity), 3f);

        _spawner?.NotifyBulletDestroyed(gameObject);

        if (hitPlayer)
            Debug.Log($"[Bullet2D] Player hit by {NoteType} bullet (vel={Velocity})");

        Destroy(gameObject);
    }
}
