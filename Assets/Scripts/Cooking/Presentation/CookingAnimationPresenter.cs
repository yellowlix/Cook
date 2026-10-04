using System;
using Cook.Core;
using UnityEngine;
using CoreCookingSession = Cook.Core.CookingSession;

namespace Cook.Presentation
{
    [Serializable]
    public sealed class StationPresentation
    {
        public CookingStation station;
        public GameObject root;
        public Animator animator;
    }

    /// <summary>
    /// 把玩法事件翻译为站位显示和 Animator Trigger。
    /// </summary>
    public sealed class CookingAnimationPresenter : MonoBehaviour
    {
        [SerializeField] private StationPresentation[] stations = Array.Empty<StationPresentation>();
        private CoreCookingSession session;
        private OperationRuntimeData sustainedOperation;

        public void ConfigureStations(StationPresentation[] value)
        {
            stations = value ?? Array.Empty<StationPresentation>();
        }

        public void Bind(CoreCookingSession session)
        {
            Unbind();
            this.session = session;
            if (session == null) return;
            session.StationChanged += OnStationChanged;
            session.OperationInputStarted += OnOperationInputStarted;
            session.OperationInputStopped += OnOperationInputStopped;
            session.OperationCompleted += OnOperationCompleted;
            session.StateChanged += OnStateChanged;
        }

        public void Unbind()
        {
            if (session == null) return;
            StopSustained();
            session.StationChanged -= OnStationChanged;
            session.OperationInputStarted -= OnOperationInputStarted;
            session.OperationInputStopped -= OnOperationInputStopped;
            session.OperationCompleted -= OnOperationCompleted;
            session.StateChanged -= OnStateChanged;
            session = null;
        }

        private void OnDestroy() => Unbind();

        private void OnStationChanged(CookingStation activeStation)
        {
            if (sustainedOperation != null && sustainedOperation.Station != activeStation)
            {
                StopSustained();
            }

            foreach (StationPresentation presentation in stations)
            {
                if (presentation?.root != null)
                {
                    presentation.root.SetActive(presentation.station == activeStation);
                }
            }
        }

        private void OnOperationInputStarted(OperationRuntimeData operation)
        {
            if (operation.AnimationMode == CookingAnimationMode.OneShot)
            {
                SetTrigger(operation.Station, operation.AnimatorStartTrigger);
                return;
            }

            if (sustainedOperation == operation) return;
            StopSustained();
            if (SetTrigger(operation.Station, operation.AnimatorStartTrigger))
            {
                sustainedOperation = operation;
            }
        }

        private void OnOperationInputStopped(OperationRuntimeData operation)
        {
            if (sustainedOperation == operation) StopSustained();
        }

        private void OnOperationCompleted(OperationRuntimeData operation)
        {
            if (sustainedOperation == operation) StopSustained();
        }

        private void OnStateChanged(CookingSessionState state)
        {
            if (state != CookingSessionState.OperationActive) StopSustained();
        }

        private void StopSustained()
        {
            if (sustainedOperation == null) return;
            SetTrigger(sustainedOperation.Station, sustainedOperation.AnimatorStopTrigger);
            sustainedOperation = null;
        }

        private bool SetTrigger(CookingStation station, string trigger)
        {
            if (string.IsNullOrWhiteSpace(trigger)) return false;
            foreach (StationPresentation presentation in stations)
            {
                if (presentation != null && presentation.station == station &&
                    presentation.animator != null && presentation.animator.isActiveAndEnabled &&
                    presentation.animator.runtimeAnimatorController != null)
                {
                    presentation.animator.SetTrigger(trigger);
                    return true;
                }
            }
            return false;
        }
    }
}
