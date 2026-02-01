using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Trial logger for Experiment1 (Wizard-of-Oz compatible).
/// Writes one CSV row per trial to Application.persistentDataPath.
/// </summary>
public class ExpLogging : MonoBehaviour
{
    [Header("Metadata")]
    [Tooltip("Optional participant ID to include in the CSV log (can be blank).")]
    [SerializeField] private string participantId = "";

    [Tooltip("Which of the 3 experimental conditions this run corresponds to (manual/request/auto).")]
    [SerializeField] private Exp1Type sessionType = Exp1Type.Manual;

    [Header("Output")]
    [Tooltip("CSV file name written under Application.persistentDataPath.")]
    [SerializeField] private string logFileName = "trial_log.csv";

    [Header("Counting")]
    [Tooltip("Seconds without water/salt effect before counting a new 'episode' (distinct intervention).")]
    [SerializeField] private float interventionGapSeconds = 0.75f;

    // Trial lifecycle
    public bool IsTrialActive { get; private set; }
    private float _trialStartTime;
    private float _trialEndTime;

    // Timers
    private float _waterBlockedTime;
    private float _saltBlockedTime;
    private float _anyBlockedTime;
    private float _onStoveTime;
    private float _stirringTime;
    private float _cookingActiveTime; // on stove + stirring + not blocked
    private float _panHeldTime;

    // Counts
    private int _panGrabCount;
    private int _panDisengageCount;

    // Peaks
    private float _peakBurnt;
    private float _peakSalt;

    // Intervention episodes
    private int _waterEpisodes;
    private int _saltEpisodes;
    private float _lastWaterEffectTime = -999f;
    private float _lastSaltEffectTime = -999f;
    private bool _waterEpisodeOngoing;
    private bool _saltEpisodeOngoing;

    // Previous states
    private bool _prevPanGrabbed;

    public void StartTrial(bool initialPanGrabbed)
    {
        IsTrialActive = true;
        _trialStartTime = Time.time;
        _trialEndTime = 0f;

        _waterBlockedTime = 0f;
        _saltBlockedTime = 0f;
        _anyBlockedTime = 0f;
        _onStoveTime = 0f;
        _stirringTime = 0f;
        _cookingActiveTime = 0f;
        _panHeldTime = 0f;

        _panGrabCount = 0;
        _panDisengageCount = 0;
        _peakBurnt = 0f;
        _peakSalt = 0f;

        _waterEpisodes = 0;
        _saltEpisodes = 0;
        _lastWaterEffectTime = -999f;
        _lastSaltEffectTime = -999f;
        _waterEpisodeOngoing = false;
        _saltEpisodeOngoing = false;

        _prevPanGrabbed = initialPanGrabbed;
    }

    /// <summary>
    /// Per-frame update of metrics while a trial is active.
    /// </summary>
    public void Track(
        float dt,
        float burntAmount,
        float saltAmount,
        bool progressHold,
        bool onStove,
        bool stirring,
        bool panGrabbed)
    {
        if (!IsTrialActive) return;

        // Block timers: burn/salt reaching 1.0 do NOT fail the trial; they only block progress growth.
        bool waterBlocking = burntAmount >= 1f;
        bool saltBlocking = saltAmount >= 1f;
        if (waterBlocking) _waterBlockedTime += dt;
        if (saltBlocking) _saltBlockedTime += dt;
        if (waterBlocking || saltBlocking) _anyBlockedTime += dt;

        _peakBurnt = Mathf.Max(_peakBurnt, burntAmount);
        _peakSalt = Mathf.Max(_peakSalt, saltAmount);

        if (onStove) _onStoveTime += dt;
        if (stirring) _stirringTime += dt;
        if (panGrabbed) _panHeldTime += dt;
        if (onStove && stirring && !progressHold) _cookingActiveTime += dt;

        // Engagement transitions
        if (panGrabbed && !_prevPanGrabbed) _panGrabCount++;
        if (!panGrabbed && _prevPanGrabbed) _panDisengageCount++;
        _prevPanGrabbed = panGrabbed;

        // Episode termination by inactivity gap
        if (_waterEpisodeOngoing && (Time.time - _lastWaterEffectTime) > interventionGapSeconds)
        {
            _waterEpisodeOngoing = false;
        }
        if (_saltEpisodeOngoing && (Time.time - _lastSaltEffectTime) > interventionGapSeconds)
        {
            _saltEpisodeOngoing = false;
        }
    }

    public void NotifyWaterEffect()
    {
        if (!IsTrialActive) return;
        _lastWaterEffectTime = Time.time;
        if (!_waterEpisodeOngoing)
        {
            _waterEpisodeOngoing = true;
            _waterEpisodes++;
        }
    }

    public void NotifySaltEffect()
    {
        if (!IsTrialActive) return;
        _lastSaltEffectTime = Time.time;
        if (!_saltEpisodeOngoing)
        {
            _saltEpisodeOngoing = true;
            _saltEpisodes++;
        }
    }

    public void EndTrial(bool success, string endReason, float finalBurnt, float finalSalt, float finalProgress)
    {
        if (!IsTrialActive) return;

        _trialEndTime = Time.time;
        IsTrialActive = false;

        WriteTrialLogRow(success, endReason, finalBurnt, finalSalt, finalProgress);
    }

    private void WriteTrialLogRow(bool success, string endReason, float finalBurnt, float finalSalt, float finalProgress)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, logFileName);
            bool needsHeader = !File.Exists(path);

            var sb = new StringBuilder(512);
            if (needsHeader)
            {
                sb.AppendLine(string.Join(",",
                    "timestamp_iso",
                    "participant_id",
                    "session_type",
                    "end_reason",
                    "success",
                    "trial_duration_s",
                    "water_episodes",
                    "salt_episodes",
                    "water_blocked_s",
                    "salt_blocked_s",
                    "any_blocked_s",
                    "on_stove_s",
                    "stirring_s",
                    "cooking_active_s",
                    "pan_grab_count",
                    "pan_disengage_count",
                    "pan_held_s",
                    "peak_burnt",
                    "peak_salt",
                    "final_burnt",
                    "final_salt",
                    "final_progress"
                ));
            }

            double duration = Math.Max(0.0, _trialEndTime - _trialStartTime);
            sb.AppendLine(string.Join(",",
                Quote(DateTime.UtcNow.ToString("o")),
                Quote(participantId),
                Quote(sessionType.ToString()),
                Quote(endReason),
                success ? "1" : "0",
                duration.ToString("F3"),
                _waterEpisodes.ToString(),
                _saltEpisodes.ToString(),
                _waterBlockedTime.ToString("F3"),
                _saltBlockedTime.ToString("F3"),
                _anyBlockedTime.ToString("F3"),
                _onStoveTime.ToString("F3"),
                _stirringTime.ToString("F3"),
                _cookingActiveTime.ToString("F3"),
                _panGrabCount.ToString(),
                _panDisengageCount.ToString(),
                _panHeldTime.ToString("F3"),
                _peakBurnt.ToString("F3"),
                _peakSalt.ToString("F3"),
                finalBurnt.ToString("F3"),
                finalSalt.ToString("F3"),
                finalProgress.ToString("F3")
            ));

            File.AppendAllText(path, sb.ToString());
            Debug.Log($"[ExpLogging] Wrote trial log row to: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ExpLogging] Failed to write trial log: {e}");
        }
    }

    private static string Quote(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}


