using System;
using UnityEngine;

namespace Manyworld
{
    /// <summary>총알 궤적을 잠깐 보여 주는 선.</summary>
    public class Tracer : MonoBehaviour
    {
        float life = 0.06f;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.03f;
            lr.endWidth = 0.01f;
            lr.sharedMaterial = Graybox.Mat(color, true);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<Tracer>();
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
