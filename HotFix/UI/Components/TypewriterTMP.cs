using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TypewriterTMP : MonoBehaviour
{
    public TMP_Text tmp;

    [Tooltip("一个字符出现速度：x 字每秒。此为默认，可覆盖")]
    public float apperSpeedOnechar = 1.0f;
    private Coroutine co;
    public bool clickToComplete = true;

    public void Play(string fullText, float apperSpeedOnechar = -1)
    {
        if (apperSpeedOnechar == -1) apperSpeedOnechar = this.apperSpeedOnechar;

        tmp.text = fullText;
        tmp.ForceMeshUpdate();
        tmp.maxVisibleCharacters = 0;
    }

    public void Skip()
    {
        tmp.maxVisibleCharacters = tmp.textInfo.characterCount;
    }

    private IEnumerator Typing()
    {
        int total = tmp.textInfo.characterCount;   // 总可见字符数（富文本标签不计入）
        while (tmp.maxVisibleCharacters < total)
        {
            tmp.maxVisibleCharacters++;            // 逐字出现
            yield return new WaitForSeconds(apperSpeedOnechar / 1000f);
        }
    }
}

