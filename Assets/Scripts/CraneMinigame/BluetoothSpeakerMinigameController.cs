using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class BluetoothSpeakerMinigameController : GameController
    {
        [SerializeField] private Transform speakerRoot;
        [SerializeField] private Transform hitTarget;
        [SerializeField] private Transform hammerVisual;
        [SerializeField] private Transform destroyedStage;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform[] crackStages;
        [SerializeField] private Transform[] soundWaves;
        [SerializeField] private AudioSource speakerAudioSource;
        [SerializeField] private AudioSource speakerSfxSource;
        [SerializeField] private AudioDistortionFilter distortionFilter;
        [SerializeField] private AudioLowPassFilter lowPassFilter;
        [SerializeField] private AudioClip hammerHitClip;
        [SerializeField] private AudioClip speakerDestroyedClip;
        [SerializeField] private GameObject damageImpactPrefab;

        [Header("Gameplay")]
        [SerializeField] private int hitsToBreak = 10;
        [SerializeField] private float timeLimit = 5.5f;
        [SerializeField] private float clickRadius = 2.05f;

        [Header("Feedback")]
        [SerializeField] private float speakerPulseSpeed = 7f;
        [SerializeField] private float speakerPulseAmount = 0.045f;
        [SerializeField] private float hitPunchAmount = 0.12f;
        [SerializeField] private float feedbackRecoverSpeed = 7f;
        [SerializeField] private float hammerIdleAngle = -28f;
        [SerializeField] private float hammerHitAngle = 34f;
        [SerializeField] private float hammerSwingDuration = 0.14f;
        [SerializeField] private Vector3 hammerCursorOffset = new Vector3(0.62f, -0.62f, -2f);
        [SerializeField] private float maxDistortionLevel = 1f;
        [SerializeField] private float minCutoffFrequency = 260f;
        [SerializeField] private float maxPitchJitter = 0.3f;
        [SerializeField] private float hitDistortionKick = 0.3f;
        [SerializeField] private float hitCutoffDrop = 3400f;
        [SerializeField] private float audioRecoverSpeed = 2.4f;
        [SerializeField] private float damageImpactLifetime = 0.7f;
        [SerializeField] private float damageImpactSpread = 0.45f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private enum RoundState
        {
            Playing,
            Won,
            Lost
        }

        private float baseTimeLimit;
        private float basePulseSpeed;
        private RoundState roundState = RoundState.Playing;
        private int hitsDone;
        private float timeRemaining;
        private float animationTime;
        private float hitFeedback;
        private float hammerSwingTimer;
        private float audioHitJolt;
        private bool speakerDestroyed;
        private string lastResult = "Click the speaker and break it.";
        private Vector3 speakerBaseScale;
        private Vector3 speakerBaseLocalPosition;
        private Vector3 hammerBaseScale;
        private float baseVolume = 0.85f;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;


        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            CacheState();
            ResetRound();
        }


        private void OnEnable()
        {
            Cursor.visible = false;
            ResumeAudio();
        }

        private void OnDisable()
        {
            Cursor.visible = true;
            PauseAudio();
        }

        private void Update()
        {
            UpdateHammerVisual();
            HandleInput();
            UpdatePresentation();

            if (roundState != RoundState.Playing)
            {
                return;
            }

            timeRemaining -= Time.deltaTime;
            if (timeRemaining > 0f)
            {
                return;
            }

            timeRemaining = 0f;
            roundState = RoundState.Lost;
            lastResult = "The music is still blasting. Time is up.";
            onFailure.Invoke();
            ReportRoundFinished(false);
        }

        private void OnGUI()
        {
            if (!enabled)
            {
                return;
            }

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(430f, Screen.width - 30f);
            Rect panelRect = new Rect(Screen.width - panelWidth - 16f, Screen.height - 136f, panelWidth, 120f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
            Rect bodyRect = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 38f);
            Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 80f, panelRect.width - 28f, 24f);

            GUI.Label(titleRect, "Bluetooth Speaker Smash", titleStyle);
            GUI.Label(bodyRect, "The speaker is blasting music. Use the mouse like a hammer and smash it before time runs out.", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }

        private void HandleInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            bool restartPressed = keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (roundState == RoundState.Playing)
            {
                if (!clicked)
                {
                    return;
                }

                hammerSwingTimer = hammerSwingDuration;
                if (!TryGetCursorWorldPosition(mouse.position.ReadValue(), out Vector3 cursorWorld))
                {
                    return;
                }

                if (IsSpeakerClicked(cursorWorld))
                {
                    RegisterHit(cursorWorld);
                }
                else
                {
                    lastResult = "Missed. Hit the speaker body.";
                }

                return;
            }

            if (!autoRestartEnabled)
            {
                return;
            }

            if (clicked || restartPressed)
            {
                ResetRound();
            }
        }

        private bool IsSpeakerClicked(Vector3 cursorWorld)
        {
            if (hitTarget == null)
            {
                return false;
            }

            return Vector2.Distance(cursorWorld, hitTarget.position) <= clickRadius;
        }

        private void RegisterHit(Vector3 hitPoint)
        {
            hitsDone++;
            hitFeedback = 1f;
            audioHitJolt = Mathf.Clamp01(audioHitJolt + 0.85f);
            lastResult = $"SMASH! {Mathf.Max(0, hitsToBreak - hitsDone)} hits left.";
            UpdateCrackVisibility();
            SpawnDamageImpact(hitPoint);
            PlaySfx(hammerHitClip, 1f);
            ApplyAudioDamage(true);

            if (hitsDone < hitsToBreak)
            {
                return;
            }

            roundState = RoundState.Won;
            speakerDestroyed = true;
            lastResult = "Speaker destroyed. Silence achieved.";
            ShowDestroyedVisuals();
            BreakSpeakerAudio();
            onSuccess.Invoke();
            ReportRoundFinished(true);
        }

        protected override void ResetRound()
        {
            roundState = RoundState.Playing;
            hitsDone = 0;
            timeRemaining = timeLimit;
            animationTime = Random.Range(0f, 10f);
            hitFeedback = 0f;
            hammerSwingTimer = 0f;
            audioHitJolt = 0f;
            speakerDestroyed = false;
            lastResult = "Click the speaker and break it.";
            roundReported = false;
            UpdateCrackVisibility();
            RestorePresentation();
            RestoreDestroyedVisuals();
            RestoreAudio();
        }

        private void UpdatePresentation()
        {
            animationTime += Time.deltaTime;
            hitFeedback = Mathf.MoveTowards(hitFeedback, 0f, feedbackRecoverSpeed * Time.deltaTime);
            hammerSwingTimer = Mathf.MoveTowards(hammerSwingTimer, 0f, Time.deltaTime);
            audioHitJolt = Mathf.MoveTowards(audioHitJolt, 0f, audioRecoverSpeed * Time.deltaTime);

            if (speakerRoot != null)
            {
                float beat = (Mathf.Sin(animationTime * speakerPulseSpeed) + 1f) * 0.5f;
                float pulseScale = 1f + (beat * speakerPulseAmount) + (hitFeedback * hitPunchAmount);
                Vector2 shakeOffset = Random.insideUnitCircle * 0.06f * hitFeedback;
                speakerRoot.localScale = speakerBaseScale * pulseScale;
                speakerRoot.localPosition = speakerBaseLocalPosition + new Vector3(shakeOffset.x, shakeOffset.y, 0f);
            }

            if (soundWaves != null)
            {
                for (int i = 0; i < soundWaves.Length; i++)
                {
                    Transform wave = soundWaves[i];
                    if (wave == null)
                    {
                        continue;
                    }

                    float intensity = 0.8f + ((Mathf.Sin(animationTime * 9f + (i * 0.8f)) + 1f) * 0.35f);
                    Vector3 localScale = wave.localScale;
                    localScale.y = intensity;
                    localScale.x = 0.22f + (intensity * 0.07f);
                    wave.localScale = localScale;

                    SpriteRenderer renderer = wave.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        Color color = renderer.color;
                        color.a = 0.36f + (intensity * 0.28f);
                        renderer.color = color;
                    }
                }
            }

            if (!speakerDestroyed)
            {
                ApplyAudioDamage(false);
            }
        }

        private void UpdateHammerVisual()
        {
            if (hammerVisual == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                hammerVisual.gameObject.SetActive(false);
                return;
            }

            if (!TryGetCursorWorldPosition(mouse.position.ReadValue(), out Vector3 cursorWorld))
            {
                hammerVisual.gameObject.SetActive(false);
                return;
            }

            hammerVisual.gameObject.SetActive(true);
            hammerVisual.position = cursorWorld + hammerCursorOffset;

            float swingProgress = hammerSwingDuration <= 0f
                ? 0f
                : Mathf.Clamp01(hammerSwingTimer / hammerSwingDuration);
            float angle = Mathf.Lerp(hammerIdleAngle, hammerHitAngle, swingProgress);
            hammerVisual.rotation = Quaternion.Euler(0f, 0f, angle);
            hammerVisual.localScale = hammerBaseScale;
        }

        private bool TryGetCursorWorldPosition(Vector2 cursorScreen, out Vector3 cursorWorld)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            if (targetCamera == null)
            {
                cursorWorld = default;
                return false;
            }

            cursorWorld = targetCamera.ScreenToWorldPoint(new Vector3(cursorScreen.x, cursorScreen.y, Mathf.Abs(targetCamera.transform.position.z)));
            cursorWorld.z = 0f;
            return true;
        }

        private void UpdateCrackVisibility()
        {
            if (crackStages == null)
            {
                return;
            }

            float damage = hitsToBreak <= 0 ? 1f : (float)hitsDone / hitsToBreak;

            for (int i = 0; i < crackStages.Length; i++)
            {
                if (crackStages[i] == null)
                {
                    continue;
                }

                float threshold = (i + 1f) / (crackStages.Length + 1f);
                crackStages[i].gameObject.SetActive(damage >= threshold);
            }
        }

        private void RestorePresentation()
        {
            if (speakerRoot != null)
            {
                speakerRoot.localScale = speakerBaseScale;
                speakerRoot.localPosition = speakerBaseLocalPosition;
                speakerRoot.localRotation = Quaternion.identity;
            }

            if (hammerVisual != null)
            {
                hammerVisual.localScale = hammerBaseScale;
                hammerVisual.rotation = Quaternion.Euler(0f, 0f, hammerIdleAngle);
            }
        }

        private void ApplyAudioDamage(bool forceJolt)
        {
            if (speakerAudioSource == null)
            {
                return;
            }

            float damage = hitsToBreak <= 0 ? 1f : Mathf.Clamp01((float)hitsDone / hitsToBreak);
            float jolt = forceJolt ? 1f : audioHitJolt;

            float distortedDamage = damage * damage;
            float jitter = Random.Range(-maxPitchJitter, maxPitchJitter) * jolt;
            speakerAudioSource.pitch = Mathf.Clamp(Mathf.Lerp(1f, 0.4f, damage) + jitter, 0.12f, 1.1f);
            speakerAudioSource.volume = Mathf.Clamp01(Mathf.Lerp(baseVolume, 0f, distortedDamage) + (jolt * 0.025f));

            if (distortionFilter != null)
            {
                float baseDistortion = Mathf.Lerp(0f, maxDistortionLevel, distortedDamage);
                distortionFilter.distortionLevel = Mathf.Clamp01(baseDistortion + (jolt * hitDistortionKick));
            }

            if (lowPassFilter != null)
            {
                float baseCutoff = Mathf.Lerp(22000f, minCutoffFrequency, distortedDamage);
                lowPassFilter.cutoffFrequency = Mathf.Max(250f, baseCutoff - (jolt * hitCutoffDrop));
                lowPassFilter.lowpassResonanceQ = Mathf.Lerp(1f, 2.2f, distortedDamage);
            }
        }

        private void RestoreAudio()
        {
            if (speakerAudioSource != null)
            {
                speakerAudioSource.Stop();
                speakerAudioSource.pitch = 1f;
                speakerAudioSource.volume = baseVolume;
                ResumeAudio();
            }

            if (distortionFilter != null)
            {
                distortionFilter.distortionLevel = 0f;
            }

            if (lowPassFilter != null)
            {
                lowPassFilter.cutoffFrequency = 22000f;
                lowPassFilter.lowpassResonanceQ = 1f;
            }
        }

        private void BreakSpeakerAudio()
        {
            if (speakerAudioSource != null)
            {
                speakerAudioSource.Stop();
                speakerAudioSource.pitch = 0.01f;
                speakerAudioSource.volume = 0f;
            }

            if (distortionFilter != null)
            {
                distortionFilter.distortionLevel = 1f;
            }

            if (lowPassFilter != null)
            {
                lowPassFilter.cutoffFrequency = 340f;
                lowPassFilter.lowpassResonanceQ = 2.4f;
            }

            PlaySfx(speakerDestroyedClip, 1f);
        }

        private void ResumeAudio()
        {
            if (speakerAudioSource == null || speakerAudioSource.clip == null)
            {
                return;
            }

            if (!speakerAudioSource.isPlaying)
            {
                speakerAudioSource.Play();
            }
        }

        private void PauseAudio()
        {
            if (speakerAudioSource == null)
            {
                return;
            }

            if (speakerAudioSource.isPlaying)
            {
                speakerAudioSource.Pause();
            }
        }

        private void SpawnDamageImpact(Vector3 hitPoint)
        {
            if (damageImpactPrefab == null)
            {
                return;
            }

            Vector2 randomOffset = Random.insideUnitCircle * damageImpactSpread;
            Vector3 spawnPosition = hitPoint + new Vector3(randomOffset.x, randomOffset.y, 0f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f));
            GameObject impactInstance = Instantiate(damageImpactPrefab, spawnPosition, rotation);

            if (impactInstance != null)
            {
                Destroy(impactInstance, damageImpactLifetime);
            }
        }

        private void PlaySfx(AudioClip clip, float volumeScale)
        {
            if (clip == null || speakerSfxSource == null)
            {
                return;
            }

            speakerSfxSource.PlayOneShot(clip, volumeScale);
        }

        private void ShowDestroyedVisuals()
        {
            if (destroyedStage != null)
            {
                destroyedStage.gameObject.SetActive(true);
            }

            if (speakerRoot != null)
            {
                speakerRoot.localRotation = Quaternion.Euler(0f, 0f, -7f);
                speakerRoot.localPosition = speakerBaseLocalPosition + new Vector3(0.08f, -0.18f, 0f);
            }

            if (soundWaves != null)
            {
                for (int i = 0; i < soundWaves.Length; i++)
                {
                    if (soundWaves[i] != null)
                    {
                        soundWaves[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void RestoreDestroyedVisuals()
        {
            if (destroyedStage != null)
            {
                destroyedStage.gameObject.SetActive(false);
            }

            if (soundWaves != null)
            {
                for (int i = 0; i < soundWaves.Length; i++)
                {
                    if (soundWaves[i] != null)
                    {
                        soundWaves[i].gameObject.SetActive(true);
                    }
                }
            }
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.74f, 0.92f, 0.99f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.83f, 0.87f, 0.95f) }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.71f, 0.31f) }
            };
        }

        private string GetStatusText()
        {
            switch (roundState)
            {
                case RoundState.Playing:
                    return $"Hits: {hitsDone}/{hitsToBreak} | Time: {timeRemaining:0.0}s | {lastResult}";
                case RoundState.Won:
                    return $"{lastResult} Click or press R to restart.";
                case RoundState.Lost:
                    return $"{lastResult} Click or press R to retry.";
                default:
                    return string.Empty;
            }
        }

        public override void SetTimeLimit(float seconds) => timeLimit = seconds;
        public override float GetBaseTimeLimit() => baseTimeLimit;
        public override void SetSpeedMultiplier(float multiplier) => speakerPulseSpeed = basePulseSpeed * multiplier;

        private void CacheState()
        {
            baseTimeLimit = timeLimit;
            basePulseSpeed = speakerPulseSpeed;

            if (speakerRoot != null)
            {
                speakerBaseScale = speakerRoot.localScale;
                speakerBaseLocalPosition = speakerRoot.localPosition;
            }

            if (hammerVisual != null)
            {
                hammerBaseScale = hammerVisual.localScale;
            }

            if (speakerAudioSource != null)
            {
                baseVolume = speakerAudioSource.volume;
            }
        }
    }
}
