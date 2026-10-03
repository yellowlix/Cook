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
            session.OperationStarted += OnOperationStarted;
            session.OperationCompleted += OnOperationCompleted;
        }

        public void Unbind()
        {
            if (session == null) return;
            session.StationChanged -= OnStationChanged;
            session.OperationStarted -= OnOperationStarted;
            session.OperationCompleted -= OnOperationCompleted;
            session = null;
        }

        private void OnDestroy() => Unbind();

        private void OnStationChanged(CookingStation activeStation)
        {
            foreach (StationPresentation presentation in stations)
            {
                if (presentation?.root != null)
                {
                    presentation.root.SetActive(presentation.station == activeStation);
                }
            }
        }

        private void OnOperationStarted(OperationRuntimeData operation, int index, int count)
        {
            SetTrigger(operation.Station, operation.AnimatorStartTrigger);
        }

        private void OnOperationCompleted(OperationRuntimeData operation)
        {
            if (operation.AnimationMode == CookingAnimationMode.Sustained)
            {
                SetTrigger(operation.Station, operation.AnimatorStopTrigger);
            }
        }

        private void SetTrigger(CookingStation station, string trigger)
        {
            if (string.IsNullOrWhiteSpace(trigger)) return;
            foreach (StationPresentation presentation in stations)
            {
                if (presentation != null && presentation.station == station && presentation.animator != null)
                {
                    presentation.animator.SetTrigger(trigger);
                    return;
                }
            }
        }
    }
}
