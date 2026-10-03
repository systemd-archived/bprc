using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using RCR2Port;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PortRepairAudit
{
 static List<string> rows = new List<string>();
 static void Check(bool value, string message) { if(!value) throw new Exception(message); rows.Add("PASS " + message); }
 public static void Run()
 {
  rows.Clear(); Directory.CreateDirectory("Builds/QA/PortRepair");
  try
  {
   RoverPortExtraCatalog.Load();
   RC3Port.RC3PortCatalog.Load();
   foreach(var prototype in RoverPortCatalog.Instance.Parts)
   {
    var desc=new ConstructionUI.PartDesc { part=prototype.GetComponent<BasePart>(), maxCount=10, useCount=10000 };
    Check(desc.HasAvailableParts && desc.CurrentCount==10000,"unlimited inventory " + prototype.name);
   }
   var regular=new GameObject("regular");var native=regular.AddComponent<BasePart>();
   Check(!new ConstructionUI.PartDesc{part=native,maxCount=10,useCount=10}.HasAvailableParts,"native inventory unchanged");
   UnityEngine.Object.DestroyImmediate(regular);
   AuditWheel(); AuditMissile(); AuditUI();
   rows.Add("PASS all repair checks");Debug.Log("PORT_REPAIR_AUDIT_PASS");
  }
  catch(Exception e) { rows.Add("FAIL " + e); throw; }
  finally { File.WriteAllLines("Builds/QA/PortRepair/audit.txt",rows); }
 }
 static RoverPortPartData Data(RoverPortPart p) => (RoverPortPartData)typeof(RoverPortPart).GetField("m_portData",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p);
 static Transform Node(RoverPortPart p,string id) => (Transform)typeof(RoverPortPart).GetMethod("FindNode",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{id});
 static void AuditWheel()
 {
  var go=UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_mecanum_wheel"));
  bool auto=PhysicsSimulation.Auto;PhysicsSimulation.Auto=false;
  try
  {
   go.SetActive(true);var part=go.GetComponent<RoverPortPoweredPart>();part.Awake();part.EnsureRigidbody();
   var data=Data(part);var root=go.GetComponent<Rigidbody>();root.isKinematic=true;
   foreach(var b in go.GetComponentsInChildren<Rigidbody>()) { b.useGravity=false;b.solverIterations=20;b.solverVelocityIterations=12; }
   var hub=Node(part,data.motion.wheel_bodies[0]).GetComponent<Rigidbody>();
   var rollers=data.motion.contact_bodies.Select(id=>Node(part,id).GetComponent<Rigidbody>()).ToArray();
   var rest=rollers.Select(b=>Vector3.Distance(b.position,hub.position)).ToArray();
   foreach(var roller in rollers)
   {
    var spring=roller.GetComponents<SpringJoint>().Single(j=>j.connectedBody==hub);
    Check(Mathf.Abs(spring.minDistance-Vector3.Distance(roller.transform.TransformPoint(spring.anchor),hub.transform.TransformPoint(spring.connectedAnchor)))<0.002f,"automatic roller spring distance " + roller.name);
    Check(Mathf.Abs(roller.GetComponent<SphereCollider>().radius-0.205f)<0.001f,"roller collision radius " + roller.name);
   }
   for(int i=0;i<240;i++)Physics.Simulate(1f/120f);
   float drift=rollers.Select((b,i)=>Mathf.Abs(Vector3.Distance(b.position,hub.position)-rest[i])).Max();
   rows.Add("wheel rest maximum radial drift="+drift);
   Check(drift<0.15f,"wheel ring remains stable at rest");
   var drive=typeof(RoverPortPoweredPart).GetMethod("DriveWheels",BindingFlags.Instance|BindingFlags.NonPublic);
   drive.Invoke(part,new object[]{0.1f,1f});
   var hinge=hub.GetComponent<HingeJoint>();
   Check(hinge.useMotor && Mathf.Abs(hinge.motor.targetVelocity+540f)<0.01f && Mathf.Abs(hinge.motor.force-1000f)<0.01f,"hub receives original motor torque and RPM");
   for(int i=0;i<120;i++)Physics.Simulate(1f/120f);
   drift=rollers.Select((b,i)=>Mathf.Abs(Vector3.Distance(b.position,hub.position)-rest[i])).Max();
   rows.Add("wheel driven maximum radial drift="+drift);
   Check(drift<0.3f,"wheel ring remains connected under motor load");
   drive.Invoke(part,new object[]{0f,0f});Check(!hinge.useMotor,"hub motor releases with no power");
   Render(go,"mecanum-runtime.png");
   var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
   try
   {
    float bottom=go.GetComponentsInChildren<Collider>().Min(c=>c.bounds.min.y);
    ground.transform.position=new Vector3(0,bottom-0.55f,0);
    ground.transform.localScale=new Vector3(100,1,10);
    foreach(var b in go.GetComponentsInChildren<Rigidbody>())b.useGravity=true;
    root.isKinematic=false;root.mass=10;
    Physics.SyncTransforms();
    for(int i=0;i<120;i++)Physics.Simulate(1f/120f);
    float startX=root.position.x;
    drive.Invoke(part,new object[]{0.1f,1f});
    for(int i=0;i<240;i++)Physics.Simulate(1f/120f);
    float travel=Mathf.Abs(root.position.x-startX);
    rows.Add("wheel ground travel="+travel);
    Check(travel>0.05f,"wheel motor propels free assembly through ground contact");
    Check(rollers.All(b=>Vector3.Distance(b.position,hub.position)<1.5f),"ground load preserves roller ring");
   }
   finally {UnityEngine.Object.DestroyImmediate(ground);}
  }
  finally { UnityEngine.Object.DestroyImmediate(go);PhysicsSimulation.Auto=auto; }
 }
 static void AuditMissile()
 {
  var go=UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_rc3_missile"));
  try
  {
   go.SetActive(true);var part=go.GetComponent<RoverPortMissilePart>();part.Awake();
   var data=Data(part);var sprite=data.construction_renderers.Single(r=>r.node=="ME:4").sprite;
   Check(sprite.width==349 && sprite.height==105,"missile uses full unrotated source sprite");
   Check(Vector3.Dot(Node(part,"ME:4").right,go.transform.up)>0.999f,"missile preserves source mount rotation");
   Check(Mathf.Abs(Node(part,"ME:4").localPosition.x-24.046f/128f)<0.001f,"missile preserves source node offset");
   Render(go,"missile-build.png");
   part.EnsureRigidbody();Render(go,"missile-runtime.png");
   Check(part.TryFire(),"missile fires");
   var projectile=UnityEngine.Object.FindObjectOfType<RoverPortRC3MissileProjectile>();
   Check(projectile!=null && Vector3.Dot(projectile.GetComponent<Rigidbody>().velocity.normalized,go.transform.up)>0.999f,"missile flies in its visible nose direction");
   if(projectile!=null) { Render(projectile.gameObject,"missile-projectile.png");UnityEngine.Object.DestroyImmediate(projectile.gameObject); }
   foreach(var effect in UnityEngine.Object.FindObjectsOfType<RoverPortTimedHide>())UnityEngine.Object.DestroyImmediate(effect.gameObject);
  }
  finally { UnityEngine.Object.DestroyImmediate(go); }
 }
 static void AuditUI()
 {
  foreach(string name in new[]{"UI_EnterButton","UI_BackButton","UI_MenuButton","UI_CopyButton","UI_Button_Background"})
  {
   var sprite=AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Sprite/"+name+".asset");
   Check(sprite!=null && sprite.rect.size==new Vector2(200,200),"UI original canvas " + name);
   var go=new GameObject(name,typeof(RectTransform),typeof(Canvas));
   go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
   go.transform.localScale=Vector3.one*0.02f;
   var icon=new GameObject("Image",typeof(RectTransform),typeof(Image));
   icon.transform.SetParent(go.transform,false);
   var graphic=icon.GetComponent<Image>();graphic.sprite=sprite;
   graphic.rectTransform.sizeDelta=new Vector2(100,100);
   Canvas.ForceUpdateCanvases();
   try {Render(go,name+".png",true);} finally {UnityEngine.Object.DestroyImmediate(go);}
  }
 }
 static void Render(GameObject go,string filename,bool ui=false)
 {
  var cameraObject=new GameObject("audit camera");var camera=cameraObject.AddComponent<Camera>();
  var target=new RenderTexture(512,512,24);camera.targetTexture=target;camera.orthographic=true;
  camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.12f,0.14f,0.17f,1);camera.cullingMask=1<<30;
  var active=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
  var layers=go.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.layer);
  foreach(var t in layers.Keys)t.gameObject.layer=30;
  Bounds bounds=new Bounds(go.transform.position,Vector3.zero);bool first=true;
  foreach(var r in active){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
  camera.orthographicSize=ui?1.2f:Mathf.Max(bounds.extents.x,bounds.extents.y)*1.2f;
  camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,bounds.center.z-30);
  var previous=RenderTexture.active;var image=new Texture2D(512,512,TextureFormat.RGBA32,false);
  try {camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();File.WriteAllBytes("Builds/QA/PortRepair/"+filename,image.EncodeToPNG());}
  finally {RenderTexture.active=previous;foreach(var pair in layers)pair.Key.gameObject.layer=pair.Value;UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);}
 }
}
