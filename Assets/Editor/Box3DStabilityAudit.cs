using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Box3D;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class Box3DStabilityAudit
{
    private static readonly List<string> results = new List<string>();
    private static bool box;
    private static int failures;
    private static float referenceMotorError, referenceMotorLate, referenceMotorAngle;
    private static Rigidbody Make(string name, Vector3 position, bool sphere = false)
    {
        var go = new GameObject(name); go.transform.position = position;
        if (sphere) go.AddComponent<SphereCollider>().radius = 0.5f;
        else go.AddComponent<BoxCollider>().size = new Vector3(1, 0.4f, 0.3f);
        var rb = go.AddComponent<Rigidbody>(); rb.maxAngularVelocity = 100;
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        return rb;
    }
    private static void Begin()
    {
        Physics.SyncTransforms();
        if (box && !GamePhysics.SelectBox3D(true)) throw new Exception(GamePhysics.LastError);
    }
    private static void Step()
    {
        GamePhysics.Simulate(0.02f);
        if (box && !GamePhysics.IsBox3D) throw new Exception(GamePhysics.LastError);
    }
    private static void Check(bool pass, string detail)
    {
        results.Add((pass ? "PASS " : "FAIL ") + (box ? "Box3D " : "PhysX ") + detail);
        if (!pass) failures++;
    }
    private static void Clear()
    {
        GamePhysics.Shutdown();
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(go);
    }
    private static object Backend => typeof(GamePhysics).GetField("backend", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    private static Dictionary<int, ShapeId> ShapeIds()
    {
        var ids = new Dictionary<int, ShapeId>();
        if (!box) return ids;
        var shapes = (IDictionary)typeof(Box3DBackend).GetField("shapes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Backend);
        foreach (DictionaryEntry p in shapes) ids.Add((int)p.Key, ((Shape)p.Value.GetType().GetField("native").GetValue(p.Value)).Id);
        return ids;
    }
    private static void Spin(bool tilted)
    {
        var a = Make("pivot", new Vector3(500, 30, 0)); a.isKinematic = true;
        var b = Make("rotating beam", new Vector3(500, 29, 0)); b.useGravity = false;
        if (tilted) { b.transform.rotation = Quaternion.Euler(0, 90, 27); Physics.SyncTransforms(); }
        var hinge = b.gameObject.AddComponent<HingeJoint>(); hinge.connectedBody = a;
        hinge.axis = b.transform.InverseTransformDirection(Vector3.forward);
        hinge.anchor = b.transform.InverseTransformPoint(a.position);
        hinge.useMotor = true; hinge.motor = new JointMotor { force = 100, targetVelocity = 180 };
        Begin(); var ids = ShapeIds(); int rebuilds = 0;
        float error = 0, speed = 0, lateError = 0;
        for (int i = 0; i < 1500; i++)
        {
            Step();
            float e = Vector3.Distance(b.transform.TransformPoint(hinge.anchor), a.transform.TransformPoint(hinge.connectedAnchor));
            error = Mathf.Max(error, e); if (i > 1000) lateError = Mathf.Max(lateError, e);
            speed = Mathf.Max(speed, b.velocity.magnitude);
            var next = ShapeIds(); foreach (var p in next) if (ids.TryGetValue(p.Key, out var id) && !id.Equals(p.Value)) rebuilds++;
            ids = next;
        }
        Check(error < 0.1f && lateError < 0.05f && speed < 6, "spin tilted=" + tilted + " error=" + error + " late=" + lateError + " speed=" + speed + " rebuilds=" + rebuilds);
        if (box) Check(rebuilds == 0, "rigid rotation preserves shape IDs, tilted=" + tilted + " rebuilds=" + rebuilds);
    }
    private static void DrivenCart(bool animatedCollider)
    {
        var ground = new GameObject("ground"); ground.transform.position = new Vector3(0,-0.5f,0);
        ground.AddComponent<BoxCollider>().size = new Vector3(2000,1,10);
        var chassis = Make("chassis", new Vector3(300,2,0)); chassis.mass = 5;
        var wheels = new Rigidbody[2]; var joints = new FixedJoint[2];
        Transform animated = null;
        for(int i=0;i<2;i++)
        {
            wheels[i] = Make("driven wheel", new Vector3(299+i*2,1,0),true);
            joints[i] = wheels[i].gameObject.AddComponent<FixedJoint>(); joints[i].connectedBody = chassis;
            if (animatedCollider && i == 0)
            {
                UnityEngine.Object.DestroyImmediate(wheels[i].GetComponent<Collider>());
                animated = new GameObject("WheelPivot").transform; animated.SetParent(wheels[i].transform,false);
                animated.gameObject.AddComponent<SphereCollider>().radius = 0.5f;
            }
        }
        Begin(); float error=0, lateError=0; var ids=ShapeIds(); int rebuilds=0;
        for(int i=0;i<1500;i++)
        {
            if(animated) animated.localRotation = Quaternion.Euler(0,0,i*7);
            if(i>100 && i<700) foreach(var wheel in wheels) if (wheel.velocity.x < 10) wheel.EngineAddForce(Vector3.right*50);
            Step();
            foreach(var j in joints) { float e=Vector3.Distance(j.transform.TransformPoint(j.anchor),chassis.transform.TransformPoint(j.connectedAnchor)); error=Mathf.Max(error,e); if(i>1000) lateError=Mathf.Max(lateError,e); }
            var next=ShapeIds(); foreach(var p in next) if(ids.TryGetValue(p.Key,out var id) && !id.Equals(p.Value)) rebuilds++; ids=next;
        }
        Check(error<0.2f && lateError<0.05f && chassis.position.x>305, "cart animated="+animatedCollider+" error="+error+" late="+lateError+" x="+chassis.position.x+" rebuilds="+rebuilds);
        if(box) Check(rebuilds==0,"moving compound and animated sphere preserve shape IDs");
    }
    private static void MotorCart()
    {
        var ground = new GameObject("ground"); ground.transform.position = new Vector3(0,-0.5f,0);
        ground.AddComponent<BoxCollider>().size = new Vector3(2000,1,10);
        var chassis = Make("chassis",new Vector3(300,2,0)); chassis.mass=10; chassis.drag=0.2f;
        var wheels=new Rigidbody[2]; var probes=new Box3DWheelAuditProbe[2]; var joints=new HingeJoint[2];
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/Part_MotorWheel_01_SET.prefab");
        for(int i=0;i<2;i++)
        {
            var go=new GameObject("ordinary motor wheel"); go.transform.position=new Vector3(298+4*i,1,0);
            foreach(var c in prefab.GetComponentsInChildren<Collider>())
            {
                var child=new GameObject(c.name); child.transform.SetParent(go.transform,false);
                child.transform.localPosition=prefab.transform.InverseTransformPoint(c.transform.position);
                child.transform.localRotation=Quaternion.Inverse(prefab.transform.rotation)*c.transform.rotation;
                Collider clone;
                if(c is SphereCollider s) { var sphere=child.AddComponent<SphereCollider>(); sphere.radius=s.radius; sphere.center=s.center; clone=sphere; }
                else if(c is BoxCollider b) { var cube=child.AddComponent<BoxCollider>(); cube.size=b.size; cube.center=b.center; clone=cube; }
                else throw new Exception("Unexpected motor wheel collider");
                clone.sharedMaterial=c.sharedMaterial;
            }
            var rb=go.AddComponent<Rigidbody>(); wheels[i]=rb; rb.mass=1; rb.constraints=(RigidbodyConstraints)56; rb.drag=0.2f;
            var j=go.AddComponent<HingeJoint>(); joints[i]=j; j.autoConfigureConnectedAnchor=false;
            j.anchor=go.transform.InverseTransformPoint(chassis.position)*0.5f;
            j.connectedAnchor=chassis.transform.InverseTransformPoint(go.transform.position)*0.5f;
            j.axis=Vector3.forward; j.useLimits=true; j.limits=new JointLimits{min=-0.1f,max=0.1f}; j.connectedBody=chassis;
            j.enablePreprocessing=false;
            probes[i]=go.AddComponent<Box3DWheelAuditProbe>();
        }
        Begin(); var ids=ShapeIds(); int rebuilds=0; float error=0,late=0,angle=0; int driveTicks=0;
        for(int i=0;i<1500;i++)
        {
            for(int k=0;k<2;k++)
            {
                Vector3 pivot=wheels[k].transform.TransformPoint(new Vector3(0,-0.075f,0));
                if(i<750 && wheels[k].velocity.x<15 && Physics.Raycast(pivot,probes[k].direction,out var hit,0.55f))
                {
                    wheels[k].EngineAddForceAtPosition(50*Mathf.Sqrt(Mathf.Clamp01(1-Mathf.Abs(wheels[k].velocity.x)/15))*Vector3.Cross(hit.normal,Vector3.forward),hit.point);
                    driveTicks++;
                }
            }
            Step();
            foreach(var j in joints) { float e=Vector3.Distance(j.transform.TransformPoint(j.anchor),chassis.transform.TransformPoint(j.connectedAnchor)); error=Mathf.Max(error,e); if(i>1000) late=Mathf.Max(late,e); angle=Mathf.Max(angle,Mathf.Abs(Mathf.DeltaAngle(chassis.rotation.eulerAngles.z,j.transform.eulerAngles.z))); }
            var next=ShapeIds(); foreach(var p in next) if(ids.TryGetValue(p.Key,out var id) && !id.Equals(p.Value)) rebuilds++; ids=next;
        }
        if(!box) { referenceMotorError=error; referenceMotorLate=late; referenceMotorAngle=angle; }
        bool stable=error<0.3f && late<0.05f && angle<25;
        if(box) stable &= error<=referenceMotorError+0.005f && late<=referenceMotorLate+0.005f && angle<=referenceMotorAngle+0.1f;
        Check(stable && chassis.position.x>400 && driveTicks>1000,"ordinary motor wheel error="+error+" late="+late+" angle="+angle+" x="+chassis.position.x+" driveTicks="+driveTicks+" rebuilds="+rebuilds);
        if(box) Check(rebuilds==0,"ordinary motor wheel preserves contact geometry while moving");
    }
    private static void NarrowChain()
    {
        var ground = new GameObject("ground"); ground.transform.position = new Vector3(300,-0.5f,0);
        ground.AddComponent<BoxCollider>().size = new Vector3(1000,1,10);
        var bodies=new List<Rigidbody>(); var joints=new List<HingeJoint>();
        for(int i=0;i<12;i++)
        {
            var rb=Make("frame "+i,new Vector3(300+i,4,0)); rb.drag=0.2f; bodies.Add(rb);
            if(i==6) rb.mass=20;
            if(i==0) continue;
            var j=rb.gameObject.AddComponent<HingeJoint>(); j.autoConfigureConnectedAnchor=false;
            j.connectedBody=bodies[i-1]; j.anchor=Vector3.left*0.5f; j.connectedAnchor=Vector3.right*0.5f;
            j.axis=Vector3.forward; j.useLimits=true; j.limits=new JointLimits{min=-0.1f,max=0.1f}; j.enablePreprocessing=false;
            joints.Add(j);
        }
        Begin(); float error=0,late=0,speed=0;
        for(int i=0;i<3000;i++)
        {
            if(i==300) bodies[6].EngineAddForce(new Vector3(50,50,0),ForceMode.Impulse);
            Step();
            foreach(var j in joints)
            {
                float e=Vector3.Distance(j.transform.TransformPoint(j.anchor),j.connectedBody.transform.TransformPoint(j.connectedAnchor));
                error=Mathf.Max(error,e); if(i>2000) late=Mathf.Max(late,e);
            }
            if(i>2000) foreach(var rb in bodies) speed=Mathf.Max(speed,rb.velocity.magnitude);
        }
        Check(error<0.25f && late<0.02f && speed<0.1f,"60-second limited-hinge chain error="+error+" late="+late+" final-speed="+speed);
    }
    private static void Breaking()
    {
        var pivot=Make("pivot",new Vector3(0,3,0)); pivot.isKinematic=true;
        var rb=Make("breakable frame",new Vector3(0,1,0));
        var j=rb.gameObject.AddComponent<FixedJoint>(); j.connectedBody=pivot; j.breakForce=1;
        var probe=rb.gameObject.AddComponent<Box3DWheelAuditProbe>();
        Begin(); for(int i=0;i<10;i++) Step();
        Check(probe.breaks==1,"broken connection is not recreated before deferred Destroy, breaks="+probe.breaks);
    }
    private static void Suspension()
    {
        var pivot=Make("suspension pivot",new Vector3(0,3,0)); pivot.isKinematic=true;
        var rb=Make("off-road suspension",new Vector3(0,1,0));
        var j=rb.gameObject.AddComponent<ConfigurableJoint>(); j.connectedBody=pivot;
        j.xMotion=j.zMotion=ConfigurableJointMotion.Locked; j.yMotion=ConfigurableJointMotion.Limited;
        j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Locked;
        j.linearLimit=new SoftJointLimit{limit=0}; j.linearLimitSpring=new SoftJointLimitSpring{spring=150,damper=5};
        Begin(); for(int i=0;i<500;i++) Step();
        float extension=1-rb.position.y;
        // Unity treats a zero linear limit as locked, even with linearLimitSpring set.
        Check(Mathf.Abs(extension)<0.001f,"off-road zero-limit constraint matches PhysX under load, extension="+extension);
    }
    private static void Catalog()
    {
        if(!box) return;
        int count=0;
        foreach(var path in Directory.GetFiles("Assets/GameObject","Part_*.prefab"))
        {
            string name=Path.GetFileNameWithoutExtension(path);
            if(!name.Contains("Wheel") && !name.Contains("Frame")) continue;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));
            var go=new GameObject(name); go.transform.position=new Vector3(500,30,0);
            foreach(var c in prefab.GetComponentsInChildren<Collider>())
            {
                var child=new GameObject(c.name); child.transform.SetParent(go.transform,false);
                child.transform.localPosition=prefab.transform.InverseTransformPoint(c.transform.position);
                child.transform.localRotation=Quaternion.Inverse(prefab.transform.rotation)*c.transform.rotation;
                child.transform.localScale=c.transform.lossyScale;
                if(c is BoxCollider b) { var clone=child.AddComponent<BoxCollider>(); clone.center=b.center; clone.size=b.size; }
                else if(c is SphereCollider s) { var clone=child.AddComponent<SphereCollider>(); clone.center=s.center; clone.radius=s.radius; }
                else throw new Exception("Unhandled audit collider in "+name+": "+c.GetType());
            }
            var rb=go.AddComponent<Rigidbody>(); rb.constraints=(RigidbodyConstraints)56; rb.useGravity=false; rb.angularDrag=0;
            rb.velocity=Vector3.right*10; rb.angularVelocity=Vector3.forward*2;
            Begin(); var ids=ShapeIds(); int rebuilds=0;
            for(int i=0;i<300;i++)
            {
                Step(); var next=ShapeIds(); foreach(var p in next) if(ids.TryGetValue(p.Key,out var id) && !id.Equals(p.Value)) rebuilds++; ids=next;
            }
            Check(rebuilds==0 && rb.velocity.magnitude<20 && rb.position.x>550,"moving prefab "+name+" rebuilds="+rebuilds);
            count++; Clear();
        }
        Check(count>=8,"wheel/frame geometry catalog covered "+count+" prefabs");
    }
    private static void SquareFrames(bool metal)
    {
        string name=metal?"Part_MetalFrame_01_SET":"Part_WoodenFrame_01_SET";
        var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/"+name+".prefab").GetComponent<BoxCollider>();
        var ground=new GameObject("ground"); ground.transform.position=new Vector3(300,-0.5f,0);
        ground.AddComponent<BoxCollider>().size=new Vector3(1000,1,10);
        var frames=new Rigidbody[3,4]; var joints=new List<FixedJoint>(); var probes=new List<Box3DWheelAuditProbe>();
        for(int y=0;y<3;y++) for(int x=0;x<4;x++)
        {
            var rb=Make(name,new Vector3(300+x,0.505f+y,0)); frames[y,x]=rb;
            rb.mass=metal?1:0.5f; rb.drag=0.2f;
            var shape=rb.GetComponent<BoxCollider>(); shape.size=template.size; shape.center=template.center; shape.sharedMaterial=template.sharedMaterial;
            probes.Add(rb.gameObject.AddComponent<Box3DWheelAuditProbe>());
            foreach(var other in new[]{x>0?frames[y,x-1]:null,y>0?frames[y-1,x]:null})
            {
                if(!other) continue;
                var joint=rb.gameObject.AddComponent<FixedJoint>(); joint.connectedBody=other;
                joint.enablePreprocessing=false; joint.breakForce=metal?1200:250; joints.Add(joint);
            }
        }
        Begin(); float maxError=0,lateError=0,lateSpeed=0;
        for(int i=0;i<3000;i++)
        {
            if(i>=100 && i<500) frames[2,3].EngineAddForce(Vector3.right*30);
            Step();
            foreach(var j in joints) if(j)
            {
                float error=Vector3.Distance(j.transform.TransformPoint(j.anchor),j.connectedBody.transform.TransformPoint(j.connectedAnchor));
                maxError=Mathf.Max(maxError,error); if(i>2000) lateError=Mathf.Max(lateError,error);
            }
            if(i>2000) foreach(var rb in frames) lateSpeed=Mathf.Max(lateSpeed,rb.velocity.magnitude);
        }
        int breaks=0; foreach(var p in probes) breaks+=p.breaks;
        Check(breaks==0 && maxError<0.05f && lateError<0.01f && lateSpeed<0.1f,"60-second square-frame grid metal="+metal+" error="+maxError+" late="+lateError+" speed="+lateSpeed+" breaks="+breaks);
    }
    public static void Run()
    {
        int exit=0;
        try
        {
            PhysicsSimulation.Auto=false; Time.fixedDeltaTime=0.02f; Clear();
            results.Add("Execution: "+(EditorApplication.isPlaying?"Play Mode":"Edit Mode"));
            foreach (bool mode in new[] { false, true })
            {
                box = mode;
                Spin(false); Clear();
                Spin(true); Clear();
                DrivenCart(false); Clear();
                DrivenCart(true); Clear();
                MotorCart(); Clear();
                NarrowChain(); Clear();
                Breaking(); Clear();
                Suspension(); Clear();
                SquareFrames(false); Clear();
                SquareFrames(true); Clear();
                Catalog();
            }
        }
        catch(Exception e) { results.Add(e.ToString()); exit=1; }
        finally { GamePhysics.Shutdown(); Directory.CreateDirectory("Builds/QA"); File.WriteAllLines("Builds/QA/box3d-stability.txt",results); }
        EditorApplication.Exit(exit!=0 || failures>0 ? 1:0);
    }
    public static void RunInPlayMode()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("Box3DStabilityAudit.Play",true); EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Box3DStabilityAudit.Play",false)) return;
            SessionState.SetBool("Box3DStabilityAudit.Play",false); EditorApplication.delayCall+=Run;
        };
    }
}
