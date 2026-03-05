using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Experiment logger.  Automatically starts recording when the progress bar
/// first rises above 0 and writes a timestamped log file when progress
/// reaches 1.  Tracks prompt events, user decisions, and hindered time.
/// </summary>
public class ExpLogging : MonoBehaviour
{
    [Header("Output")]
    [Tooltip("Sub-folder under Application.persistentDataPath for log files.")]
    [SerializeField] private string logFolder = "ExperimentLogs";

    // ── state ──────────────────────────────────────────────────────────
    private bool _loggingActive;
    private bool _loggingStarted;   // one-shot guard so we don't fire every frame

    private float _trialStartTime;
    private string _sessionMode;
    private DateTime _logDateTime;

    // ── prompt events ──────────────────────────────────────────────────
    private struct PromptEvent
    {
        public string type;           // "water" or "salt"
        public float shownTime;       // seconds since logging started
        public float reactionTime;    // seconds between shown and decision (-1 = pending)
        public bool accepted;
        public bool decided;
    }

    private readonly List<PromptEvent> _promptEvents = new List<PromptEvent>();

    // ── hindered (progress-paused) accumulator ─────────────────────────
    private float _totalHinderedTime;

    // ── per-item decision strings (0 = deny, 1 = accept) ──────────────
    private string _waterDecisions = "";
    private string _saltDecisions = "";
    private const string WaterCorrect = "0011";
    private const string SaltCorrect  = "0101";

    // ── public API ─────────────────────────────────────────────────────

    /// <summary>True while the logger is actively recording data.</summary>
    public bool IsLoggingActive => _loggingActive;

    /// <summary>
    /// Resets all internal state so a new trial can be tracked.
    /// Call this at the start of each trial (before progress begins).
    /// </summary>
    public void ResetForNewTrial()
    {
        _loggingActive  = false;
        _loggingStarted = false;
        _trialStartTime = 0f;
        _sessionMode    = "";
        _promptEvents.Clear();
        _totalHinderedTime = 0f;
        _waterDecisions = "";
        _saltDecisions  = "";
    }

    /// <summary>
    /// Call every frame with the current progress value.
    /// The first frame progress is &gt; 0, logging begins (one-shot).
    /// </summary>
    public void CheckProgressStart(float progress, string mode)
    {
        if (_loggingStarted) return;          // already triggered — skip
        if (progress <= 0f) return;           // still at zero — skip

        _loggingStarted = true;
        _loggingActive  = true;
        _trialStartTime = Time.time;
        _sessionMode    = mode;
        _logDateTime    = DateTime.Now;

        Debug.Log("[ExpLogging] Logging started — progress rose above 0.");
    }

    /// <summary>Record that a prompt of the given type was shown.</summary>
    public void OnPromptShown(string type)
    {
        if (!_loggingActive) return;

        _promptEvents.Add(new PromptEvent
        {
            type         = type,
            shownTime    = Time.time - _trialStartTime,
            reactionTime = -1f,
            accepted     = false,
            decided      = false
        });
    }

    /// <summary>Record that the user accepted or denied a prompt.</summary>
    public void OnPromptDecision(string type, bool accepted)
    {
        if (!_loggingActive) return;

        // Find the most recent undecided prompt of this type
        for (int i = _promptEvents.Count - 1; i >= 0; i--)
        {
            if (_promptEvents[i].type == type && !_promptEvents[i].decided)
            {
                var evt = _promptEvents[i];
                evt.reactionTime = (Time.time - _trialStartTime) - evt.shownTime;
                evt.accepted     = accepted;
                evt.decided      = true;
                _promptEvents[i] = evt;
                break;
            }
        }

        // Append to per-item decision string
        string bit = accepted ? "1" : "0";
        if (type == "water") _waterDecisions += bit;
        else if (type == "salt") _saltDecisions += bit;
    }

    /// <summary>
    /// Call every frame so the logger can accumulate hindered time.
    /// <paramref name="hindered"/> should be true when progress is paused.
    /// </summary>
    public void TrackHinderedTime(float dt, bool hindered)
    {
        if (!_loggingActive) return;
        if (hindered) _totalHinderedTime += dt;
    }

    /// <summary>
    /// Discard the current log without writing to disk.
    /// Use when the trial is restarted or the application is quit early.
    /// </summary>
    public void DiscardLog()
    {
        _loggingActive = false;
        _loggingStarted = false;
        Debug.Log("[ExpLogging] Log discarded — no file written.");
    }

    /// <summary>
    /// Finalize and write the log file.  Called when progress reaches 1
    /// (or whenever you want to flush the current data).
    /// </summary>
    public void EndLogging()
    {
        if (!_loggingActive) return;
        _loggingActive = false;

        float totalDuration = Time.time - _trialStartTime;
        WriteLogFile(totalDuration);
    }

    // ── file output ────────────────────────────────────────────────────

    private void WriteLogFile(float totalDuration)
    {
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, logFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Auto-name: log-MM-dd-HH-mm.txt
            string fileName = $"log-{_logDateTime:MM-dd-HH-mm}.txt";
            string path = Path.Combine(folder, fileName);

            var sb = new StringBuilder();

            // 1. Metadata
            sb.AppendLine("=== Experiment Log ===");
            sb.AppendLine($"Mode: {_sessionMode}");
            sb.AppendLine($"Date: {_logDateTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 2. Total duration (progress 0 → 1)
            sb.AppendLine($"Total Duration (progress 0 -> 1): {totalDuration:F3}s");
            sb.AppendLine();

            // 3. Prompt events array + total hindered time
            sb.AppendLine("--- Prompt Events ---");
            for (int i = 0; i < _promptEvents.Count; i++)
            {
                var evt = _promptEvents[i];
                string decision = evt.decided
                    ? $"{(evt.accepted ? "ACCEPTED" : "DENIED")} | decision time: {evt.reactionTime:F3}s"
                    : "NO DECISION";
                sb.AppendLine($"  [{i}] {evt.type,-5} | shown at: {evt.shownTime:F3}s | {decision}");
            }
            sb.AppendLine();
            sb.AppendLine($"Total Hindered Time: {_totalHinderedTime:F3}s");
            sb.AppendLine();

            // 4. Water decisions
            bool waterCorrect = _waterDecisions == WaterCorrect;
            sb.AppendLine($"Water Decisions: {_waterDecisions} (Expected: {WaterCorrect}) -> {(waterCorrect ? "CORRECT" : "INCORRECT")}");

            // 5. Salt decisions
            bool saltCorrect = _saltDecisions == SaltCorrect;
            sb.AppendLine($"Salt Decisions:  {_saltDecisions} (Expected: {SaltCorrect}) -> {(saltCorrect ? "CORRECT" : "INCORRECT")}");

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[ExpLogging] Log written to: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ExpLogging] Failed to write log: {e}");
        }
    }
}


