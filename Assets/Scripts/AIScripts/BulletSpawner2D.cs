using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens to MidiRhythmManager.OnNoteEvent and spawns typed 2D bullets
/// around the outer ring. Each NoteType has its own prefab, count, and pattern.
/// </summary>
public class BulletSpawner2D : MonoBehaviour
{
    // ── Per-NoteType config ───────────────────────────────────────────────────
    [System.Serializable]
    public class NoteConfig
    {
        [Tooltip("Which NoteType this row controls.")]
        public MidiRhythmManager.NoteType noteType = MidiRhythmManager.NoteType.Normal;

        [Tooltip("Bullet prefab (must have Bullet2D).")]
        public GameObject bulletPrefab;

        [Range(1, 16)]
        public int bulletsPerNote = 1;

        public SpawnPattern pattern = SpawnPattern.Random;

        [Tooltip("Degrees added to the spiral each time this note fires.")]
        public float spiralStep = 137.5f;   // golden angle gives nice spread

        [HideInInspector] public float _angle    = 0f;
        [HideInInspector] public int   _fireCount = 0;
    }

    public enum SpawnPattern { Random, Alternating, Spiral, Cardinal, Converging, Ring }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("References")]
    public MidiRhythmManager midiManager;
    public Transform         playerTransform;

    [Header("Spawn Ring")]
    [Tooltip("Radius of the outer circle where bullets appear (world units).")]
    public float spawnRadius = 8f;
    [Tooltip("Random angle spread used by Converging pattern.")]
    public float angleJitter = 15f;

    [Header("Note Configs")]
    [Tooltip("One entry per NoteType. Drag a different prefab/pattern into each row.")]
    public List<NoteConfig> noteConfigs = new List<NoteConfig>
    {
        new NoteConfig { noteType = MidiRhythmManager.NoteType.Normal,   bulletsPerNote = 1, pattern = SpawnPattern.Random      },
        new NoteConfig { noteType = MidiRhythmManager.NoteType.Heal, bulletsPerNote = 3, pattern = SpawnPattern.Spiral      },
        new NoteConfig { noteType = MidiRhythmManager.NoteType.Critical, bulletsPerNote = 8, pattern = SpawnPattern.Ring        },
    };

    // ── Private ───────────────────────────────────────────────────────────────
    private Dictionary<MidiRhythmManager.NoteType, NoteConfig> _configMap;
    private readonly List<GameObject> _activeBullets = new List<GameObject>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Start()
    {
        if (midiManager == null)    { Debug.LogError("[BulletSpawner2D] MidiRhythmManager not assigned!"); return; }
        if (playerTransform == null){ Debug.LogError("[BulletSpawner2D] Player transform not assigned!");  return; }

        _configMap = new Dictionary<MidiRhythmManager.NoteType, NoteConfig>();
        foreach (var cfg in noteConfigs)
            _configMap[cfg.noteType] = cfg;

        // Subscribe AFTER the manager is ready
        midiManager.OnNoteEvent += HandleNoteEvent;
    }

    private void OnDestroy()
    {
        if (midiManager != null) midiManager.OnNoteEvent -= HandleNoteEvent;
    }

    // ── Note Callback ─────────────────────────────────────────────────────────
    private void HandleNoteEvent(
        MidiRhythmManager.NoteType type,
        float noteTime,
        float nextNoteTime,
        int velocity)
    {
        if (!_configMap.TryGetValue(type, out NoteConfig cfg)) return;

        if (cfg.bulletPrefab == null)
        {
            Debug.LogWarning($"[BulletSpawner2D] No prefab assigned for NoteType '{type}'");
            return;
        }

        // Travel duration = gap until the next note arrives
        // Bullet leaves now and arrives exactly when the next note fires
        float travelDuration = nextNoteTime > 0f
            ? Mathf.Clamp(nextNoteTime - midiManager.SongTime, 0.1f, 5f)
            : 0.5f;

        foreach (Vector2 pos in GetSpawnPositions(cfg))
            SpawnBullet(pos, cfg.bulletPrefab, travelDuration, type, velocity);

        cfg._fireCount++;
    }

    // ── Spawn Position Logic ──────────────────────────────────────────────────
    private List<Vector2> GetSpawnPositions(NoteConfig cfg)
    {
        var list = new List<Vector2>();
        int n = cfg.bulletsPerNote;

        switch (cfg.pattern)
        {
            case SpawnPattern.Random:
                for (int i = 0; i < n; i++)
                    list.Add(PointOnRing(Random.Range(0f, 360f)));
                break;

            case SpawnPattern.Alternating:
                float alt = (cfg._fireCount % 2 == 0) ? 0f : 180f;
                for (int i = 0; i < n; i++)
                {
                    float spread = n > 1 ? (i / (float)(n - 1) - 0.5f) * 60f : 0f;
                    list.Add(PointOnRing(alt + spread));
                }
                break;

            case SpawnPattern.Spiral:
                for (int i = 0; i < n; i++)
                    list.Add(PointOnRing(cfg._angle + i * (360f / n)));
                cfg._angle += cfg.spiralStep;
                break;

            case SpawnPattern.Cardinal:
                float[] cards = { 0f, 90f, 180f, 270f };
                list.Add(PointOnRing(cards[cfg._fireCount % 4]));
                break;

            case SpawnPattern.Converging:
                float anchor = Random.Range(0f, 360f);
                for (int i = 0; i < n; i++)
                    list.Add(PointOnRing(anchor + Random.Range(-angleJitter, angleJitter)));
                break;

            case SpawnPattern.Ring:
                for (int i = 0; i < n; i++)
                    list.Add(PointOnRing(i * (360f / n)));
                break;
        }
        return list;
    }

    private Vector2 PointOnRing(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return (Vector2)playerTransform.position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * spawnRadius;
    }

    // ── Instantiation ─────────────────────────────────────────────────────────
    private void SpawnBullet(
        Vector2 pos, GameObject prefab, float travelDuration,
        MidiRhythmManager.NoteType type, int velocity)
    {
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        _activeBullets.Add(go);

        if (go.TryGetComponent<Bullet2D>(out var bullet))
            bullet.Initialize(playerTransform, travelDuration, type, velocity, this);
        else
        {
            Debug.LogError($"[BulletSpawner2D] '{prefab.name}' missing Bullet2D component!");
            Destroy(go);
        }
    }

    public void NotifyBulletDestroyed(GameObject b) => _activeBullets.Remove(b);

    // ── Gizmos ────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Vector3 prev = playerTransform.position + new Vector3(spawnRadius, 0f);
        for (int i = 1; i <= 60; i++)
        {
            float a  = i / 60f * 2f * Mathf.PI;
            Vector3 next = playerTransform.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * spawnRadius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
