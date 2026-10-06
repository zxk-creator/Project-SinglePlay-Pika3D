using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using PK;
using UnityEngine.AddressableAssets;

// 管理游戏中音频的播放
public class SoundSystem : IDeathAndRevive, IEnterNewScene
{
    public enum EUISoundType
    {
        PRESS,
        CLOSE,
        HOVER
    }
    public enum EBGMSoundType
    {
        TITLE,
        CITY,
        NONE
    }
    public float gravity = 9.8f;
    // UI点击音效
    public AudioClip press;
    public AudioClip hover;
    public AudioClip close;
    // bgm
    public AudioClip titleBGM;
    public AudioClip cityBGM;
    // 音效池
    private AudioSource[] audioSources;
    private AudioSource BGM;
    

    public SoundSystem()
    {
        audioSources = new AudioSource[10];
        for (int i = 0;i < 10;i++)
        {
            var audioObj = new GameObject("音效池" + i);
            UnityEngine.Object.DontDestroyOnLoad(audioObj);
            audioSources[i] = audioObj.AddComponent<AudioSource>();
        }
        var bgmObj = new GameObject("BGM池");
        UnityEngine.Object.DontDestroyOnLoad(bgmObj);
        BGM = bgmObj.AddComponent<AudioSource>();
        BGM.loop = true;

        LocalPlayer.RegisterDeathAndReviveEvents(this);
        SceneController.RegisterEnterNewSceneCallback(this);

        // 可以永不卸载，这些都是生命周期跟随的
        press = ResourceHelp.LoadAudioNew("Click");
        close = ResourceHelp.LoadAudioNew("Close");
        cityBGM = ResourceHelp.LoadAudioNew("CommonBGM");
        titleBGM = ResourceHelp.LoadAudioNew("TitleBGM");
    }

    // 音量管理区
    public float UISoundVolume = 1.0f;
    public float BGMSoundVolume = 1.0f;
    public float SFXSoundVolume = 1.0f;

    public void Play(EUISoundType soundType)
    {
        switch(soundType)
        {
            case EUISoundType.PRESS:
                {
                    Play_Internal(press,UISoundVolume);
                    break;
                }
            case EUISoundType.CLOSE:
                {
                    Play_Internal(close,UISoundVolume);
                    break;
                }
            case EUISoundType.HOVER:
                {
                    if (hover == null) return;
                    Play_Internal(hover,UISoundVolume);
                    break;
                }
        }
    }

    /// <summary>
    /// 这里的name是Addressable里面配置的name
    /// </summary>
    public void Play(string name)
    {
        var sH = Addressables.LoadAssetAsync<AudioClip>(name);
        sH.WaitForCompletion();
        if (sH.Result == null)
        {
            PK.Log.NullPtr("Play Sound " + name);
            return;
        }

        Play_Internal(sH.Result,SFXSoundVolume);

        // 注册一个延迟任务，播完销毁
        Context.updateProxy.RegisterDelayTask(() =>
        {
            sH.Release();
        }, sH.Result.length);
    }

    public void PlayBGM(EBGMSoundType bgm)
    {
        switch (bgm)
        {
            case EBGMSoundType.TITLE:
                {
                    BGM.clip = titleBGM;
                    BGM.Play();
                    break;
                }
            case EBGMSoundType.CITY:
                {
                    BGM.clip = cityBGM;
                    BGM.Play();
                    break;
                }
                case EBGMSoundType.NONE:
                {
                    BGM.Stop();
                    break;
                }
        }
    }

    private void Play_Internal(AudioClip clip,float volume)
    {
        if (clip == null)
        {
            Log.NullPtr("SoundSystem");
            return;
        }
        for (int i = 0; i < audioSources.Length;i++)
        {
            if (!audioSources[i].isPlaying)
            {
                audioSources[i].PlayOneShot(clip,volume);
                return;
            }
        }
        float maxProgress = 0.0f;
        int idx = 0;
        // 走到这说明音效池用完了
        for (int i = 0; i < audioSources.Length; i++)
        {
            // 找快播完的
            float progress = audioSources[i].time / audioSources[i].clip.length;
            if (progress > maxProgress) {
                maxProgress = progress;
                idx = i;
            }
        }

        audioSources[idx].PlayOneShot(clip,volume);
    }

    public void OnBeginEnterNewScene(ESceneType newScene)
    {
    }

    public void OnNewSceneEntered(ESceneType newScene)
    {
        switch (newScene)
        {
            case ESceneType.CITY:
                {
                    PlayBGM(EBGMSoundType.CITY);
                    break;
                }
                case ESceneType.MAIN_MENU:
                {
                    PlayBGM(EBGMSoundType.TITLE);
                    break;
                }
        }
    }

    public void OnPlayerDeath()
    {
        PlayBGM(EBGMSoundType.NONE);
    }

    public void OnPlayerRevive()
    {
        PlayBGM(EBGMSoundType.CITY);
    }
}
