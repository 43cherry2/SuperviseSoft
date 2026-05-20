using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TaskNormalizedLandmark = Mediapipe.Tasks.Components.Containers.NormalizedLandmark;

namespace SuperviseSoft.Mediapipe
{
  public sealed class StudyMonitorLandmarkOverlay : MaskableGraphic
  {
    private static readonly (int, int)[] PoseConnections =
    {
      (0, 1), (1, 2), (2, 3), (3, 7),
      (0, 4), (4, 5), (5, 6), (6, 8),
      (9, 10),
      (11, 13), (13, 15), (15, 17), (15, 19), (15, 21), (17, 19),
      (12, 14), (14, 16), (16, 18), (16, 20), (16, 22), (18, 20),
      (11, 12), (12, 24), (24, 23), (23, 11),
      (23, 25), (25, 27), (27, 29), (27, 31), (29, 31),
      (24, 26), (26, 28), (28, 30), (28, 32), (30, 32),
    };

    private static readonly (int, int)[] HandConnections =
    {
      (0, 1), (1, 2), (2, 3), (3, 4),
      (0, 5), (5, 6), (6, 7), (7, 8),
      (5, 9), (9, 10), (10, 11), (11, 12),
      (9, 13), (13, 14), (14, 15), (15, 16),
      (13, 17), (0, 17), (17, 18), (18, 19), (19, 20),
    };

    private static readonly (int, int)[] FaceConnections =
    {
      (10, 338), (338, 297), (297, 332), (332, 284), (284, 251), (251, 389),
      (389, 356), (356, 454), (454, 323), (323, 361), (361, 288), (288, 397),
      (397, 365), (365, 379), (379, 378), (378, 400), (400, 377), (377, 152),
      (152, 148), (148, 176), (176, 149), (149, 150), (150, 136), (136, 172),
      (172, 58), (58, 132), (132, 93), (93, 234), (234, 127), (127, 162),
      (162, 21), (21, 54), (54, 103), (103, 67), (67, 109), (109, 10),
      (33, 7), (7, 163), (163, 144), (144, 145), (145, 153), (153, 154),
      (154, 155), (155, 133), (33, 246), (246, 161), (161, 160), (160, 159),
      (159, 158), (158, 157), (157, 173), (173, 133),
      (46, 53), (53, 52), (52, 65), (65, 55), (70, 63), (63, 105),
      (105, 66), (66, 107),
      (263, 249), (249, 390), (390, 373), (373, 374), (374, 380), (380, 381),
      (381, 382), (382, 362), (263, 466), (466, 388), (388, 387), (387, 386),
      (386, 385), (385, 384), (384, 398), (398, 362),
      (276, 283), (283, 282), (282, 295), (295, 285), (300, 293), (293, 334),
      (334, 296), (296, 336),
      (78, 95), (95, 88), (88, 178), (178, 87), (87, 14), (14, 317),
      (317, 402), (402, 318), (318, 324), (324, 308), (78, 191), (191, 80),
      (80, 81), (81, 82), (82, 13), (13, 312), (312, 311), (311, 310),
      (310, 415), (415, 308),
      (61, 146), (146, 91), (91, 181), (181, 84), (84, 17), (17, 314),
      (314, 405), (405, 321), (321, 375), (375, 291), (61, 185), (185, 40),
      (40, 39), (39, 37), (37, 0), (0, 267), (267, 269), (269, 270),
      (270, 409), (409, 291),
    };

    [SerializeField] private Color _posePointColor = new Color(0.05f, 1f, 0.35f, 0.95f);
    [SerializeField] private Color _poseLineColor = new Color(0.05f, 0.9f, 1f, 0.8f);
    [SerializeField] private Color _facePointColor = new Color(1f, 0.85f, 0.1f, 0.8f);
    [SerializeField] private Color _faceLineColor = new Color(1f, 0.55f, 0.1f, 0.65f);
    [SerializeField] private Color _irisLineColor = new Color(0.35f, 0.95f, 1f, 0.8f);
    [SerializeField] private Color _handPointColor = new Color(1f, 0.25f, 0.85f, 0.9f);
    [SerializeField] private Color _handLineColor = new Color(0.95f, 0.2f, 1f, 0.75f);
    [SerializeField] private float _posePointRadius = 4f;
    [SerializeField] private float _facePointRadius = 2f;
    [SerializeField] private float _handPointRadius = 3.5f;
    [SerializeField] private float _poseLineWidth = 3f;
    [SerializeField] private float _faceLineWidth = 1.3f;
    [SerializeField] private float _irisLineWidth = 1.5f;
    [SerializeField] private float _handLineWidth = 2.4f;

    private readonly List<Vector2> _posePoints = new();
    private readonly List<bool> _poseValid = new();
    private readonly List<Vector2> _facePoints = new();
    private readonly List<bool> _faceValid = new();
    private readonly List<List<Vector2>> _handPoints = new();
    private readonly List<List<bool>> _handValid = new();

    protected override void Awake()
    {
      base.Awake();
      raycastTarget = false;
    }

    public void Draw(
      IReadOnlyList<TaskNormalizedLandmark> poseLandmarks,
      IReadOnlyList<TaskNormalizedLandmark> faceLandmarks,
      IReadOnlyList<IReadOnlyList<TaskNormalizedLandmark>> handLandmarks)
    {
      CopyLandmarks(poseLandmarks, _posePoints, _poseValid);
      CopyLandmarks(faceLandmarks, _facePoints, _faceValid);
      CopyHands(handLandmarks);
      SetVerticesDirty();
    }

    public void Clear()
    {
      _posePoints.Clear();
      _poseValid.Clear();
      _facePoints.Clear();
      _faceValid.Clear();
      _handPoints.Clear();
      _handValid.Clear();
      SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
      vh.Clear();

      DrawConnections(vh, _facePoints, _faceValid, FaceConnections, _faceLineColor, _faceLineWidth);
      DrawConnections(vh, _posePoints, _poseValid, PoseConnections, _poseLineColor, _poseLineWidth);
      DrawIris(vh, 468);
      DrawIris(vh, 473);

      for (var i = 0; i < _handPoints.Count; i++)
      {
        DrawConnections(vh, _handPoints[i], _handValid[i], HandConnections, _handLineColor, _handLineWidth);
      }

      DrawPoints(vh, _facePoints, _faceValid, _facePointColor, _facePointRadius);
      DrawPoints(vh, _posePoints, _poseValid, _posePointColor, _posePointRadius);

      for (var i = 0; i < _handPoints.Count; i++)
      {
        DrawPoints(vh, _handPoints[i], _handValid[i], _handPointColor, _handPointRadius);
      }
    }

    private void CopyLandmarks(IReadOnlyList<TaskNormalizedLandmark> source, List<Vector2> points, List<bool> valid)
    {
      points.Clear();
      valid.Clear();

      if (source == null)
      {
        return;
      }

      var rect = rectTransform.rect;
      for (var i = 0; i < source.Count; i++)
      {
        var landmark = source[i];
        var isValid = IsFinite(landmark.x) && IsFinite(landmark.y) &&
          landmark.x >= -0.5f && landmark.x <= 1.5f &&
          landmark.y >= -0.5f && landmark.y <= 1.5f;

        points.Add(new Vector2(
          Mathf.LerpUnclamped(rect.xMin, rect.xMax, landmark.x),
          Mathf.LerpUnclamped(rect.yMax, rect.yMin, landmark.y)));
        valid.Add(isValid);
      }
    }

    private void CopyHands(IReadOnlyList<IReadOnlyList<TaskNormalizedLandmark>> source)
    {
      var handCount = source?.Count ?? 0;
      while (_handPoints.Count < handCount)
      {
        _handPoints.Add(new List<Vector2>());
        _handValid.Add(new List<bool>());
      }

      while (_handPoints.Count > handCount)
      {
        _handPoints.RemoveAt(_handPoints.Count - 1);
        _handValid.RemoveAt(_handValid.Count - 1);
      }

      for (var i = 0; i < handCount; i++)
      {
        CopyLandmarks(source[i], _handPoints[i], _handValid[i]);
      }
    }

    private static void DrawConnections(VertexHelper vh, IReadOnlyList<Vector2> points, IReadOnlyList<bool> valid,
      IReadOnlyList<(int, int)> connections, Color drawColor, float width)
    {
      for (var i = 0; i < connections.Count; i++)
      {
        var (start, end) = connections[i];
        if (start < 0 || end < 0 || start >= points.Count || end >= points.Count)
        {
          continue;
        }

        if (!valid[start] || !valid[end])
        {
          continue;
        }

        AddLine(vh, points[start], points[end], width, drawColor);
      }
    }

    private void DrawIris(VertexHelper vh, int startIndex)
    {
      if (_facePoints.Count <= startIndex + 4)
      {
        return;
      }

      for (var i = startIndex; i <= startIndex + 4; i++)
      {
        if (!_faceValid[i])
        {
          return;
        }
      }

      var radiusA = Vector2.Distance(_facePoints[startIndex + 1], _facePoints[startIndex + 3]);
      var radiusB = Vector2.Distance(_facePoints[startIndex + 2], _facePoints[startIndex + 4]);
      AddCircleOutline(vh, _facePoints[startIndex], (radiusA + radiusB) * 0.25f, _irisLineWidth, _irisLineColor);
    }

    private static void DrawPoints(VertexHelper vh, IReadOnlyList<Vector2> points, IReadOnlyList<bool> valid, Color drawColor, float radius)
    {
      for (var i = 0; i < points.Count; i++)
      {
        if (valid[i])
        {
          AddCircle(vh, points[i], radius, drawColor);
        }
      }
    }

    private static void AddCircleOutline(VertexHelper vh, Vector2 center, float radius, float width, Color drawColor)
    {
      const int segments = 32;
      if (radius <= 0.01f)
      {
        return;
      }

      var previous = center + new Vector2(radius, 0f);
      for (var i = 1; i <= segments; i++)
      {
        var angle = Mathf.PI * 2f * i / segments;
        var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        AddLine(vh, previous, next, width, drawColor);
        previous = next;
      }
    }

    private static void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float width, Color drawColor)
    {
      var direction = end - start;
      if (direction.sqrMagnitude < 0.0001f)
      {
        return;
      }

      var normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
      var index = vh.currentVertCount;
      vh.AddVert(start - normal, drawColor, Vector2.zero);
      vh.AddVert(start + normal, drawColor, Vector2.zero);
      vh.AddVert(end + normal, drawColor, Vector2.zero);
      vh.AddVert(end - normal, drawColor, Vector2.zero);
      vh.AddTriangle(index, index + 1, index + 2);
      vh.AddTriangle(index, index + 2, index + 3);
    }

    private static void AddCircle(VertexHelper vh, Vector2 center, float radius, Color drawColor)
    {
      const int segments = 10;
      var centerIndex = vh.currentVertCount;
      vh.AddVert(center, drawColor, Vector2.zero);

      for (var i = 0; i <= segments; i++)
      {
        var angle = Mathf.PI * 2f * i / segments;
        var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        vh.AddVert(point, drawColor, Vector2.zero);
      }

      for (var i = 1; i <= segments; i++)
      {
        vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
      }
    }

    private static bool IsFinite(float value)
    {
      return !float.IsNaN(value) && !float.IsInfinity(value);
    }
  }
}
