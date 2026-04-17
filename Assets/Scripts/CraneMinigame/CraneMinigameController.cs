using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class CraneMinigameController : MonoBehaviour
    {
        [SerializeField] private Transform carriage;
        [SerializeField] private Transform hook;
        [SerializeField] private Transform rope;
        [SerializeField] private Transform targetObject;

        [Header("Horizontal Movement")]
        [SerializeField] private float leftLimit = -5.2f;
        [SerializeField] private float rightLimit = 5.2f;
        [SerializeField] private float horizontalSpeed = 2.8f;

        [Header("Vertical Movement")]
        [SerializeField] private float hookTopLocalY = -1f;
        [SerializeField] private float hookBottomLocalY = -6.3f;
        [SerializeField] private float descendSpeed = 5f;
        [SerializeField] private float ascendSpeed = 5.5f;

        [Header("Grab")]
        [SerializeField] private float grabToleranceX = 0.8f;
        [SerializeField] private float grabToleranceY = 0.65f;
        [SerializeField] private Vector2 targetSpawnRange = new Vector2(-4.4f, 4.4f);
        [SerializeField] private Vector3 grabbedTargetLocalOffset = new Vector3(0f, -0.95f, 0f);

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
        private float horizontalDirection = 1f;
        private bool isConfigured;
        private bool targetAttached;
        private Transform targetOriginalParent;
        private Vector3 targetStartPosition;
        private Vector3 targetStartScale;
        private Vector3 hookStartLocalPosition;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;

        public void SetupDemo(Transform carriageRef, Transform hookRef, Transform ropeRef, Transform targetRef)
        {
            carriage = carriageRef;
            hook = hookRef;
            rope = ropeRef;
            targetObject = targetRef;
            CacheInitialState();
        }

        private void Start()
        {
            CacheInitialState();

            if (!isConfigured)
            {
                Debug.LogWarning($"{nameof(CraneMinigameController)} is missing one or more references.", this);
                enabled = false;
                return;
            }

            ResetRound();
        }

        private void Update()
        {
            HandleInput();

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
            {
                return;
            }

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

            if (keyboard == null)
            {
                return;
            }

            bool actionPressed = keyboard.spaceKey.wasPressedThisFrame;
            bool restartPressed = keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;

            if ((roundState == RoundState.Won || roundState == RoundState.Lost) && (actionPressed || restartPressed))
            {
                ResetRound();
                return;
            }

            if (roundState == RoundState.Aiming && actionPressed)
            {
                roundState = RoundState.Descending;
            }
        }

        private void UpdateHorizontalMovement()
        {
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
            SetHookLocalY(Mathf.MoveTowards(hook.localPosition.y, hookBottomLocalY, descendSpeed * Time.deltaTime));
            TryGrabTarget();

            if (targetAttached)
            {
                roundState = RoundState.Ascending;
                return;
            }

            if (Mathf.Approximately(hook.localPosition.y, hookBottomLocalY))
            {
                roundState = RoundState.Lost;
                onFailure.Invoke();
            }
        }

        private void UpdateLift()
        {
            SetHookLocalY(Mathf.MoveTowards(hook.localPosition.y, hookTopLocalY, ascendSpeed * Time.deltaTime));

            if (Mathf.Approximately(hook.localPosition.y, hookTopLocalY))
            {
                roundState = RoundState.Won;
                onSuccess.Invoke();
            }
        }

        private void TryGrabTarget()
        {
            if (targetAttached)
            {
                return;
            }

            Vector3 hookTipPosition = hook.position + (Vector3.down * 0.15f);
            Vector3 targetPosition = targetObject.position;

            bool insideHorizontalWindow = Mathf.Abs(hookTipPosition.x - targetPosition.x) <= grabToleranceX;
            bool insideVerticalWindow = Mathf.Abs(hookTipPosition.y - targetPosition.y) <= grabToleranceY;

            if (!insideHorizontalWindow || !insideVerticalWindow)
            {
                return;
            }

            targetAttached = true;
            targetObject.SetParent(hook, true);
            targetObject.localPosition = grabbedTargetLocalOffset;
            targetObject.localRotation = Quaternion.identity;
        }

        private void ResetRound()
        {
            roundState = RoundState.Aiming;
            horizontalDirection = 1f;
            targetAttached = false;

            if (targetObject.parent != targetOriginalParent)
            {
                targetObject.SetParent(targetOriginalParent, true);
            }

            Vector3 carriagePosition = carriage.position;
            carriagePosition.x = leftLimit;
            carriage.position = carriagePosition;

            SetHookLocalY(hookTopLocalY);

            Vector3 nextTargetPosition = targetStartPosition;
            if (targetSpawnRange.x < targetSpawnRange.y)
            {
                nextTargetPosition.x = Random.Range(targetSpawnRange.x, targetSpawnRange.y);
            }

            targetObject.position = nextTargetPosition;
            targetObject.rotation = Quaternion.identity;
            targetObject.localScale = targetStartScale;
        }

        private void SetHookLocalY(float nextY)
        {
            Vector3 nextHookPosition = hook.localPosition;
            nextHookPosition.y = nextY;
            hook.localPosition = nextHookPosition;
            UpdateRopeVisual();
        }

        private void UpdateRopeVisual()
        {
            const float carriageBottomY = -0.45f;
            float hookTopY = hook.localPosition.y + 0.32f;
            float ropeLength = Mathf.Abs(carriageBottomY - hookTopY);

            Vector3 ropePosition = rope.localPosition;
            ropePosition.y = (carriageBottomY + hookTopY) * 0.5f;
            rope.localPosition = ropePosition;

            Vector3 ropeScale = rope.localScale;
            ropeScale.y = ropeLength;
            rope.localScale = ropeScale;
        }

        private void CacheInitialState()
        {
            isConfigured = carriage != null && hook != null && rope != null && targetObject != null;

            if (!isConfigured)
            {
                return;
            }

            if (targetOriginalParent == null)
            {
                targetOriginalParent = targetObject.parent;
            }

            targetStartPosition = targetObject.position;
            targetStartScale = targetObject.localScale;
            hookStartLocalPosition = hook.localPosition;

            if (!Mathf.Approximately(hookStartLocalPosition.y, hookTopLocalY))
            {
                hookTopLocalY = hookStartLocalPosition.y;
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
                    return "Status: Aim the claw, then press Space.";
                case RoundState.Descending:
                    return "Status: Dropping...";
                case RoundState.Ascending:
                    return "Status: Prize grabbed. Lifting it back up.";
                case RoundState.Won:
                    return "Status: Success! Press Space or R to restart.";
                case RoundState.Lost:
                    return "Status: Missed it. Press Space or R to try again.";
                default:
                    return string.Empty;
            }
        }
    }
}
