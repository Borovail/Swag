using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;
using UnityEngine.Serialization;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class CraneMinigameController : GameController
    {

        [SerializeField] private AudioClip _craneMoveSound;
        [SerializeField] private AudioClip _craneMiss;
        [SerializeField] private AudioClip _craneCatch;

        [SerializeField] private Transform carriage;
        [FormerlySerializedAs("hook")]
        [SerializeField] private Transform grabPoint;
        [SerializeField] private Transform targetObject;

        [Header("Horizontal Movement")]
        [SerializeField] private float leftLimit = -5.2f;
        [SerializeField] private float rightLimit = 5.2f;
        [SerializeField] private float horizontalSpeed = 2.8f;

        [Header("Vertical Movement")]
        [FormerlySerializedAs("hookBottomLocalY")]
        [SerializeField] private float carriageBottomY = -6.3f;
        [SerializeField] private float descendSpeed = 5f;
        [SerializeField] private float ascendSpeed = 5.5f;

        [Header("Grab")]
        [SerializeField] private float grabToleranceX = 0.8f;
        [SerializeField] private float grabToleranceY = 0.65f;
        [SerializeField] private Vector2 targetSpawnRange = new Vector2(-4.4f, 4.4f);
        [SerializeField] private Vector3 grabbedTargetLocalOffset = new Vector3(0f, -0.95f, 0f);

        [Header("Round")]
        [SerializeField] private float timeLimit = 6f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private enum RoundState
        {
            Aiming,
            Descending,
            Ascending,
            Won,
            Lost
        }

        private RoundState roundState = RoundState.Aiming;
        private float baseHorizontalSpeed;
        private float baseDescendSpeed;
        private float baseAscendSpeed;
        private float horizontalDirection = 1f;
        private bool targetAttached;
        private Transform targetOriginalParent;
        private Vector3 targetStartPosition;
        private Vector3 targetStartScale;
        private Vector3 carriageStartPosition;
        private float timeRemaining;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;
        private bool isConfigured;

        private AudioSource _audio;
        private bool _isMovementSoundPlaying;

        private void Awake()
        {
            baseHorizontalSpeed = horizontalSpeed;
            baseDescendSpeed = descendSpeed;
            baseAscendSpeed = ascendSpeed;

            if (carriage != null)
            {
                carriageStartPosition = carriage.position;
            }

            if (targetObject != null && targetOriginalParent == null)
            {
                targetOriginalParent = targetObject.parent;
                targetStartScale = targetObject.localScale;
                targetStartPosition = targetObject.localPosition;
            }

            isConfigured = carriage != null && grabPoint != null && targetObject != null;

            _audio = GetComponent<AudioSource>();
        }

        private void Start()
        {
            if (isConfigured)
                return;

            Debug.LogWarning($"{nameof(CraneMinigameController)} requires carriage, grab point and target object references.", this);
            enabled = false;
        }

        private void Update()
        {
            HandleInput();

            if (roundState == RoundState.Aiming || roundState == RoundState.Descending || roundState == RoundState.Ascending)
            {
                timeRemaining -= Time.deltaTime;
                if (timeRemaining <= 0f)
                {
                    timeRemaining = 0f;
                    StopMovementSound();
                    PlayOneShot(_craneMiss);
                    roundState = RoundState.Lost;
                    onFailure.Invoke();
                    ReportRoundFinished(false);
                    return;
                }
            }

            switch (roundState)
            {
                case RoundState.Aiming:
                    UpdateHorizontalMovement();
                    break;
                case RoundState.Descending:
                    UpdateDrop();
                    break;
                case RoundState.Ascending:
                    UpdateLift();
                    break;
            }
        }

        private void OnGUI()
        {
            if (!enabled)
                return;

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(430f, Screen.width - 30f);
            Rect panelRect = new Rect(16f, 16f, panelWidth, 112f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
            Rect bodyRect = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 38f);
            Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 77f, panelRect.width - 28f, 24f);

            GUI.Label(titleRect, "Crane Claw Mini-Game", titleStyle);
            GUI.Label(bodyRect, "Press Space when the claw is above the object.\nHit it to lift the prize. Miss it and the round is lost.", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;

            bool actionPressed = keyboard.spaceKey.wasPressedThisFrame;
            bool restartPressed = keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;

            if ((roundState == RoundState.Won || roundState == RoundState.Lost) && (actionPressed || restartPressed))
            {
                if (!autoRestartEnabled)
                    return;

                ResetRound();
                return;
            }

            if (roundState == RoundState.Aiming && actionPressed)
                roundState = RoundState.Descending;
        }

        private void UpdateHorizontalMovement()
        {
            EnsureMovementSoundPlaying();

            Vector3 nextPosition = carriage.position;
            nextPosition.x += horizontalDirection * horizontalSpeed * Time.deltaTime;

            if (nextPosition.x >= rightLimit)
            {
                nextPosition.x = rightLimit;
                horizontalDirection = -1f;
            }
            else if (nextPosition.x <= leftLimit)
            {
                nextPosition.x = leftLimit;
                horizontalDirection = 1f;
            }

            carriage.position = nextPosition;
        }

        private void UpdateDrop()
        {
            EnsureMovementSoundPlaying();
            SetCarriageY(Mathf.MoveTowards(carriage.position.y, carriageBottomY, descendSpeed * Time.deltaTime));
            TryGrabTarget();

            if (targetAttached)
            {
                roundState = RoundState.Ascending;
                return;
            }

            if (Mathf.Approximately(carriage.position.y, carriageBottomY))
            {
                StopMovementSound();
                PlayOneShot(_craneMiss);
                roundState = RoundState.Lost;
                onFailure.Invoke();
                ReportRoundFinished(false);
            }
        }

        private void UpdateLift()
        {
            EnsureMovementSoundPlaying();
            SetCarriageY(Mathf.MoveTowards(carriage.position.y, carriageStartPosition.y, ascendSpeed * Time.deltaTime));

            if (Mathf.Approximately(carriage.position.y, carriageStartPosition.y))
            {
                StopMovementSound();
                roundState = RoundState.Won;
                onSuccess.Invoke();
                ReportRoundFinished(true);
            }
        }

        private void TryGrabTarget()
        {
            if (targetAttached)
                return;

            Vector3 hookTipPosition = grabPoint.position;
            Vector3 targetPosition = targetObject.position;

            bool insideHorizontalWindow = Mathf.Abs(hookTipPosition.x - targetPosition.x) <= grabToleranceX;
            bool insideVerticalWindow = Mathf.Abs(hookTipPosition.y - targetPosition.y) <= grabToleranceY;

            if (!insideHorizontalWindow || !insideVerticalWindow)
                return;

            StopMovementSound();
            PlayOneShot(_craneCatch);
            targetAttached = true;
            targetObject.SetParent(grabPoint, true);
            targetObject.localPosition = grabbedTargetLocalOffset;
            targetObject.localRotation = Quaternion.identity;
        }

        public override ControlScheme RequiredControls => ControlScheme.Spacebar;
        public override string ControlDescription => "Catch Patrick";

        public override void ApplyDifficulty(Difficulty difficulty)
        {
            float multiplier = difficulty switch
            {
                Difficulty.Easy   => 1f,
                Difficulty.Medium => 1.3f,
                Difficulty.Hard   => 1.7f,
                Difficulty.Insane => 2.2f,
                _                 => 1f
            };

            horizontalSpeed = baseHorizontalSpeed * multiplier;
            descendSpeed    = baseDescendSpeed    * multiplier;
            ascendSpeed     = baseAscendSpeed     * multiplier;
        }

        protected override void ResetRound()
        {
            roundState = RoundState.Aiming;
            horizontalDirection = 1f;
            targetAttached = false;
            roundReported = false;
            timeRemaining = timeLimit;
            StopMovementSound();

            if (targetObject.parent != targetOriginalParent)
            {
                targetObject.SetParent(targetOriginalParent, true);
            }

            Vector3 carriagePosition = carriageStartPosition;
            carriagePosition.x = leftLimit;
            carriage.position = carriagePosition;

            Vector3 nextTargetPosition = targetStartPosition;
            if (targetSpawnRange.x < targetSpawnRange.y)
                nextTargetPosition.x = Random.Range(targetSpawnRange.x, targetSpawnRange.y);

            targetObject.position = nextTargetPosition;
            targetObject.rotation = Quaternion.identity;
            targetObject.localScale = targetStartScale;
        }

        private void EnsureMovementSoundPlaying()
        {
            if (_audio == null || _craneMoveSound == null || _isMovementSoundPlaying)
                return;

            _audio.clip = _craneMoveSound;
            _audio.loop = true;
            _audio.Play();
            _isMovementSoundPlaying = true;
        }

        private void StopMovementSound()
        {
            if (_audio == null || !_isMovementSoundPlaying)
                return;

            _audio.Stop();
            _audio.loop = false;
            _audio.clip = null;
            _isMovementSoundPlaying = false;
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (_audio == null || clip == null)
                return;

            _audio.PlayOneShot(clip);
        }

        private void SetCarriageY(float nextY)
        {
            Vector3 nextCarriagePosition = carriage.position;
            nextCarriagePosition.y = nextY;
            carriage.position = nextCarriagePosition;
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
                normal = { textColor = new Color(0.13f, 0.18f, 0.29f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.2f, 0.24f, 0.32f) }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.56f, 0.17f, 0.16f) }
            };
        }

        private string GetStatusText()
        {
            switch (roundState)
            {
                case RoundState.Aiming:
                    return $"Status: {timeRemaining:0.0}s left. Aim the claw, then press Space.";
                case RoundState.Descending:
                    return $"Status: {timeRemaining:0.0}s left. Dropping...";
                case RoundState.Ascending:
                    return $"Status: {timeRemaining:0.0}s left. Prize grabbed. Lifting it back up.";
                case RoundState.Won:
                    return "Status: Success! Press Space or R to restart.";
                case RoundState.Lost:
                    return "Status: Time up or missed it. Press Space or R to try again.";
                default:
                    return string.Empty;
            }
        }
    }
}
