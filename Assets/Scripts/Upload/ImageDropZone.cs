using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SuperviseSoft.Upload
{
  public sealed class ImageDropZone : MonoBehaviour
  {
    public Text hintText;
    [NonSerialized] public Action<string> ImagePathDropped;

    private RectTransform _rectTransform;

    private void Awake()
    {
      _rectTransform = GetComponent<RectTransform>();
    }

    public void SetHint(string message)
    {
      if (hintText != null)
      {
        hintText.text = message ?? string.Empty;
      }
    }

    private void OnGUI()
    {
#if UNITY_EDITOR
      if (!isActiveAndEnabled || _rectTransform == null)
      {
        return;
      }

      var current = Event.current;
      if (current == null || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
      {
        return;
      }

      if (!ContainsMouse(current.mousePosition))
      {
        return;
      }

      var imagePath = FindImagePath(DragAndDrop.paths);
      DragAndDrop.visualMode = string.IsNullOrWhiteSpace(imagePath) ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
      if (current.type == EventType.DragPerform && !string.IsNullOrWhiteSpace(imagePath))
      {
        DragAndDrop.AcceptDrag();
        ImagePathDropped?.Invoke(imagePath);
      }

      current.Use();
#endif
    }

#if UNITY_EDITOR
    private bool ContainsMouse(Vector2 guiMousePosition)
    {
      var worldCorners = new Vector3[4];
      _rectTransform.GetWorldCorners(worldCorners);
      var bottomLeft = RectTransformUtility.WorldToScreenPoint(null, worldCorners[0]);
      var topRight = RectTransformUtility.WorldToScreenPoint(null, worldCorners[2]);
      var screenMouse = new Vector2(guiMousePosition.x, Screen.height - guiMousePosition.y);
      var rect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
      return rect.Contains(screenMouse);
    }

    private static string FindImagePath(string[] paths)
    {
      if (paths == null)
      {
        return string.Empty;
      }

      foreach (var path in paths)
      {
        if (File.Exists(path) && IsSupportedImage(path))
        {
          return path;
        }
      }

      return string.Empty;
    }

    private static bool IsSupportedImage(string path)
    {
      var extension = Path.GetExtension(path)?.ToLowerInvariant();
      return extension == ".jpg" || extension == ".jpeg" || extension == ".png";
    }
#endif
  }
}
