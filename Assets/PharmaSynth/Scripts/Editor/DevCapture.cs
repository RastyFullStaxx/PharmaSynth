#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Dev-only capture bridge: renders a one-off camera to a PNG on disk so
/// out-of-editor tooling can see the scene (the MCP scene-preview capture is
/// broken on this machine). Pose/output come from Temp/dev-capture-request.json
/// when present; defaults to the player spawn head pose.
public static class DevCapture
{
    [System.Serializable]
    public class Request
    {
        public float px, py = 1.58f, pz = 0.8f;   // camera position
        public float yaw = 180f, pitch;            // rotation (degrees)
        public float fov = 75f;
        public int width = 1280, height = 720;
        public string output = "";                 // absolute png path ("" = Temp/dev-capture.png)
    }

    public const string RequestPath = "Temp/dev-capture-request.json";

    [MenuItem("Tools/PharmaSynth/Dev Capture")]
    public static void Capture()
    {
        var req = new Request();
        if (File.Exists(RequestPath))
        {
            var loaded = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            if (loaded != null) req = loaded;
        }
        if (string.IsNullOrEmpty(req.output))
            req.output = Path.GetFullPath("Temp/dev-capture.png");

        var go = new GameObject("~DevCaptureCam");
        try
        {
            var cam = go.AddComponent<Camera>();
            go.transform.position = new Vector3(req.px, req.py, req.pz);
            go.transform.rotation = Quaternion.Euler(req.pitch, req.yaw, 0f);
            cam.fieldOfView = req.fov;
            cam.nearClipPlane = 0.05f;

            // A bare URP camera defaults post-processing OFF, so a capture would show the
            // scene WITHOUT the volume stack the player actually sees. Opt in, and match the
            // pipeline's MSAA so thin glass edges alias in the capture the way they do
            // in the headset.
            var camData = go.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = go.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            int msaa = 1;
            var urp = UniversalRenderPipeline.asset;
            if (urp != null) msaa = Mathf.Max(1, urp.msaaSampleCount);

            var rt = new RenderTexture(req.width, req.height, 24) { antiAliasing = msaa };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(req.width, req.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, req.width, req.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);

            File.WriteAllBytes(req.output, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[DevCapture] wrote " + req.output);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
#endif
