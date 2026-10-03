using Cook.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cook
{
    /// <summary>
    /// 把强类型 Input Action 转换为料理玩法与 Animator 参数。
    ///
    /// 左右键只切换三个站位角色的显示状态，不移动 Transform。
    /// J 键的含义由当前站位的 CookInputMode 决定：
    /// 单击、连续点击或按住。
    /// </summary>
    public sealed class CookingInputController : MonoBehaviour
    {
        [Header("Stations")]
        [Tooltip("左、中、右三个角色显示节点；任何时刻只启用一个。")]
        [SerializeField] private GameObject[] stationPresenters;

        [Tooltip("每个站位如何解释 J 键输入。")]
        [SerializeField] private CookInputMode[] stationModes;

        [Tooltip("每个站位开始料理时发送给 Animator 的 Trigger。")]
        [SerializeField] private string[] startTriggers;

        [Tooltip("游戏开始时显示的站位索引：0 左、1 中、2 右。")]
        [SerializeField] private int currentStation;

        [Header("Timing")]
        [Tooltip("连续点击停止多久后，认为本轮连打结束。")]
        [SerializeField, Min(0.01f)] private float repeatedPressTimeout = 0.45f;

        [Tooltip("单击动作播放多久后自动结束。")]
        [SerializeField, Min(0.01f)] private float singlePressDuration = 0.9f;

        // 由 Input System 自动生成的强类型包装类。
        // 不再通过 FindAction("字符串") 查找 Action。
        private CookInputActions inputs;
        private Animator[] stationAnimators;
        private CookingInputLogic logic;

        public int CurrentStation => currentStation;
        public int CookPulseCount { get; private set; }

        private void Awake()
        {
            // 生成类会在构造时创建 InputActionAsset 的运行时实例。
            inputs = new CookInputActions();
            logic = new CookingInputLogic(repeatedPressTimeout, singlePressDuration);

            stationAnimators = new Animator[stationPresenters.Length];
            for (int i = 0; i < stationPresenters.Length; i++)
            {
                if (stationPresenters[i] != null)
                {
                    stationAnimators[i] = stationPresenters[i].GetComponent<Animator>();
                }
            }

            currentStation = Mathf.Clamp(
                currentStation,
                0,
                Mathf.Max(0, stationPresenters.Length - 1));

            ApplyStationVisibility();
        }

        private void OnEnable()
        {
            // 强类型访问：Action 改名后这里会直接产生编译错误，
            // 不会拖到运行时才发现字符串写错。
            inputs.Player.ChangeStation.performed += OnChangeStation;
            inputs.Player.Cook.performed += OnCookPressed;
            inputs.Player.Cook.canceled += OnCookReleased;
            inputs.Player.Enable();
        }

        private void OnDisable()
        {
            // 成对解绑，避免对象重复启用后重复订阅回调。
            inputs.Player.ChangeStation.performed -= OnChangeStation;
            inputs.Player.Cook.performed -= OnCookPressed;
            inputs.Player.Cook.canceled -= OnCookReleased;
            inputs.Player.Disable();
        }

        private void OnDestroy()
        {
            // 生成类实现 IDisposable；销毁控制器时释放它创建的运行时资源。
            inputs?.Dispose();
        }

        private void Update()
        {
            if (!HasValidCurrentStation())
            {
                return;
            }

            // 连打超时和单击自动结束都属于“时间规则”，
            // 因此每帧只让纯逻辑类判断是否需要发送 Stop。
            ApplySignal(logic.Tick(CurrentMode, Time.time));
        }

        private void OnChangeStation(InputAction.CallbackContext context)
        {
            float value = context.ReadValue<float>();
            if (Mathf.Abs(value) < 0.5f || stationPresenters.Length == 0)
            {
                return;
            }

            int direction = value < 0f ? -1 : 1;
            int next = logic.StepStation(
                currentStation,
                direction,
                stationPresenters.Length);

            // 已经处在最左或最右时，继续按同方向不会发生变化。
            if (next == currentStation)
            {
                return;
            }

            // 换位前先结束旧站位正在播放的料理动作。
            ApplySignal(logic.Cancel());
            currentStation = next;
            ApplyStationVisibility();
        }

        private void OnCookPressed(InputAction.CallbackContext context)
        {
            if (!HasValidCurrentStation())
            {
                return;
            }

            ApplySignal(logic.Press(CurrentMode, Time.time));
        }

        private void OnCookReleased(InputAction.CallbackContext context)
        {
            if (!HasValidCurrentStation())
            {
                return;
            }

            // 只有 Hold 模式会在松开 J 时产生 Stop；
            // 单击与连打模式会忽略 Release。
            ApplySignal(logic.Release(CurrentMode));
        }

        private CookInputMode CurrentMode
        {
            get
            {
                return currentStation < stationModes.Length
                    ? stationModes[currentStation]
                    : CookInputMode.SinglePress;
            }
        }

        private bool HasValidCurrentStation()
        {
            return stationPresenters != null &&
                   currentStation >= 0 &&
                   currentStation < stationPresenters.Length &&
                   stationPresenters[currentStation] != null;
        }

        /// <summary>
        /// 将与具体动画无关的输入信号翻译成 Animator Trigger。
        /// </summary>
        private void ApplySignal(CookInputSignal signal)
        {
            Animator animator = HasValidCurrentStation()
                ? stationAnimators[currentStation]
                : null;

            if ((signal & CookInputSignal.Start) != 0 && animator != null)
            {
                string trigger = currentStation < startTriggers.Length
                    ? startTriggers[currentStation]
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(trigger))
                {
                    animator.SetTrigger(trigger);
                }
            }

            // Pulse 表示一次有效料理输入。
            // 连打时它只增加次数，不会重新触发 Start 动画。
            if ((signal & CookInputSignal.Pulse) != 0)
            {
                CookPulseCount++;
            }

            if ((signal & CookInputSignal.Stop) != 0 && animator != null)
            {
                animator.SetTrigger("ActionComplete");
            }
        }

        /// <summary>
        /// 通过 SetActive 完成离散换位，而不是移动角色。
        /// </summary>
        private void ApplyStationVisibility()
        {
            for (int i = 0; i < stationPresenters.Length; i++)
            {
                if (stationPresenters[i] != null)
                {
                    stationPresenters[i].SetActive(i == currentStation);
                }
            }
        }
    }
}
