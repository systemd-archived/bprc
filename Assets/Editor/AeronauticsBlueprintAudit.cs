using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class AeronauticsBlueprintAudit
{
    private static readonly List<string> Report = new List<string>();
    private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
    private static readonly List<string> Errors = new List<string>();
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private static int failures;
    private static Camera camera;
    private static Contraption machine;
    private static INBlueprintPartEffect effect;
    private static string output;

    private static T Own<T>(T value) where T : UnityEngine.Object { Owned.Add(value); return value; }
    private static GameObject New(string name) => Own(new GameObject(name));
    private static void Check(bool pass, string message)
    {
        Report.Add((pass ? "PASS " : "FAIL ") + message);
        if (!pass) failures++;
    }
    private static void OnLog(string text, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(text);
    }
    private static MeshRenderer Part(string name, Vector3 position, Texture2D texture, Vector3 scale)
    {
        var part = New(name);
        part.transform.SetParent(machine.transform, false);
        part.transform.position = position;
        part.AddComponent<BasePart>();
        var visual = New(name + " visual");
        visual.transform.SetParent(part.transform, false);
        visual.transform.localScale = scale;
        var mesh = Own(new Mesh());
        mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
        mesh.uv = new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
        mesh.triangles = new[] { 0,1,2,2,3,0 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        var material = Own(new Material(Shader.Find("_Custom/PreAlpha_Unlit_ColorTransparent_Geometry")));
        material.mainTexture = texture;
        renderer.sharedMaterial = material;
        return renderer;
    }
    private static Texture2D Pattern()
    {
        var texture = Own(new Texture2D(128,128,TextureFormat.RGBA32,false));
        texture.filterMode = FilterMode.Point;
        for (int y=0; y<128; y++)
        for (int x=0; x<128; x++)
        {
            float radius = new Vector2(x-63.5f,y-63.5f).magnitude;
            float alpha = radius > 61 || radius < 16 ? 0 : 1;
            float shade = x / 127f * .5f + .05f;
            texture.SetPixel(x,y,new Color(shade,shade*.8f,shade*.4f,alpha));
        }
        texture.Apply();
        return texture;
    }
    private static Texture2D Render(string filename, int width=640, int height=360, int samples=1)
    {
        var target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32) { antiAliasing=samples };
        var previous = RenderTexture.active;
        var picture = Own(new Texture2D(width,height,TextureFormat.RGBA32,false));
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            picture.ReadPixels(new Rect(0,0,width,height),0,0);
            picture.Apply();
            if (filename != null) File.WriteAllBytes(Path.Combine(output,filename),picture.EncodeToPNG());
            return picture;
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    private static int Difference(Texture2D a, Texture2D b, int x0=0, int y0=0, int width=-1, int height=-1)
    {
        width = width < 0 ? a.width : width; height = height < 0 ? a.height : height;
        int count = 0;
        for (int y=y0; y<y0+height; y++)
        for (int x=x0; x<x0+width; x++)
        {
            Color ca=a.GetPixel(x,y), cb=b.GetPixel(x,y);
            if (Mathf.Abs(ca.r-cb.r)+Mathf.Abs(ca.g-cb.g)+Mathf.Abs(ca.b-cb.b) > .025f) count++;
        }
        return count;
    }
    public static void Run()
    {
        if (!Application.isBatchMode || !File.Exists(".bprc-aeronautics-audit")) throw new InvalidOperationException("Use the isolated blueprint audit runner.");
        output = Path.GetFullPath("Builds/QA/AeronauticsBlueprint"); Directory.CreateDirectory(output);
        Report.Clear(); Errors.Clear(); failures=0;
        Report.Add("Aeronautics blueprint / Unity " + Application.unityVersion + " / " + SystemInfo.graphicsDeviceType);
        var previousSettings = INUserSettings.Instance;
        var previousLevel = typeof(WPFMonoBehaviour).GetField("s_levelManager",Static).GetValue(null);
        Application.logMessageReceived += OnLog;
        try
        {
            var settings = new GeneralSettings();
            Check(!settings.AeronauticsBlueprint,"setting defaults to off");
            settings.AeronauticsBlueprint=true;
            var copy = new GeneralSettings(settings);
            Check(copy.AeronauticsBlueprint,"settings copy preserves enabled state");
            var restored = JsonConvert.DeserializeObject<GeneralSettings>(JsonConvert.SerializeObject(settings));
            Check(restored.AeronauticsBlueprint,"JSON round trip preserves enabled state");
            restored.Update(new GeneralSettings());
            Check(!restored.AeronauticsBlueprint,"default reset restores disabled state");
            var locale = JObject.Parse(File.ReadAllText("Assets/INLocalizationData.json"));
            bool localized=false;
            foreach (var item in locale["data"])
                if ((string)item["id"] == "GeneralSettings_AeronauticsBlueprint")
                    localized = !string.IsNullOrEmpty((string)item["text"]["zh-Hans"]) && !string.IsNullOrEmpty((string)item["text"]["en-US"]);
            Check(localized,"Chinese and English toggle labels exist");
            camera = New("Blueprint audit camera").AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=3;
            camera.transform.position=new Vector3(0,0,-10);
            camera.nearClipPlane=.1f; camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.13f,.27f,.42f);
            camera.allowHDR=false; camera.allowMSAA=true;
            machine = New("Blueprint audit machine").AddComponent<Contraption>();
            var level = New("Blueprint audit level").AddComponent<LevelManager>();
            var mode = new BaseGameMode();
            typeof(GameMode).GetProperty("ContraptionProto").SetValue(mode,machine);
            typeof(LevelManager).GetField("m_gameMode",Private).SetValue(level,mode);
            typeof(WPFMonoBehaviour).GetField("s_levelManager",Static).SetValue(null,level);
            typeof(INUserSettings).GetField("<Instance>k__BackingField",Static).SetValue(null,new INUserSettings { GeneralSettings=settings });
            var texture = Pattern();
            var wheel = Part("Cutout wheel",new Vector3(-1,0,0),texture,new Vector3(2.5f,2.5f,1));
            var panel = Part("Tinted panel",new Vector3(1.6f,.15f,-.1f),Texture2D.whiteTexture,new Vector3(1.5f,2,1));
            panel.sharedMaterial.color=new Color(.2f,.28f,.1f,1);
            var untouched = Part("Unrelated sprite",new Vector3(0,2.35f,0),Texture2D.whiteTexture,new Vector3(1,.45f,1));
            untouched.transform.parent.SetParent(null,true);
            var occluder = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            occluder.name="Opaque foreground"; occluder.transform.position=new Vector3(-1.9f,-.7f,-1);
            occluder.transform.localScale=new Vector3(.4f,1.2f,.1f);
            var cover=Own(new Material(Shader.Find("Unlit/Color"))); cover.color=new Color(.8f,.1f,.15f);
            occluder.GetComponent<Renderer>().sharedMaterial=cover;
            effect=INBlueprintPartEffect.Attach(camera);
            INBlueprintPartEffect.SetEnabled(false);
            var baseline=Render("blueprint-off.png");
            INBlueprintPartEffect.SetEnabled(true);
            var enabled=Render("blueprint-on.png");
            Check(Difference(baseline,enabled)>5000,"part pixels change to diagram palette");
            Check(Difference(baseline,enabled,0,0,100,360)==0,"uncovered scene background is unchanged");
            Check(Difference(baseline,enabled,295,309,50,24)==0,"unrelated foreground sprite is unchanged");
            Check(Difference(baseline,enabled,250,170,20,20)==0,"transparent wheel hub remains a hole");
            Check(Difference(baseline,enabled,201,125,8,40)==0,"opaque foreground keeps occluding the part");
            Check(wheel.sharedMaterial.shader.name=="_Custom/PreAlpha_Unlit_ColorTransparent_Geometry","original part material is preserved");
            var block = new MaterialPropertyBlock(); block.SetColor("_Color",new Color(1,1,1,0)); panel.SetPropertyBlock(block);
            var invisible=Render("blueprint-property-block.png");
            Color hiddenPixel=invisible.GetPixel(418,180), background=baseline.GetPixel(20,20);
            Check(Mathf.Abs(hiddenPixel.r-background.r)+Mathf.Abs(hiddenPixel.g-background.g)+Mathf.Abs(hiddenPixel.b-background.b)<.01f,"live property block can hide a part");
            panel.SetPropertyBlock(null);
            panel.sharedMaterial.color=new Color(.2f,.28f,.1f,.5f);
            var translucentOn=Render("blueprint-translucent.png");
            INBlueprintPartEffect.SetEnabled(false); var translucentOff=Render(null);
            INBlueprintPartEffect.SetEnabled(true);
            Check(Difference(translucentOff,translucentOn,413,170,12,20)>100,"half-transparent part remains eligible for blueprint rendering");
            panel.sharedMaterial.color=new Color(.2f,.28f,.1f,1);
            var added=Part("Dynamically added part",new Vector3(0,-2,0),Texture2D.whiteTexture,new Vector3(.7f,.6f,1));
            added.sharedMaterial.color=new Color(.1f,.35f,.15f);
            var dynamicOn=Render("blueprint-dynamic.png");
            INBlueprintPartEffect.SetEnabled(false); var dynamicOff=Render(null);
            Check(Difference(dynamicOff,dynamicOn,309,50,20,20)>100,"new part is picked up without toggling");
            UnityEngine.Object.DestroyImmediate(added.transform.parent.gameObject);
            INBlueprintPartEffect.SetEnabled(true);
            var resized=Render("blueprint-mobile.png",360,640);
            Check(resized.GetPixel(0,0).b>.2f,"portrait resize keeps the scene nonblank");
            var msaa=Render("blueprint-msaa.png",640,360,4);
            Check(Difference(baseline,msaa)>5000 && Difference(baseline,msaa,201,125,8,40)==0,"MSAA rendering keeps the effect and foreground occlusion");
            INBlueprintPartEffect.SetEnabled(false);
            var disabled=Render("blueprint-restored.png");
            Check(Difference(baseline,disabled)==0,"turning off restores the complete original frame");
            Check(camera.GetCommandBuffers(CameraEvent.BeforeImageEffects).Length==0,"turning off releases the mask command buffer");
            Check(typeof(INBlueprintPartEffect).GetField("m_mask",Private).GetValue(effect)==null,"turning off releases render textures");
            INBlueprintPartEffect.SetEnabled(true);
            Check(Difference(baseline,Render(null))>5000,"effect can be enabled again after resource disposal");
            Type imageEffect=typeof(AeronauticsBlueprintAudit).Assembly.GetType("BlueprintAuditImageEffect");
            if (imageEffect!=null)
            {
                var existing=camera.gameObject.AddComponent(imageEffect);
                var combined=Render("blueprint-post-effects.png");
                Check(Difference(baseline,combined,0,0,100,360)==0 && Difference(baseline,combined)>5000,"blueprint works with a following camera image effect");
                UnityEngine.Object.DestroyImmediate(existing);
            }
            machine.gameObject.SetActive(false);
            var emptyOn=Render(null);
            INBlueprintPartEffect.SetEnabled(false); var emptyOff=Render(null);
            Check(Difference(emptyOn,emptyOff)==0,"empty machine scene retains the original frame");
            machine.gameObject.SetActive(true); INBlueprintPartEffect.SetEnabled(true);
            foreach (string resource in new[] { "PartMask","BlueprintComposite" })
            {
                var shader=Resources.Load<Shader>("AeronauticsBlueprint/"+resource);
                Check(shader!=null && shader.isSupported,"shader resource is included and supported: "+resource);
                int shaderErrors=0;
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity.ToString()=="Error") { shaderErrors++; Report.Add(message.message); }
                Check(shaderErrors==0,"shader compiles: "+resource);
            }
        }
        catch(Exception e) { Check(false,e.ToString()); }
        finally
        {
            INBlueprintPartEffect.SetEnabled(false);
            for(int i=Owned.Count-1;i>=0;i--) if(Owned[i]!=null) UnityEngine.Object.DestroyImmediate(Owned[i]);
            Owned.Clear();
            typeof(INUserSettings).GetField("<Instance>k__BackingField",Static).SetValue(null,previousSettings);
            typeof(WPFMonoBehaviour).GetField("s_levelManager",Static).SetValue(null,previousLevel);
            Application.logMessageReceived-=OnLog;
        }
        foreach(string error in Errors) Report.Add("UNITY ERROR "+error);
        Check(Errors.Count==0,"no Unity rendering errors");
        Report.Add("Failures: "+failures);
        File.WriteAllLines(Path.Combine(output,"audit.txt"),Report);
        Debug.Log(string.Join("\n",Report));
        EditorApplication.Exit(failures==0?0:1);
    }
}
