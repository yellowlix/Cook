using System;

namespace Cook.Core
{
    /// <summary>只识别完成输入，不持有工位、菜品或玩法会话。</summary>
    public sealed class OperationInput
    {
        public const float HoldDuration = 3f;
        private readonly float clickInterval;
        private bool pressed;
        private bool holdCompleted;
        private double heldTime;
        private double idleTime;

        public OperationInput(float clickInterval)
        {
            if (float.IsNaN(clickInterval) || float.IsInfinity(clickInterval) || clickInterval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(clickInterval));
            this.clickInterval = clickInterval;
        }

        public event Action<ProductionInteractionMode> Completed;
        public event Action<ProductionInteractionMode, float> ProgressChanged;
        public int ClickCount { get; private set; }
        public float Progress { get; private set; }

        public void Press()
        {
            if (pressed) return;
            pressed = true;
            holdCompleted = false;
            heldTime = 0d;
        }

        public void Release()
        {
            if (!pressed) return;
            pressed = false;
            if (holdCompleted || heldTime > clickInterval)
            {
                Reset();
                return;
            }

            ClickCount++;
            idleTime = 0d;
            Report(ProductionInteractionMode.TripleClick, ClickCount / 3f);
            if (ClickCount == 3) Complete(ProductionInteractionMode.TripleClick);
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (pressed)
            {
                if (holdCompleted) return;
                heldTime += deltaTime;
                if (heldTime > clickInterval)
                {
                    ClickCount = 0;
                    Report(ProductionInteractionMode.Hold, (float)Math.Min(1d, heldTime / HoldDuration));
                }
                if (heldTime >= HoldDuration)
                {
                    holdCompleted = true;
                    // 保持 pressed，直到松开，防止一次持续按住生成多个步骤。
                    Completed?.Invoke(ProductionInteractionMode.Hold);
                }
            }
            else if (ClickCount > 0)
            {
                idleTime += deltaTime;
                if (idleTime >= clickInterval)
                {
                    if (ClickCount == 1) Complete(ProductionInteractionMode.Click);
                    else Reset();
                }
            }
        }

        public void Reset()
        {
            pressed = false;
            holdCompleted = false;
            heldTime = idleTime = 0d;
            ClickCount = 0;
            Report(ProductionInteractionMode.TripleClick, 0f);
        }

        private void Complete(ProductionInteractionMode mode)
        {
            Reset();
            Completed?.Invoke(mode);
        }

        private void Report(ProductionInteractionMode mode, float value)
        {
            Progress = value;
            ProgressChanged?.Invoke(mode, value);
        }
    }
}
