using UnityEngine;

namespace Cook.Managers
{
    /// <summary>播放器由 Unity 入口提供；不查询玩法、面板或配置单例。</summary>
    public sealed class GameAudioManager
    {
        private readonly AudioSource bgm;
        private readonly AudioSource ui;
        private readonly AudioSource scene;

        public GameAudioManager(AudioSource bgm, AudioSource ui, AudioSource scene)
        {
            this.bgm = bgm;
            this.ui = ui;
            this.scene = scene;
        }

        public void PlayBGM(AudioClip clip, float volume = 1f)
        {
            if (bgm == null || clip == null) return;
            bgm.clip = clip;
            bgm.loop = true;
            bgm.volume = Mathf.Clamp01(volume);
            bgm.Play();
        }

        public void StopBGM()
        {
            if (bgm != null) bgm.Stop();
        }

        public void PlayUIEffect(AudioClip clip, float volume = 1f) => PlayEffect(ui, clip, volume);
        public void PlaySceneEffect(AudioClip clip, float volume = 1f) => PlayEffect(scene, clip, volume);

        public void StopAll()
        {
            if (bgm != null) bgm.Stop();
            if (ui != null) ui.Stop();
            if (scene != null) scene.Stop();
        }

        private static void PlayEffect(AudioSource source, AudioClip clip, float volume)
        {
            if (source != null && clip != null) source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
