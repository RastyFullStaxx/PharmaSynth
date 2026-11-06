using UnityEngine;

/// A liquid puddle left on the ground by a spilled/broken vessel:
/// it lingers ~3 s, then smoothly fades away.
/// Spawn() drops a flat translucent disc at the impact point (raycast to the floor),
/// tinted with the spilled chemical's colour; it lingers, then fades out and
/// destroys itself. Purely visual — the graded penalty + supply loss already
/// happen in SpillMistake/LiquidPourer/BreakableGlassware.
public class SpillPuddle : MonoBehaviour
{
    [SerializeField] private float lingerSeconds = 3f;
    [SerializeField] private float fadeSeconds = 1.2f;

    private Material _mat;
    private Color _base;
    private float _age;

    /// Pure fade curve for tests: 1 while lingering, → 0 across the fade window.
    public static float Alpha01(float age, float linger, float fade)
    {
        if (age <= linger) return 1f;
        if (fade <= 0f) return 0f;
        return Mathf.Clamp01(1f - (age - linger) / fade);
    }

    /// Drop a puddle under worldPos (raycasts to the surface below; no-op if none).
    public static SpillPuddle Spawn(Vector3 worldPos, Color liquidColor, float radius = 0.16f)
    {
        Vector3 ground;
        if (Physics.Raycast(worldPos + Vector3.up * 0.05f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            ground = hit.point + Vector3.up * 0.005f;   // just proud of the surface
        else
            ground = new Vector3(worldPos.x, worldPos.y - 0.02f, worldPos.z);

        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "SpillPuddle";
        Destroy(go.GetComponent<Collider>());           // visual only
        go.transform.position = ground;
        // squashed disc, slightly irregular so repeated spills don't look cloned
        float rx = radius * Random.Range(0.85f, 1.15f);
        float rz = radius * Random.Range(0.85f, 1.15f);
        go.transform.localScale = new Vector3(rx * 2f, 0.0025f, rz * 2f);
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        var puddle = go.AddComponent<SpillPuddle>();
        var r = go.GetComponent<Renderer>();
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh);
        // transparent surface
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.SetFloat("_Smoothness", 0.9f);                // wet sheen
        var c = liquidColor; c.a = 0.62f;
        m.SetColor("_BaseColor", c);
        r.material = m;
        puddle._mat = m;
        puddle._base = c;
        return puddle;
    }

    private void Update()
    {
        _age += Time.deltaTime;
        float a = Alpha01(_age, lingerSeconds, fadeSeconds);
        if (_mat != null)
        {
            var c = _base; c.a = _base.a * a;
            _mat.SetColor("_BaseColor", c);
        }
        if (_age >= lingerSeconds + fadeSeconds)
        {
            if (_mat != null) Destroy(_mat);
            Destroy(gameObject);
        }
    }
}
