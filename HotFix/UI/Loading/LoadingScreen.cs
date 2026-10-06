using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 这个是不受ui系统管制的，强制最上层。
public class LoadingScreen : UIBase
{
    Image daguai;
    Image wakuang;
    Image kanshu;
    Image currentShowingImage;
    public LoadingScreen() : base(R.Path.LoadingScenePanel)
    {
        Image[] allImg = UIPrefab.GetComponentsInChildren<Image>(true);
        foreach (var t in allImg)
        {
            if (t.name == "kanshu") kanshu = t;
            if (t.name == "daguai") daguai = t;
            if (t.name == "wakuang") wakuang = t;
        }
        currentShowingImage = daguai;
    }

    public override void Show()
    {
        base.Show();
        if (currentShowingImage == daguai)
        {
            SetImageEnableOrDisable(false, daguai);
            SetImageEnableOrDisable(true,wakuang);
        }
        else if (currentShowingImage == wakuang)
        {
            SetImageEnableOrDisable(false, wakuang);
            SetImageEnableOrDisable(true, kanshu);
        }
        else if (currentShowingImage == kanshu)
        {
            SetImageEnableOrDisable(false, kanshu);
            SetImageEnableOrDisable(true, daguai);
        }
        
        UIPrefab.SetActive(true);
        UIPrefab.transform.SetAsLastSibling();
    }

    private void SetImageEnableOrDisable(bool enable, Image target)
    {
        Color color = target.color;
        color.a = enable ? 1f : 0f;
        target.color = color;
        if (enable) currentShowingImage = target;
    }
}
