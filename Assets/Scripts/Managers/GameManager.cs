using UnityEngine;
using Cook.Core;

namespace Cook.Managers
{
    /// <summary>唯一全局入口。子管理器是普通类，业务会话由场景控制器创建。</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        [SerializeField, Min(0.05f)] private float clickInterval = 0.35f;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private AudioSource sceneSource;
        [SerializeField] private AudioClip buttonSound;
        [SerializeField] private AudioClip operationSound;
        [SerializeField] private AudioClip completionSound;
        private bool focused = true;

        public GameInputManager Input { get; private set; }
        public GameUIManager UI { get; private set; }
        public GameAudioManager Audio { get; private set; }
        public EventManager Events { get; private set; }
        public AudioClip ButtonSound => buttonSound;
        public AudioClip OperationSound => operationSound;
        public AudioClip CompletionSound => completionSound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Events = new EventManager();
            Input = new GameInputManager(clickInterval);
            UI = new GameUIManager();
            Audio = new GameAudioManager(PrepareSource(ref bgmSource),
                PrepareSource(ref uiSource), PrepareSource(ref sceneSource));
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            Input?.SetFocused(focused);
            Events?.Subscribe<ProductionStarted>(OnProductionStarted);
            Events?.Subscribe<OperationPerformed>(OnOperationPerformed);
            Events?.Subscribe<ProductionFinished>(OnProductionFinished);
        }

        // 场景先推进制作计时并清理跨工序输入，再确认本帧输入。
        private void LateUpdate() => Input?.Tick(Time.deltaTime);

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (Instance == this && isActiveAndEnabled) Input?.SetFocused(value);
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            Input?.SetFocused(false);
            Events?.Unsubscribe<ProductionStarted>(OnProductionStarted);
            Events?.Unsubscribe<OperationPerformed>(OnOperationPerformed);
            Events?.Unsubscribe<ProductionFinished>(OnProductionFinished);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Input?.Dispose();
            UI?.Clear();
            Audio?.StopAll();
            Events?.Clear();
            Instance = null;
        }

        private AudioSource PrepareSource(ref AudioSource source)
        {
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void OnProductionStarted(ProductionStarted message) => Audio.PlayUIEffect(buttonSound);
        private void OnOperationPerformed(OperationPerformed message) => Audio.PlaySceneEffect(operationSound);
        private void OnProductionFinished(ProductionFinished message) => Audio.PlaySceneEffect(completionSound);
    }
}
