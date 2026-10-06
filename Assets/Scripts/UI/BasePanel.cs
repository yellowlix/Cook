using UnityEngine;

namespace Cook.UI
{
    /// <summary>场景已有面板的最小生命周期，不包含玩法状态。</summary>
    public abstract class BasePanel : MonoBehaviour
    {
        public bool IsShow => gameObject.activeInHierarchy;
        public virtual void Show() => gameObject.SetActive(true);
        public virtual void Hide() => gameObject.SetActive(false);
    }
}
