using System.Text;
using SuperviseSoft.AI;
using SuperviseSoft.Tasks;
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

    public void ShowFinishSummary(TaskFinishSummary summary)
    {
      PanelVisibility.Show(root, gameObject);
      if (summary == null)
      {
        SetText(statusText, "本次任务已结束");
        SetText(summaryText, "没有返回统计结果。");
        SetText(estimatedMinutesText, string.Empty);
        SetText(suggestedStepsText, string.Empty);
        return;
      }

      SetText(statusText, "本次任务已结束，明细已清理");
      SetText(summaryText, $"{summary.title}\n共分析 {summary.itemCount} 项：图片 {summary.imageItemCount} 项，文字 {summary.textItemCount} 项。");
      SetText(estimatedMinutesText, $"AI 估算：{summary.aiEstimatedMinutes} 分钟 | 实际记录：{summary.actualMinutes} 分钟");
      SetText(suggestedStepsText, $"开始：{summary.startedAt}\n结束：{summary.finishedAt}\n持续：{summary.durationMinutes} 分钟\n\n任务组、上传文件记录、AI 明细已从 CloudBase 清理。");
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
