using System;
using System.Reflection;
using UnityEngine;

public static class DevelopmentReflectorAudit
{
    public static void Run()
    {
        var root = new GameObject("ReflectorAudit");
        try
        {
            var reflector = root.AddComponent<DevelopmentReflector>();
            var cameraObject = new GameObject("AuditCamera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            var target = GameObject.CreatePrimitive(PrimitiveType.Quad);
            target.name = "AuditSprite";
            target.transform.SetParent(root.transform);
            MethodInfo scan = typeof(DevelopmentReflector).GetMethod("Scan", BindingFlags.NonPublic | BindingFlags.Instance);
            scan.Invoke(reflector, null);
            var entries = (System.Collections.IList)typeof(DevelopmentReflector)
                .GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reflector);
            if (entries.Count == 0) throw new Exception("Reflector did not detect visible renderer");
            var consoleObject = new GameObject("AuditCommandConsole");
            consoleObject.transform.SetParent(root.transform);
            consoleObject.SetActive(false);
            var commandManager = consoleObject.AddComponent<INCommandManager>();
            var writer = new INCommandManager.ScriptWriter();
            typeof(INCommandManager).GetField("m_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(commandManager, writer);
            typeof(Singleton<INCommandManager>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, commandManager);
            MethodInfo select = typeof(DevelopmentReflector).GetMethod("SelectAt", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo text = typeof(DevelopmentReflector).GetField("reportText", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo visible = typeof(DevelopmentReflector).GetField("reportVisible", BindingFlags.Static | BindingFlags.NonPublic);
            Vector2 position = camera.WorldToScreenPoint(target.transform.position);
            select.Invoke(reflector, new object[] { position, position });
            string report = (string)text.GetValue(null);
            if (!(bool)visible.GetValue(null) || !report.Contains("AuditSprite") || !report.Contains("MeshRenderer"))
                throw new Exception("Clicked renderer did not appear in the on-screen report");
            if (!writer.Builder.ToString().Contains("AuditSprite"))
                throw new Exception("Report did not reach the BPLE command console");
            typeof(DevelopmentReflector).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reflector, null);
            if (!(bool)visible.GetValue(null) || (string)text.GetValue(null) != report)
                throw new Exception("Report disappears after releasing F6");
            Vector2 outside = new Vector2(-10000f, -10000f);
            select.Invoke(reflector, new object[] { outside, outside });
            if (!((string)text.GetValue(null)).Contains("No visible element"))
                throw new Exception("Empty selection has no feedback");
            visible.SetValue(null, false);
            Debug.Log("REFLECTOR_AUDIT_PASS: click selection, visible persistent output, command console and empty-selection feedback");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
