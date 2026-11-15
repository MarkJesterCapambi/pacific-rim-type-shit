using UnityEngine;

namespace PacificRimUI
{
    /// <summary>
    /// Persistent highscore / speedrun records manager for Pacific Rim encounters.
    /// Stores and retrieves best combat completion time locally using PlayerPrefs.
    /// Formatted in minutes and seconds (e.g. "01:45").
    /// </summary>
    public static class PacificRimCombatRecords
    {
        public const string BestTimeKey = "PacificRim_BestCombatTime_Seconds";
        public const string TotalVictoriesKey = "PacificRim_TotalVictories";

        /// <summary>
        /// Returns the fastest time recorded in seconds, or -1f if no record exists yet.
        /// </summary>
        public static float GetBestTime()
        {
            return PlayerPrefs.GetFloat(BestTimeKey, -1f);
        }

        /// <summary>
        /// Checks if the recorded time is valid.
        /// </summary>
        public static bool HasBestTime()
        {
            return GetBestTime() > 0f;
        }

        /// <summary>
        /// Formats seconds into "MM:SS" (e.g. 105.4s -> "01:45").
        /// </summary>
        public static string FormatTime(float timeInSeconds)
        {
            if (timeInSeconds < 0f) return "--:--";

            int totalSeconds = Mathf.FloorToInt(timeInSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:D2}:{seconds:D2}";
        }

        /// <summary>
        /// Formats seconds into "MM:SS.ff" for higher precision if desired.
        /// </summary>
        public static string FormatTimeDetailed(float timeInSeconds)
        {
            if (timeInSeconds < 0f) return "--:--";

            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
            int fraction = Mathf.FloorToInt((timeInSeconds * 100f) % 100f);
            return $"{minutes:D2}:{seconds:D2}.{fraction:D2}";
        }

        /// <summary>
        /// Attempts to register a new combat finish time.
        /// Returns true if this is a new best time (lowest time to defeat Kaiju).
        /// </summary>
        public static bool TryRecordVictoryTime(float finishTimeInSeconds, out bool isNewBestRecord)
        {
            if (finishTimeInSeconds <= 0.1f)
            {
                isNewBestRecord = false;
                return false;
            }

            int victories = PlayerPrefs.GetInt(TotalVictoriesKey, 0) + 1;
            PlayerPrefs.SetInt(TotalVictoriesKey, victories);

            float currentBest = GetBestTime();
            if (currentBest <= 0f || finishTimeInSeconds < currentBest)
            {
                PlayerPrefs.SetFloat(BestTimeKey, finishTimeInSeconds);
                PlayerPrefs.Save();
                isNewBestRecord = true;
                return true;
            }

            isNewBestRecord = false;
            PlayerPrefs.Save();
            return false;
        }

        /// <summary>
        /// Clears the local best time (useful for testing or reset options).
        /// </summary>
        public static void ResetBestTime()
        {
            PlayerPrefs.DeleteKey(BestTimeKey);
            PlayerPrefs.Save();
        }
    }
}
