using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DeathPanelFunction : MonoBehaviour
{
    public Volume greyPostprocess;
    public void PlayDeathSound()
    {
        Context.ss.Play("Wasted");
    }

    public void CreateGrayPostProcess()
    {
        greyPostprocess = Util.PostProcessUtil.CreateGrayScreen();
    }

    public void DestroyGreyPostProcess()
    {
        Destroy(greyPostprocess);
    }

    public void ResumeTimeScale()
    {
        Time.timeScale = 1.0f;
    }
}
