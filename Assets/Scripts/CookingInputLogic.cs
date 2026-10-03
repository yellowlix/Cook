using System;
using UnityEngine;

namespace Cook
{
    /// <summary>
    /// 当前料理如何解释同一个 J 键。
    /// 模式由料理内容决定，而不是让 Tap、MultiTap、Hold 同时竞争按键。
    /// </summary>
    public enum CookInputMode
    {
        SinglePress,
        RepeatedPress,
        Hold
    }

    /// <summary>
    /// 输入层输出给表现层的信号。
    /// Flags 允许一次按键同时产生 Start 和 Pulse。
    /// </summary>
    [Flags]
    public enum CookInputSignal
    {
        None = 0,
        Start = 1,
        Pulse = 2,
        Stop = 4
    }

    /// <summary>
    /// 与 MonoBehaviour、Input System 和 Animator 无关的料理输入规则。
    /// 保持为纯逻辑类，便于单元测试。
    /// </summary>
    public sealed class CookingInputLogic
    {
        private readonly float repeatedPressTimeout;
        private readonly float singlePressDuration;

        private bool actionActive;
        private CookInputMode activeMode;
        private float actionStartedAt;
        private float lastPressAt;

        public CookingInputLogic(
            float repeatedPressTimeout,
            float singlePressDuration)
        {
            // 防止 Inspector 中误填 0 或负数造成动作立即结束。
            this.repeatedPressTimeout = Mathf.Max(0.01f, repeatedPressTimeout);
            this.singlePressDuration = Mathf.Max(0.01f, singlePressDuration);
        }

        /// <summary>
        /// 向左或向右切换一个站位，并限制在有效索引内。
        /// </summary>
        public int StepStation(int current, int direction, int stationCount)
        {
            if (stationCount <= 0)
            {
                return -1;
            }

            int step = Math.Sign(direction);
            return Mathf.Clamp(current + step, 0, stationCount - 1);
        }

        /// <summary>
        /// 处理 J 键按下。
        /// 连打模式只有第一次按下产生 Start，之后只产生 Pulse，
        /// 因此连续切菜不会反复从 Cut_Enter 开始。
        /// </summary>
        public CookInputSignal Press(CookInputMode mode, float time)
        {
            if (!actionActive)
            {
                actionActive = true;
                activeMode = mode;
                actionStartedAt = time;
                lastPressAt = time;

                return mode == CookInputMode.Hold
                    ? CookInputSignal.Start
                    : CookInputSignal.Start | CookInputSignal.Pulse;
            }

            // 一个动作进行期间不允许另一种输入模式接管。
            if (mode != activeMode)
            {
                return CookInputSignal.None;
            }

            if (mode == CookInputMode.RepeatedPress)
            {
                lastPressAt = time;
                return CookInputSignal.Pulse;
            }

            return mode == CookInputMode.SinglePress
                ? CookInputSignal.Pulse
                : CookInputSignal.None;
        }

        /// <summary>
        /// 处理 J 键松开。只有长按模式需要在松开时停止。
        /// </summary>
        public CookInputSignal Release(CookInputMode mode)
        {
            if (!actionActive ||
                activeMode != mode ||
                mode != CookInputMode.Hold)
            {
                return CookInputSignal.None;
            }

            actionActive = false;
            return CookInputSignal.Stop;
        }

        /// <summary>
        /// 处理依赖时间的自动结束规则。
        /// </summary>
        public CookInputSignal Tick(CookInputMode mode, float time)
        {
            if (!actionActive || activeMode != mode)
            {
                return CookInputSignal.None;
            }

            bool repeatedPressFinished =
                mode == CookInputMode.RepeatedPress &&
                time - lastPressAt >= repeatedPressTimeout;

            bool singlePressFinished =
                mode == CookInputMode.SinglePress &&
                time - actionStartedAt >= singlePressDuration;

            if (!repeatedPressFinished && !singlePressFinished)
            {
                return CookInputSignal.None;
            }

            actionActive = false;
            return CookInputSignal.Stop;
        }

        /// <summary>
        /// 换位或中断料理时立即结束当前动作。
        /// </summary>
        public CookInputSignal Cancel()
        {
            if (!actionActive)
            {
                return CookInputSignal.None;
            }

            actionActive = false;
            return CookInputSignal.Stop;
        }
    }
}
