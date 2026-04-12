using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public readonly struct UIResultWindowPresentation
{
    // 中文：结果窗只依赖这份稳定的展示模型，业务层不用知道内部按钮和文本节点。
    // English: the result window depends only on this stable presentation model, so gameplay code does not touch internal UI nodes.
    public string Title { get; }
    public string PrimaryButtonLabel { get; }

    public UIResultWindowPresentation(string title, string primaryButtonLabel)
    {
        Title = title ?? string.Empty;
        PrimaryButtonLabel = primaryButtonLabel ?? string.Empty;
    }
}

public class UIResultWindow : MonoBehaviour
{
    [SerializeField]
    private Button NextButton;

    [SerializeField]
    private TextMeshProUGUI TitleLabel;

    [SerializeField]
    private TextMeshProUGUI NextButtonLabel;

    [SerializeField]
    private GameObject[] Effects;

    private Action primaryAction;

    public bool IsVisible => gameObject.activeSelf;


    private void Awake()
    {
        CacheReferences();
        Hide();
    }

    private void OnDestroy()
    {
        if (NextButton != null)
        {
            NextButton.onClick.RemoveListener(HandlePrimaryButtonClicked);
        }
    }

    public void Show(in UIResultWindowPresentation presentation, Action onPrimaryButtonClicked)
    {
        // 中文：结果窗只接收一个纯展示模型，避免外部直接操作内部控件。
        // English: the result window only consumes a presentation model,
        // so outside callers do not need to know about its internal controls.
        CacheReferences();
        primaryAction = onPrimaryButtonClicked;

        if (TitleLabel != null)
        {
            TitleLabel.text = presentation.Title;
        }

        if (NextButtonLabel != null)
        {
            NextButtonLabel.text = presentation.PrimaryButtonLabel;
        }

        SetPrimaryButtonInteractable(true);
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void SetPrimaryButtonLabel(string label)
    {
        CacheReferences();
        if (NextButtonLabel != null)
        {
            NextButtonLabel.text = label ?? string.Empty;
        }
    }

    public void Hide()
    {
        primaryAction = null;
        SetPrimaryButtonInteractable(true);
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void SetPrimaryButtonInteractable(bool interactable)
    {
        if (NextButton != null)
        {
            NextButton.interactable = interactable;
        }
    }

    private void CacheReferences()
    {
        if (NextButton == null)
        {
            NextButton = GetComponentInChildren<Button>(true);
        }

        if (NextButton != null)
        {
            NextButton.onClick.RemoveListener(HandlePrimaryButtonClicked);
            NextButton.onClick.AddListener(HandlePrimaryButtonClicked);
        }

        if (NextButtonLabel == null && NextButton != null)
        {
            NextButtonLabel = NextButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (TitleLabel == null)
        {
            TitleLabel = FindOptionalText("Title");
        }
    }

    private TextMeshProUGUI FindOptionalText(string childName)
    {
        var child = transform.Find(childName);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<TextMeshProUGUI>();
    }

    private void HandlePrimaryButtonClicked()
    {
        primaryAction?.Invoke();
    }

    IEnumerator PlayEffect()
    {
        if (Effects != null && Effects.Length > 0 && Effects[0] != null)
        {
            var particleSystem = Effects[0].GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystem.Play();
            }
        }

        yield break;
    }
}
