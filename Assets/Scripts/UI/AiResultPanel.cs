using System.Text;
using SuperviseSoft.AI;
using UnityEngine;
using UnityEngine.UI;

namespace SuperviseSoft.UI
{
  public sealed class AiResultPanel : MonoBehaviour
  {
    public GameObject root;
    public Text statusText;
    public Text summaryText;
    public Text estimatedMinutesText;
    public Text suggestedStepsText;
    public Button closeButton;

    private bool _bound;

    private void Awake()
    {
      Bind();
    }

    private void Start()
    {
      Bind();
    }

    public void Bind()
    {
      if (_bound)
      {
        return;
      }

      if (closeButton == null)
      {
        return;
      }

      _bound = true;
      closeButton.onClick.AddListener(Hide);
    }

    public void ShowResult(AiResult result, AiJob job)
    {
      PanelVisibility.Show(root, gameObject);
      SetText(statusText, $"AI 状态：{job?.status ?? "success"}");
      if (result == null)
      {
        SetText(summaryText, "暂无分析结果。");
        SetText(estimatedMinutesText, string.Empty);
        SetText(suggestedStepsText, string.Empty);
        return;
      }

      SetText(summaryText, result.summary);
      SetText(estimatedMinutesText, $"预计用时：{result.estimatedMinutes} 分钟");
      SetText(suggestedStepsText, BuildStepsText(result.suggestedSteps));
    }

    public void Hide()
    {
      PanelVisibility.Hide(root, gameObject);
    }

    private static string BuildStepsText(SuggestedStep[] steps)
    {
      if (steps == null || steps.Length == 0)
      {
        return "暂无建议步骤。";
      }

      var builder = new StringBuilder();
      for (var i = 0; i < steps.Length; i++)
      {
        var step = steps[i];
        builder.Append(i + 1)
          .Append(". ")
          .Append(step.title)
          .Append(" - ")
          .Append(step.minutes)
          .AppendLine(" 分钟");
        builder.Append(step.description).AppendLine();
      }

      return builder.ToString();
    }

    private static void SetText(Text text, string value)
    {
      if (text != null)
      {
        text.text = value ?? string.Empty;
      }
    }
  }
}
