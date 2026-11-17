using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PacificRimCutscene;
using PacificRimWater;

namespace PacificRimAudio
{
    /// <summary>
    /// Manages dynamic music playback with sample-accurate DSP queueing for boss phase progressions.
    /// - Main Menu: loops MainMenuMusic.
    /// - Deployment Cutscene: loops LoopableNoSuspense during approach, crossfades to
    ///   LoopableEnteringSuspense on foghorn/turnaround, then crossfades into Combat.
    /// - Combat Phases 1-3: strictly queues Intro -> Loopable per phase without skipping or
    ///   cutting off a currently playing loop mid-phrase.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class PacificRimMusicManager : MonoBehaviour
    {
        public static PacificRimMusicManager Instance { get; private set; }

        private enum MusicState
        {
            None,
            MainMenu,
            CutsceneNoSuspense,
            CutsceneEnteringSuspense,
            Combat,
            FadedOut
        }

        private struct QueuedTrack
        {
            public AudioClip clip;
            public bool loop;
            public int phase;
        }

        private const string MusicVolumePrefKey = "PR_MusicVolume";
        private const float DefaultMusicVolume = 0.85f;

        [Header("Volume & Transitions")]
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = DefaultMusicVolume;
        [SerializeField] private float crossfadeDuration = 1.25f;
        [SerializeField] private float outcomeFadeOutDuration = 2.5f;
        [SerializeField] private bool bypassListenerEffects = true;

        public float MusicVolume => musicVolume;

        [Header("Optional Clip Overrides (Auto-Resolved from PacificRimAudioManager if Empty)")]
        [SerializeField] private AudioClip mainMenuClip;
        [SerializeField] private AudioClip noSuspenseClip;
        [SerializeField] private AudioClip enteringSuspenseClip;
        [SerializeField] private AudioClip phase1IntroClip;
        [SerializeField] private AudioClip phase1LoopClip;
        [SerializeField] private AudioClip phase2IntroClip;
        [SerializeField] private AudioClip phase2LoopClip;
        [SerializeField] private AudioClip phase3IntroClip;
        [SerializeField] private AudioClip phase3LoopClip;

        private AudioSource deckA;
        private AudioSource deckB;
        private AudioSource activeDeck;
        private AudioSource standbyDeck;

        // Sample-accurate DSP scheduling state for active progression queue
        private readonly Queue<QueuedTrack> progressionQueue = new Queue<QueuedTrack>();
        private double activeStartDspTime;
        private double scheduledStartDspTime;
        private bool hasScheduledNext;
        private int highestQueuedPhase;

        // Crossfade state
        private bool isCrossfading;
        private float crossfadeElapsed;
        private float crossfadeDurationActive;
        private float fadeOutStartVolume;
        private bool fadingToSilence;

        private MusicState currentState = MusicState.None;
        private bool clipsResolved;

        // Cached scene gameplay references
        private KaijuAI cachedKaiju;
        private SimpleJaegerController cachedJaeger;
        private PacificRimDeploymentCutscene cachedCutscene;
        private OperationPitfallIntroCutscene cachedPitfallCutscene;
        private float nextRefSearchTime;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, DefaultMusicVolume));
            InitializeDecks();
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicVolumePrefKey, musicVolume);
            PlayerPrefs.Save();

            if (!isCrossfading && currentState != MusicState.FadedOut)
            {
                if (activeDeck != null && activeDeck.isPlaying)
                {
                    activeDeck.volume = musicVolume;
                }
                if (hasScheduledNext && standbyDeck != null)
                {
                    standbyDeck.volume = musicVolume;
                }
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            ResolveClips();
            EvaluateSceneState(SceneManager.GetActiveScene().name, forceRefresh: true);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            cachedKaiju = null;
            cachedJaeger = null;
            cachedCutscene = null;
            cachedPitfallCutscene = null;
            nextRefSearchTime = 0f;

            ResolveClips();
            EvaluateSceneState(scene.name, forceRefresh: true);
        }

        private void InitializeDecks()
        {
            if (deckA != null && deckB != null) return;

            deckA = CreateMusicDeck("MusicDeckA");
            deckB = CreateMusicDeck("MusicDeckB");
            activeDeck = deckA;
            standbyDeck = deckB;
        }

        private AudioSource CreateMusicDeck(string deckName)
        {
            var go = new GameObject(deckName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = false;
            src.volume = 0f;
            src.ignoreListenerPause = true;
            src.bypassListenerEffects = bypassListenerEffects;
            return src;
        }

        private void ResolveClips()
        {
            var audioMgr = PacificRimAudioManager.Instance;
            if (audioMgr == null) return;

            if (mainMenuClip == null) mainMenuClip = audioMgr.GetClip("MainMenuMusic");
            if (noSuspenseClip == null) noSuspenseClip = audioMgr.GetClip("LoopableNoSuspense");
            if (enteringSuspenseClip == null) enteringSuspenseClip = audioMgr.GetClip("LoopableEnteringSuspense");

            if (phase1IntroClip == null) phase1IntroClip = audioMgr.GetClip("Phase1IntroMusic");
            if (phase1LoopClip == null) phase1LoopClip = audioMgr.GetClip("Phase1LoopableMusic");
            if (phase2IntroClip == null) phase2IntroClip = audioMgr.GetClip("Phase2IntroMusic");
            if (phase2LoopClip == null) phase2LoopClip = audioMgr.GetClip("Phase2LoopableMusic");
            if (phase3IntroClip == null) phase3IntroClip = audioMgr.GetClip("Phase3IntroMusic");
            if (phase3LoopClip == null) phase3LoopClip = audioMgr.GetClip("Phase3LoopableMusic");

            PreloadClip(mainMenuClip);
            PreloadClip(noSuspenseClip);
            PreloadClip(enteringSuspenseClip);
            PreloadClip(phase1IntroClip);
            PreloadClip(phase1LoopClip);
            PreloadClip(phase2IntroClip);
            PreloadClip(phase2LoopClip);
            PreloadClip(phase3IntroClip);
            PreloadClip(phase3LoopClip);

            clipsResolved = true;
        }

        private static void PreloadClip(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
        }

        private void Update()
        {
            if (!clipsResolved) ResolveClips();

            UpdateCrossfade();
            UpdateProgressionQueue();
            EvaluateGameplayState();
        }

        private void EvaluateSceneState(string sceneName, bool forceRefresh)
        {
            bool isMenu = !string.IsNullOrEmpty(sceneName) &&
                          sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isMenu)
            {
                if (forceRefresh || currentState != MusicState.MainMenu)
                {
                    TransitionToLoopingState(MusicState.MainMenu, mainMenuClip, crossfadeDuration);
                }
                return;
            }

            if (forceRefresh)
            {
                currentState = MusicState.None;
                RefreshSceneReferences();
            }

            EvaluateGameplayState();
        }

        private void EvaluateGameplayState()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(sceneName) &&
                sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            if (Time.unscaledTime >= nextRefSearchTime &&
                (cachedKaiju == null || cachedJaeger == null || (cachedCutscene == null && cachedPitfallCutscene == null)))
            {
                RefreshSceneReferences();
                nextRefSearchTime = Time.unscaledTime + 0.5f;
            }

            // 1. Check Cutscene State (Level 1 Deployment or Level 2 Operation Pitfall Intro)
            if (cachedCutscene != null && cachedCutscene.isCutsceneRunning)
            {
                if (cachedCutscene.HasTriggeredFoghorn)
                {
                    if (currentState != MusicState.CutsceneEnteringSuspense)
                    {
                        AudioClip clip = enteringSuspenseClip != null ? enteringSuspenseClip : noSuspenseClip;
                        TransitionToLoopingState(MusicState.CutsceneEnteringSuspense, clip, crossfadeDuration);
                    }
                }
                else if (currentState != MusicState.CutsceneNoSuspense)
                {
                    AudioClip clip = noSuspenseClip != null ? noSuspenseClip : enteringSuspenseClip;
                    TransitionToLoopingState(MusicState.CutsceneNoSuspense, clip, crossfadeDuration);
                }
                return;
            }

            if (cachedPitfallCutscene != null && cachedPitfallCutscene.isCutsceneRunning)
            {
                if (cachedPitfallCutscene.IsInDeepDescentPhase)
                {
                    if (currentState != MusicState.CutsceneEnteringSuspense)
                    {
                        AudioClip clip = enteringSuspenseClip != null ? enteringSuspenseClip : noSuspenseClip;
                        TransitionToLoopingState(MusicState.CutsceneEnteringSuspense, clip, crossfadeDuration);
                    }
                }
                else if (currentState != MusicState.CutsceneNoSuspense)
                {
                    AudioClip clip = noSuspenseClip != null ? noSuspenseClip : enteringSuspenseClip;
                    TransitionToLoopingState(MusicState.CutsceneNoSuspense, clip, crossfadeDuration);
                }
                return;
            }

            // 2. Check Outcome (Boss Dead or Player Dead) -> Fade out gently
            bool bossDead = cachedKaiju != null && cachedKaiju.IsDead;
            bool playerDead = cachedJaeger != null && cachedJaeger.currentHealth <= 0f;
            if (bossDead || playerDead)
            {
                if (currentState != MusicState.FadedOut)
                {
                    FadeOutMusic(outcomeFadeOutDuration);
                }
                return;
            }

            // 3. Active Combat State -> Phase 1-3 Strict Queueing
            if (currentState != MusicState.Combat)
            {
                StartCombatProgression(cachedKaiju != null ? cachedKaiju.CurrentPhase : 1);
            }
            else if (cachedKaiju != null && cachedKaiju.CurrentPhase > highestQueuedPhase)
            {
                EnqueueUpToPhase(cachedKaiju.CurrentPhase);
            }
        }

        private void RefreshSceneReferences()
        {
            if (cachedCutscene == null) cachedCutscene = PacificRimDeploymentCutscene.Instance;
            if (cachedPitfallCutscene == null) cachedPitfallCutscene = OperationPitfallIntroCutscene.Instance;
            if (cachedJaeger == null) cachedJaeger = SimpleJaegerController.Instance;
            if (cachedKaiju == null) cachedKaiju = FindAnyObjectByType<KaijuAI>();
        }

        private void TransitionToLoopingState(MusicState newState, AudioClip clip, float fadeDuration)
        {
            currentState = newState;
            ClearProgressionQueue();

            if (clip == null) return;
            if (activeDeck.isPlaying && activeDeck.clip == clip && activeDeck.loop) return;

            CrossfadeImmediate(clip, loop: true, fadeDuration);
        }

        private void FadeOutMusic(float fadeDuration)
        {
            currentState = MusicState.FadedOut;
            ClearProgressionQueue();

            if (!activeDeck.isPlaying && !standbyDeck.isPlaying) return;

            fadingToSilence = true;
            isCrossfading = true;
            crossfadeElapsed = 0f;
            crossfadeDurationActive = Mathf.Max(0.05f, fadeDuration);
            fadeOutStartVolume = activeDeck.volume;
            standbyDeck.Stop();
        }

        private void StartCombatProgression(int initialPhase)
        {
            currentState = MusicState.Combat;
            ClearProgressionQueue();
            highestQueuedPhase = 0;

            int clampedStart = Mathf.Clamp(initialPhase, 1, 3);
            EnqueuePhasePair(clampedStart);
            highestQueuedPhase = clampedStart;

            if (progressionQueue.Count == 0) return;

            var first = progressionQueue.Dequeue();
            CrossfadeImmediate(first.clip, first.loop, crossfadeDuration);

            // Immediately pre-schedule the loop (or next queued track) at the exact end of the intro
            if (!first.loop && progressionQueue.Count > 0)
            {
                ScheduleNextFromQueue(activeStartDspTime + first.clip.length);
            }
        }

        private void EnqueueUpToPhase(int targetPhase)
        {
            int clampedTarget = Mathf.Clamp(targetPhase, 1, 3);
            while (highestQueuedPhase < clampedTarget)
            {
                highestQueuedPhase++;
                EnqueuePhasePair(highestQueuedPhase);
            }

            // Ensure the current track stops looping after its current playthrough and queues the next
            if (activeDeck.isPlaying && activeDeck.clip != null && !hasScheduledNext && progressionQueue.Count > 0)
            {
                double currentTrackEndDsp = ComputeCurrentPlaythroughEndDsp();
                activeDeck.loop = false;
                ScheduleNextFromQueue(currentTrackEndDsp);
            }
        }

        private void EnqueuePhasePair(int phase)
        {
            AudioClip intro = null;
            AudioClip loop = null;

            switch (phase)
            {
                case 1:
                    intro = phase1IntroClip;
                    loop = phase1LoopClip;
                    break;
                case 2:
                    intro = phase2IntroClip;
                    loop = phase2LoopClip;
                    break;
                case 3:
                    intro = phase3IntroClip;
                    loop = phase3LoopClip;
                    break;
            }

            if (intro != null)
            {
                progressionQueue.Enqueue(new QueuedTrack { clip = intro, loop = false, phase = phase });
            }
            if (loop != null)
            {
                progressionQueue.Enqueue(new QueuedTrack { clip = loop, loop = true, phase = phase });
            }
        }

        private void CrossfadeImmediate(AudioClip clip, bool loop, float fadeDuration)
        {
            if (clip == null) return;

            standbyDeck.Stop();
            standbyDeck.clip = clip;
            standbyDeck.loop = loop;
            standbyDeck.volume = 0f;

            double startDsp = AudioSettings.dspTime + 0.05;
            standbyDeck.PlayScheduled(startDsp);

            // Swap decks so standby becomes active
            var prevActive = activeDeck;
            activeDeck = standbyDeck;
            standbyDeck = prevActive;

            activeStartDspTime = startDsp;
            hasScheduledNext = false;
            scheduledStartDspTime = 0;

            fadingToSilence = false;
            isCrossfading = true;
            crossfadeElapsed = 0f;
            crossfadeDurationActive = Mathf.Max(0.05f, fadeDuration);
            fadeOutStartVolume = standbyDeck.isPlaying ? standbyDeck.volume : 0f;
        }

        private void UpdateCrossfade()
        {
            if (!isCrossfading) return;

            crossfadeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(crossfadeElapsed / crossfadeDurationActive);

            if (fadingToSilence)
            {
                activeDeck.volume = Mathf.Lerp(fadeOutStartVolume, 0f, t);
                if (t >= 1f)
                {
                    activeDeck.Stop();
                    isCrossfading = false;
                    fadingToSilence = false;
                }
                return;
            }

            activeDeck.volume = Mathf.Lerp(0f, musicVolume, t);
            // Only fade out standbyDeck when it's the old deck, NOT when it holds a DSP-scheduled next track
            if (!hasScheduledNext && standbyDeck.isPlaying)
            {
                standbyDeck.volume = Mathf.Lerp(fadeOutStartVolume, 0f, t);
            }

            if (t >= 1f)
            {
                if (!hasScheduledNext)
                {
                    if (standbyDeck.isPlaying) standbyDeck.Stop();
                    standbyDeck.volume = 0f;
                }
                activeDeck.volume = musicVolume;
                isCrossfading = false;
            }
        }

        private void UpdateProgressionQueue()
        {
            if (currentState != MusicState.Combat) return;

            // Detect when the DSP-scheduled standbyDeck has taken over from activeDeck
            if (hasScheduledNext && AudioSettings.dspTime >= scheduledStartDspTime)
            {
                // Swap decks cleanly
                var finishedDeck = activeDeck;
                activeDeck = standbyDeck;
                standbyDeck = finishedDeck;

                activeStartDspTime = scheduledStartDspTime;
                activeDeck.volume = musicVolume;
                hasScheduledNext = false;
                scheduledStartDspTime = 0;
            }

            // If we're on a non-looping track (e.g. an Intro, or a Loop whose loop flag was cleared)
            // and haven't scheduled the next queued track yet, schedule it now.
            if (!hasScheduledNext && progressionQueue.Count > 0 && activeDeck.clip != null)
            {
                // If another phase was queued while a loop was playing, clear loop so it finishes cleanly
                if (activeDeck.loop)
                {
                    activeDeck.loop = false;
                }

                if (activeDeck.isPlaying)
                {
                    double endDsp = ComputeCurrentPlaythroughEndDsp();
                    ScheduleNextFromQueue(endDsp);
                }
                else
                {
                    // Fallback if activeDeck already finished before scheduling
                    var next = progressionQueue.Dequeue();
                    if (next.clip != null)
                    {
                        bool shouldLoop = next.loop && progressionQueue.Count == 0;
                        activeDeck.Stop();
                        activeDeck.clip = next.clip;
                        activeDeck.loop = shouldLoop;
                        activeDeck.volume = musicVolume;
                        double startDsp = AudioSettings.dspTime + 0.02;
                        activeDeck.PlayScheduled(startDsp);
                        activeStartDspTime = startDsp;
                    }
                }
            }
        }

        private double ComputeCurrentPlaythroughEndDsp()
        {
            double now = AudioSettings.dspTime;
            if (activeDeck.clip == null || activeDeck.clip.length <= 0.01f)
            {
                return now + 0.05;
            }

            double len = activeDeck.clip.length;
            double elapsed = Math.Max(0.0, now - activeStartDspTime);
            int completedIterations = (int)Math.Floor(elapsed / len);
            double endDsp = activeStartDspTime + (completedIterations + 1) * len;

            // Guard against scheduling too close to an imminent audio buffer wrap
            if (endDsp - now < 0.08)
            {
                endDsp += len;
            }

            return endDsp;
        }

        private void ScheduleNextFromQueue(double startDspTime)
        {
            if (progressionQueue.Count == 0) return;

            var next = progressionQueue.Dequeue();
            if (next.clip == null) return;

            // If there's already another phase waiting behind a Loopable track, don't loop it infinitely
            bool shouldLoop = next.loop && progressionQueue.Count == 0;

            standbyDeck.Stop();
            standbyDeck.clip = next.clip;
            standbyDeck.loop = shouldLoop;
            standbyDeck.volume = musicVolume;
            standbyDeck.PlayScheduled(startDspTime);

            scheduledStartDspTime = startDspTime;
            hasScheduledNext = true;
        }

        private void ClearProgressionQueue()
        {
            progressionQueue.Clear();
            hasScheduledNext = false;
            scheduledStartDspTime = 0;
        }
    }
}
