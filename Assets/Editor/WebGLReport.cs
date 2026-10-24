using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prints what is expensive to render in each built scene (for WebGL performance work).
// Batch mode: -executeMethod WebGLReport.Run   (writes Temp/webgl_report.txt)
public static class WebGLReport
{
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var s in EditorBuildSettings.scenes.Where(x => x.enabled))
        {
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int point = lights.Count(l => l.type != LightType.Directional), shadowed = lights.Count(l => l.shadows != LightShadows.None && l.type != LightType.Directional);
            var ps = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int maxParticles = ps.Sum(p => p.main.maxParticles);
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int skinned = renderers.Count(r => r is SkinnedMeshRenderer);
            long tris = 0; foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
            sb.AppendLine($"== {scene.name}: lights={lights.Length} (point/spot={point}, with shadows={shadowed}) particleSystems={ps.Length} maxParticles={maxParticles} renderers={renderers.Length} skinned={skinned} meshTris={tris / 1000}k");
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                sb.AppendLine($"   terrain {t.name}: size={t.terrainData.size} heightRes={t.terrainData.heightmapResolution} trees={t.terrainData.treeInstanceCount} treeDist={t.treeDistance} billboard={t.treeBillboardDistance} detailDist={t.detailObjectDistance} detailDensity={t.detailObjectDensity} pixelError={t.heightmapPixelError} detailLayers={t.terrainData.detailPrototypes.Length} shadows={t.shadowCastingMode}");
            foreach (var p in ps.OrderByDescending(p => p.main.maxParticles).Take(4)) sb.AppendLine($"   ps {p.name}: max={p.main.maxParticles} rate={p.emission.rateOverTime.constantMax}");
            var cam = Camera.main; if (cam) { var d = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); sb.AppendLine($"   camera far={cam.farClipPlane} post={(d ? d.renderPostProcessing.ToString() : "?")} AA={(d ? d.antialiasing.ToString() : "?")}"); }
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)) if (v.sharedProfile) sb.AppendLine($"   volume {v.sharedProfile.name}: " + string.Join(",", v.sharedProfile.components.Where(c => c.active).Select(c => c.GetType().Name)));
        }
        System.IO.File.WriteAllText("Temp/webgl_report.txt", sb.ToString());
        Debug.Log("[WebGLReport]\n" + sb);
    }
}
