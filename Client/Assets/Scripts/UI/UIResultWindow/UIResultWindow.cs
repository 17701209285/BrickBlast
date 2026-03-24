using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UIResultWindow : MonoBehaviour
{
    [SerializeField]
    private Button NextButton;

    [SerializeField]
    private TextMeshProUGUI TitleLabel;

    [SerializeField]
    private TextMeshProUGUI NextButtonLabel;

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

    public void Show(bool isVictory, bool canAdvanceToNextLevel, Action onPrimaryButtonClicked)
    {
        CacheReferences();
        primaryAction = onPrimaryButtonClicked;

        if (TitleLabel != null)
        {
            TitleLabel.text = isVictory ? "通关成功" : "挑战失败";
        }

        if (NextButtonLabel != null)
        {
            NextButtonLabel.text = isVictory && canAdvanceToNextLevel ? "下一关" : "重新开始";
        }

        SetPrimaryButtonInteractable(true);
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
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
}
