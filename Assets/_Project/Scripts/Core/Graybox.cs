using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    /// <summary>프로토타입용 그레이박스 도형·머티리얼 헬퍼.</summary>
    public static class Graybox
    {
        static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        static Shader litShader;

        public static Material Mat(Color c, bool emissive = false)
        {
            var key = emissive ? new Color(c.r, c.g, c.b, -1f) : c;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            // 빌드에서 셰이더 변형이 빠지지 않게 Resources의 베이스 머티리얼을 복제한다 (ProjectSetup.CreateBaseMaterials)
            string baseName = emissive ? "GrayboxEmissive" : c.a < 1f ? "GrayboxTransparent" : "GrayboxOpaque";
            var template = Resources.Load<Material>("Materials/" + baseName);
            if (template != null) m = new Material(template);
            else
            {
                if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(litShader);
            }
            m.color = c;
            if (emissive) m.SetColor("_EmissionColor", c * 2f);
            Cache[key] = m;
            return m;
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Color color, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>월드에 떠 있는 3D 텍스트 (간판, 표지판).</summary>
        public static TextMesh Label(Transform parent, string text, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UIStyle.Font;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = LabelMaterial;
            return tm;
        }

        static Material labelMat;

        /// <summary>
        /// 기본 폰트 머티리얼(GUI/Text Shader)은 깊이 테스트를 안 해서 간판이 벽을 뚫고 뒤집혀 보인다.
        /// 복사본에 ZTest LessEqual을 걸어 쓴다.
        /// </summary>
        static Material LabelMaterial
        {
            get
            {
                if (labelMat == null)
                {
                    labelMat = new Material(UIStyle.Font.material) { name = "LabelDepthTested" };
                    labelMat.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                    Font.textureRebuilt += f =>
                    {
                        if (f == UIStyle.Font && labelMat != null) labelMat.mainTexture = f.material.mainTexture;
                    };
                }
                return labelMat;
            }
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }
    }
}
