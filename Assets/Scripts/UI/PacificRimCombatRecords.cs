using UnityEngine;
using UnityEngine.SceneManagement;

namespace PacificRimUI
{
    /// <summary>
    /// Persistent highscore / speedrun records manager for Pacific Rim encounters.
    /// Stores and retrieves best combat completion time locally using PlayerPrefs per level.
    /// Formatted in minutes and seconds (e.g. "01:45").
    /// </summary>
    public static class PacificRimCombatRecords
    {
        public const string BestTimeKey = "PacificRim_BestCombatTime_Seconds";
        public const string TotalVictoriesKey = "PacificRim_TotalVictories";

        private static string GetBestTimeKeyForLevel(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                levelName = SceneManager.GetActiveScene().name;

            if (string.IsNullOrEmpty(levelName) ||
                levelName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase) ||
                levelName.Equals("Save Striker Eureka", System.StringComparison.OrdinalIgnoreCase))
            {
                return BestTimeKey;
            }

            return $"PacificRim_BestCombatTime_{levelName}";
        }

        /// <summary>
        /// Returns the fastest time recorded in seconds for the given or active level, or -1f if no record exists yet.
        /// </summary>
        public static float GetBestTime(string levelName = null)
        {
            return PlayerPrefs.GetFloat(GetBestTimeKeyForLevel(levelName), -1f);
        }

        /// <summary>
        /// Checks if the recorded time is valid.
        /// </summary>
        public static bool HasBestTime(string levelName = null)
        {
            return GetBestTime(levelName) > 0f;
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
        /// Attempts to register a new combat finish time for the given or active level.
        /// Returns true if this is a new best time (lowest time to defeat Kaiju).
        /// </summary>
        public static bool TryRecordVictoryTime(float finishTimeInSeconds, out bool isNewBestRecord, string levelName = null)
        {
            if (finishTimeInSeconds <= 0.1f)
            {
                isNewBestRecord = false;
                return false;
            }

            int victories = PlayerPrefs.GetInt(TotalVictoriesKey, 0) + 1;
            PlayerPrefs.SetInt(TotalVictoriesKey, victories);

            string key = GetBestTimeKeyForLevel(levelName);
            float currentBest = PlayerPrefs.GetFloat(key, -1f);
            if (currentBest <= 0f || finishTimeInSeconds < currentBest)
            {
                PlayerPrefs.SetFloat(key, finishTimeInSeconds);
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
        public static void ResetBestTime(string levelName = null)
        {
            PlayerPrefs.DeleteKey(GetBestTimeKeyForLevel(levelName));
            PlayerPrefs.Save();
        }
    }
}
