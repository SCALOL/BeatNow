using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Common;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class MidiRhythmManager : MonoBehaviour
{
    // ── NoteType Enum ─────────────────────────────────────────────────────────
    public enum NoteType { Normal, Heal, Critical, Ignored }

    // ── NoteMapping ───────────────────────────────────────────────────────────
    [System.Serializable]
    public class NoteMapping
    {
        [Tooltip("MIDI note name, e.g. C4  D#5  Bb3")]
        public string noteName = "C4";
        public NoteType noteType = NoteType.Normal;
        [HideInInspector] public int resolvedNoteNumber = -1;
    }

    // ── Public C# Event ───────────────────────────────────────────────────────
    public event System.Action<NoteType, float, float, int> OnNoteEvent;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("MIDI Source  (assign ONE)")]
    public TextAsset midiFileAsset;
    public string midiStreamingAssetsPath = "";

    [Header("Audio")]
    public AudioSource SFXaudiosource;
    public AudioClip CountDownSound;
    public AudioClip MusicAudioClip;

    [Header("Countdown Settings")]
    [Tooltip("Countdown seconds shown at game START before music plays.")]
    public int leadInTime = 3;
    [Tooltip("Countdown seconds shown when RESUMING from pause.")]
    public int resumeCountdownTime = 3;

    [Header("Note Mappings")]
    public List<NoteMapping> noteMappings = new List<NoteMapping>
    {
        new NoteMapping { noteName = "C3", noteType = NoteType.Normal   },
        new NoteMapping { noteName = "C4", noteType = NoteType.Critical },
        new NoteMapping { noteName = "C5", noteType = NoteType.Heal     },
    };

    [Header("UI")]
    public UIManager UIManager;
    public UnityEvent<float> UpdateSongProgress;

    [Header("Song End")]
    public UnityEvent OnSongFinished;

    [Header("Pause / Resume Events")]
    [Tooltip("Fired the moment the game pauses.")]
    public UnityEvent OnGamePaused;
    [Tooltip("Fired every second during resume countdown. Passes number shown (3,2,1,0).")]
    public UnityEvent<int> OnResumeCountdownTick;
    [Tooltip("Fired when countdown ends and gameplay fully resumes.")]
    public UnityEvent OnGameResumed;

    [Header("Debug")]
    public bool showNoteLog = true;

    // ── Public State ──────────────────────────────────────────────────────────
    public bool IsReady { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsResuming { get; private set; }
    public float SongTime => _audioSource != null ? _audioSource.time : 0f;
    public float currentProgress;

    // ── Scheduled Note ────────────────────────────────────────────────────────
    [System.Serializable]
    public struct ScheduledNote
    {
        public float timeSeconds;
        public NoteType type;
        public int velocity;
        public string noteName;
        public int noteNumber;
    }

    public List<ScheduledNote> Schedule { get; private set; } = new List<ScheduledNote>();

    // ── Private ───────────────────────────────────────────────────────────────
    private AudioSource _audioSource;
    private int _nextIndex;
    private Dictionary<int, NoteType> _noteMap = new Dictionary<int, NoteType>();
    private bool _hasFinishedCalled = false;
    private Coroutine _resumeCoroutine = null;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        
        UIManager.ActivateCountdownUI();

        if (MusicAudioClip == null)
        {
            Debug.LogError("[MidiRhythmManager] MusicAudioClip not assigned!");
            return;
        }

        BuildNoteMap();

        MidiFile midiFile = LoadMidiFile();
        if (midiFile == null) return;

        ParseSchedule(midiFile);

        IsReady = false;
        StartCoroutine(PlayWithLeadIn());
    }

    private void Update()
    {
        // Block firing while not ready, paused, or mid-resume countdown
        if (!IsReady || IsPaused || IsResuming) return;
        if (!_audioSource.isPlaying) return;
        FireDueNotes();
    }

    // ── MIDI Loading ──────────────────────────────────────────────────────────
    private MidiFile LoadMidiFile()
    {
        if (midiFileAsset != null)
        {
            try
            {
                using (var stream = new MemoryStream(midiFileAsset.bytes))
                    return MidiFile.Read(stream);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MidiRhythmManager] Failed to parse TextAsset MIDI: {e.Message}");
                return null;
            }
        }

        if (!string.IsNullOrEmpty(midiStreamingAssetsPath))
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, midiStreamingAssetsPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MidiRhythmManager] MIDI file not found at: {fullPath}");
                return null;
            }
            try { return MidiFile.Read(fullPath); }
            catch (System.Exception e)
            {
                Debug.LogError($"[MidiRhythmManager] StreamingAssets MIDI error: {e.Message}");
                return null;
            }
        }

        Debug.LogError("[MidiRhythmManager] No MIDI source assigned!");
        return null;
    }

    // ── Schedule Parsing ──────────────────────────────────────────────────────
    private void ParseSchedule(MidiFile midiFile)
    {
        TempoMap tempoMap = midiFile.GetTempoMap();
        var allNotes = midiFile.GetNotes();

        Schedule.Clear();

        foreach (var note in allNotes)
        {
            int noteNum = (byte)note.NoteNumber;
            if (!_noteMap.TryGetValue(noteNum, out NoteType type)) continue;
            if (type == NoteType.Ignored) continue;

            float timeSec = (float)note.TimeAs<MetricTimeSpan>(tempoMap).TotalSeconds;
            int velocity = (byte)note.Velocity;
            string dispName = $"{note.NoteName}{note.Octave}";

            Schedule.Add(new ScheduledNote
            {
                timeSeconds = timeSec,
                type = type,
                velocity = velocity,
                noteName = dispName,
                noteNumber = noteNum
            });
        }

        Schedule.Sort((a, b) => a.timeSeconds.CompareTo(b.timeSeconds));
        Debug.Log($"[MidiRhythmManager] Parsed {Schedule.Count} mapped notes.");

        if (showNoteLog)
            foreach (var n in Schedule)
                Debug.Log($"  {n.noteName} (#{n.noteNumber}) → {n.type} @ {n.timeSeconds:F3}s  vel={n.velocity}");
    }

    // ── Note Map ──────────────────────────────────────────────────────────────
    private void BuildNoteMap()
    {
        _noteMap.Clear();
        foreach (var m in noteMappings)
        {
            int num = ParseNoteName(m.noteName);
            if (num < 0) { Debug.LogWarning($"[MidiRhythmManager] Bad note name: '{m.noteName}'"); continue; }
            m.resolvedNoteNumber = num;
            _noteMap[num] = m.noteType;
            if (showNoteLog) Debug.Log($"[MidiRhythmManager] Mapped #{num} ({m.noteName}) → {m.noteType}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SHARED COUNTDOWN HELPER
    // Counts down from `seconds` to 1 (each tick = 1 real second),
    // plays the countdown SFX, updates UIManager, and optionally fires a
    // UnityEvent<int> so external UI can react to each tick.
    // Uses WaitForSecondsRealtime so it works even when Time.timeScale == 0.
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator RunCountdown(int seconds, UnityEvent<int> tickEvent)
    {
        for (int t = seconds; t >= 1; t--)
        {
            tickEvent?.Invoke(t);
            SFXaudiosource.PlayOneShot(CountDownSound);
            yield return new WaitForSecondsRealtime(1f);
        }

        // Final "GO" / "0" tick
        tickEvent?.Invoke(0);
        SFXaudiosource.PlayOneShot(CountDownSound);
        yield return new WaitForSecondsRealtime(0.5f);
    }

    // ── Initial Lead-in (game start) ──────────────────────────────────────────
    private IEnumerator PlayWithLeadIn()
    {

        // Start countdown — no external tick event needed for the lead-in
        yield return StartCoroutine(RunCountdown(leadInTime, OnResumeCountdownTick));

        // Fade out countdown panel
        StartCoroutine(UIManager.FadeOutAndLoadScene());
        yield return new WaitForSecondsRealtime(UIManager.fadeDuration);

        _audioSource.clip = MusicAudioClip;
        _audioSource.Play();
        IsReady = true;

        StartCoroutine(TrackProgressRoutine());
    }

    // ── Note Firing ───────────────────────────────────────────────────────────
    private void FireDueNotes()
    {
        while (_nextIndex < Schedule.Count &&
               _audioSource.time >= Schedule[_nextIndex].timeSeconds)
        {
            ScheduledNote note = Schedule[_nextIndex];
            float nextT = GetNextNoteTimeAfter(note.timeSeconds);

            OnNoteEvent?.Invoke(note.type, note.timeSeconds, nextT, note.velocity);

            if (showNoteLog)
                Debug.Log($"[MidiRhythmManager] ▶ {note.noteName} → {note.type}  vel={note.velocity}  @{note.timeSeconds:F3}s");

            _nextIndex++;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PAUSE
    // Freezes Time.timeScale so WaitForSeconds coroutines also stop.
    // Uses AudioSource.Pause() which preserves the playhead position.
    // ─────────────────────────────────────────────────────────────────────────
    public void PauseGame()
    {
        if (!IsReady || IsPaused) return;

        // If a resume countdown is running, cancel it cleanly
        if (_resumeCoroutine != null)
        {
            StopCoroutine(_resumeCoroutine);
            _resumeCoroutine = null;
            IsResuming = false;
        }

        // Pause audio — keeps the playhead exactly where it is
        _audioSource.Pause();

        // Freeze game time (stops physics, animations, WaitForSeconds coroutines)
        Time.timeScale = 0f;

        IsPaused = true;
        Debug.Log($"[MidiRhythmManager] PAUSED at {_audioSource.time:F3}s  (note index {_nextIndex})");

    }

    // ─────────────────────────────────────────────────────────────────────────
    // RESUME
    // Restores Time.timeScale FIRST so the countdown coroutine can run,
    // then shows the countdown, then unpauses audio.
    // ─────────────────────────────────────────────────────────────────────────
    public void ResumeGame()
    {
        // Guard: only resume if paused and not already counting down
        if (!IsReady || !IsPaused || IsResuming) return;

        // Restore time-scale NOW so WaitForSecondsRealtime works in the coroutine
        // (we use WaitForSecondsRealtime in RunCountdown, so timeScale doesn't matter,
        //  but we restore it here so other systems like animations also resume visually)


        _resumeCoroutine = StartCoroutine(ResumeWithCountdown());
    }

    private IEnumerator ResumeWithCountdown()
    {
        IsResuming = true;

        // Show countdown UI (same panel as the lead-in)
        UIManager.ActivateCountdownUI();

        // Countdown — fires OnResumeCountdownTick each tick so the UI can
        // display "3 … 2 … 1 … GO" with different colors/animations if desired
        yield return StartCoroutine(RunCountdown(resumeCountdownTime, OnResumeCountdownTick));

        // Fade out countdown panel
        StartCoroutine(UIManager.FadeOutAndLoadScene());
        yield return new WaitForSecondsRealtime(UIManager.fadeDuration);

        // ── ACTUALLY RESUME ──
        // UnPause picks up exactly from where Pause left off
        _audioSource.UnPause();

        IsPaused = false;
        IsResuming = false;
        _resumeCoroutine = null;
        Time.timeScale = 1f;
        Debug.Log($"[MidiRhythmManager] RESUMED at {_audioSource.time:F3}s  (note index {_nextIndex})");
        OnGameResumed?.Invoke();
    }

    // ── Toggle (single pause button) ─────────────────────────────────────────
    /// <summary>Call this from a Pause/Resume button — handles both directions.</summary>
    public void TogglePause()
    {
        if (!IsPaused) PauseGame();
        else ResumeGame();
        OnGamePaused?.Invoke();
    }

    // ── Progress Tracking ─────────────────────────────────────────────────────
    private IEnumerator TrackProgressRoutine()
    {
        while (true)
        {
            if (IsReady)
            {
                if (_audioSource.isPlaying)
                {
                    currentProgress = GetSongProgress();
                    _hasFinishedCalled = false;
                }
                else if (!_audioSource.isPlaying && _audioSource.time == 0f
                         && !_hasFinishedCalled && !IsPaused)
                {
                    HandleSongEnd();
                }
                else if (_audioSource.clip != null
                         && _audioSource.time >= _audioSource.clip.length - 0.1f
                         && !_hasFinishedCalled)
                {
                    HandleSongEnd();
                }
            }

            UpdateSongProgress?.Invoke(currentProgress);
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void HandleSongEnd()
    {
        _hasFinishedCalled = true;
        Debug.Log("[MidiRhythmManager] Song Finished!");
        OnSongFinished?.Invoke();
    }

    // ── Public Helpers ────────────────────────────────────────────────────────
    public float GetSongProgress()
    {
        if (_audioSource == null || _audioSource.clip == null) return 0f;
        return Mathf.Clamp((_audioSource.time / _audioSource.clip.length) * 100f, 0f, 100f);
    }

    public float GetNextNoteTimeAfter(float fromTime)
    {
        foreach (var n in Schedule)
            if (n.timeSeconds > fromTime) return n.timeSeconds;
        return -1f;
    }

    public void RestartSong()
    {
        _nextIndex = 0;
        _hasFinishedCalled = false;
        IsPaused = false;
        IsResuming = false;
        Time.timeScale = 1f;

        if (_resumeCoroutine != null)
        {
            StopCoroutine(_resumeCoroutine);
            _resumeCoroutine = null;
        }

        _audioSource.Stop();
        _audioSource.time = 0f;
        _audioSource.Play();
    }

    // ── Note Name Parser ──────────────────────────────────────────────────────
    public static int ParseNoteName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;
        name = name.Trim();

        int splitIndex = 1;
        if (name.Length > 1 && (name[1] == '#' || name[1] == 'b' || name[1] == 'B'))
            splitIndex = 2;

        string pitchStr = name.Substring(0, splitIndex).ToUpper();
        string octaveStr = name.Substring(splitIndex);
        if (!int.TryParse(octaveStr, out int octave)) return -1;

        int semitone;
        switch (pitchStr)
        {
            case "C": semitone = 0; break;
            case "C#": case "DB": semitone = 1; break;
            case "D": semitone = 2; break;
            case "D#": case "EB": semitone = 3; break;
            case "E": semitone = 4; break;
            case "F": semitone = 5; break;
            case "F#": case "GB": semitone = 6; break;
            case "G": semitone = 7; break;
            case "G#": case "AB": semitone = 8; break;
            case "A": semitone = 9; break;
            case "A#": case "BB": semitone = 10; break;
            case "B": semitone = 11; break;
            default: return -1;
        }

        int noteNumber = (octave + 1) * 12 + semitone;
        return (noteNumber >= 0 && noteNumber <= 127) ? noteNumber : -1;
    }
}