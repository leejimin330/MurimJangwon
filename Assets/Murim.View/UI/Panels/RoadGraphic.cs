using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>지역 사이 길을 정규화 좌표 선분으로 그린다.</summary>
    public class RoadGraphic : MaskableGraphic
    {
        public readonly List<(Vector2 a, Vector2 b)> Segments = new List<(Vector2, Vector2)>();
        public float Thickness = 4;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            foreach (var (a, b) in Segments)
            {
                var p = new Vector2(r.xMin + a.x * r.width, r.yMin + a.y * r.height);
                var q = new Vector2(r.xMin + b.x * r.width, r.yMin + b.y * r.height);
                var n = (q - p).normalized;
                var off = new Vector2(-n.y, n.x) * Thickness / 2;
                int i = vh.currentVertCount;
                vh.AddVert(p - off, color, Vector2.zero);
                vh.AddVert(p + off, color, Vector2.zero);
                vh.AddVert(q + off, color, Vector2.zero);
                vh.AddVert(q - off, color, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i, i + 2, i + 3);
            }
        }
    }
}
