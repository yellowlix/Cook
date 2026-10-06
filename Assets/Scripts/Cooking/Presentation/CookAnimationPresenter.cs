using System;
using Cook.Core;
using Cook.Managers;
using UnityEngine;

namespace Cook.Presentation
{
    [Serializable]
    public sealed class StationPresentation
    {
        public ProductionStation station;
        public GameObject root;
        public Animator animator;
    }

    /// <summary>保留各工位角色显隐，动画只作用于 CharacterVisual 子对象。</summary>
    public sealed class CookAnimationPresenter : MonoBehaviour
    {
        [SerializeField] private StationPresentation[] stations;
        private Animator characterAnimator;
        private EventManager events;
        private bool active;
        private static readonly int Tap = Animator.StringToHash("Tap");
        private static readonly int Holding = Animator.StringToHash("Holding");

        private void OnEnable()
        {
            events = GameManager.Instance?.Events;
            if (events == null) return;
            events.Subscribe<StationChanged>(OnStation);
            events.Subscribe<CookInputPressed>(OnPressed);
            events.Subscribe<CookInputReleased>(OnReleased);
            events.Subscribe<OperationInputProgress>(OnProgress);
            events.Subscribe<ProductionChanged>(OnState);
        }

        private void OnDisable()
        {
            StopInput();
            if (events == null) return;
            events.Unsubscribe<StationChanged>(OnStation);
            events.Unsubscribe<CookInputPressed>(OnPressed);
            events.Unsubscribe<CookInputReleased>(OnReleased);
            events.Unsubscribe<OperationInputProgress>(OnProgress);
            events.Unsubscribe<ProductionChanged>(OnState);
            events = null;
        }

        private void OnStation(StationChanged message)
        {
            StopInput();
            characterAnimator = null;
            if (stations == null) return;
            foreach (StationPresentation presentation in stations)
            {
                if (presentation == null) continue;
                bool selected = presentation.station == message.Station;
                if (presentation.root != null) presentation.root.SetActive(selected);
                if (selected) characterAnimator = presentation.animator;
            }
            StopInput();
        }

        private void OnPressed(CookInputPressed message)
        {
            if (active && characterAnimator != null) characterAnimator.SetTrigger(Tap);
        }
        private void OnReleased(CookInputReleased message) => StopInput();
        private void OnProgress(OperationInputProgress message)
        {
            if (characterAnimator != null)
                characterAnimator.SetBool(Holding, active && message.Mode == ProductionInteractionMode.Hold && message.Progress > 0f && message.Progress < 1f);
        }
        private void OnState(ProductionChanged message)
        {
            active = message.Session.State == ProductionState.Active;
            if (!active) StopInput();
        }
        private void StopInput()
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;
            characterAnimator.ResetTrigger(Tap);
            characterAnimator.SetBool(Holding, false);
        }
    }
}
