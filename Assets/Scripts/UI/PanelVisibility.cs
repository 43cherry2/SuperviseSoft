using UnityEngine;

namespace SuperviseSoft.UI
{
  internal static class PanelVisibility
  {
    public static void Show(GameObject root, GameObject fallback)
    {
      SetVisible(root != null ? root : fallback, true);
    }

    public static void Hide(GameObject root, GameObject fallback)
    {
      SetVisible(root != null ? root : fallback, false);
    }

    private static void SetVisible(GameObject target, bool visible)
    {
      if (target == null)
      {
        return;
      }

      target.SetActive(true);

      var canvasGroup = target.GetComponent<CanvasGroup>();
      if (canvasGroup == null)
      {
        canvasGroup = target.AddComponent<CanvasGroup>();
      }

      canvasGroup.alpha = visible ? 1f : 0f;
      canvasGroup.interactable = visible;
      canvasGroup.blocksRaycasts = visible;
    }
  }
}
