using UnityEngine;
using TMPro;
public partial class UIChessBoard : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI SourceText;


    public void SetSource(string source)
    {
        EnsureSourceText();
        if (SourceText == null)
        {
            return;
        }

        SourceText.SetText(source ?? "0");
    }

    public void SetScore(int score)
    {
        SetSource(Mathf.Max(0, score).ToString());
    }

    private void EnsureSourceText()
    {
        if (SourceText != null)
        {
            return;
        }

        var scoreTransform = transform.Find("Score");
        if (scoreTransform != null)
        {
            SourceText = scoreTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
