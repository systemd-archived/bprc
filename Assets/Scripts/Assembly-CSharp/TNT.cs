using System;
using System.Collections;
using System.Collections.Generic;
using Innovation;
using UnityEngine;

public class TNT : BasePart
{
    public float m_explosionImpulse;

    public float m_explosionRadius;

    public float m_triggerSpeed;

    [SerializeField]
    protected GameObject extraEffect;

    protected bool m_triggered;

    private GameObject m_leftAttachment;

    private GameObject m_rightAttachment;

    private GameObject m_topAttachment;

    private GameObject m_bottomAttachment;

    public GameObject smokeCloud;

    protected int no90 => (m_gridRotation >= GridRotation.Deg_45) ? 1 : 0;

    private Vector3 m_lastVelocity;

    private Transform m_visualization;

    // ===== 灼烧模式（no90 == 1 的普通 TNT）：360° 范围灼烧（炸弹火球，距离越远越弱） =====
    private const float BurnImpulseScale = 0.05f;     // 灼烧模式物理冲击倍率（大幅削减原爆炸伤害）
    private const float BurnMaxRange = 12f;           // 灼烧范围半径
    private const float BurnProbabilityScale = 0.16f; // 灼烧概率基准（贴脸时最高，随距离衰减）
    private const int BurnOcclusionBuckets = 16;     // 360° 方位角遮挡细分数量
    private const float ShakeForceScale = 0f;      // 震动破坏基准力度（方向真随机、力度随机，随距离衰减）
    private const float ShakeRandomMin = 0.5f;       // 随机力度下限倍率
    private const float ShakeRandomMax = 1.5f;       // 随机力度上限倍率

    private static readonly System.Random s_random = new System.Random();

    public override bool CanBeEnclosed()
    {
        return true;
    }

    public override void Awake()
    {
        base.Awake();
        Transform transform = base.transform.Find("LeftAttachment");
        Transform transform2 = base.transform.Find("RightAttachment");
        Transform transform3 = base.transform.Find("TopAttachment");
        Transform transform4 = base.transform.Find("BottomAttachment");
        m_visualization = base.transform.Find("Visualization");
        if ((bool)transform)
        {
            m_leftAttachment = transform.gameObject;
            m_leftAttachment.SetActive(value: false);
        }
        if ((bool)transform2)
        {
            m_rightAttachment = transform2.gameObject;
            m_rightAttachment.SetActive(value: false);
        }
        if ((bool)transform3)
        {
            m_topAttachment = transform3.gameObject;
            m_topAttachment.SetActive(value: false);
        }
        if ((bool)transform4)
        {
            m_bottomAttachment = transform4.gameObject;
            m_bottomAttachment.SetActive(value: false);
        }
    }

    public override Direction EffectDirection()
    {
        if (INSettings.GetBool(INFeature.RotatableTNT))
        {
            return BasePart.Rotate(Direction.Right, m_gridRotation);
        }
        return Direction.Right;
    }

    public override void SetRotation(GridRotation rotation)
    {
        base.SetRotation(rotation);
        ApplyOrientation(rotation);
    }

    private Transform GetVisualization()
    {
        if (m_visualization == null)
        {
            m_visualization = base.transform.Find("Visualization");
        }
        return m_visualization;
    }

    private void ApplyOrientation(GridRotation rotation)
    {
        Transform visualization = GetVisualization();
        if (m_gridRotation >= GridRotation.Deg_45)
        {
            GridRotation ortho = (GridRotation)(((int)m_gridRotation - (int)GridRotation.Deg_45) % 4);
            ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(ortho), Vector3.forward), false);
            if ((bool)visualization)
            {
                visualization.localRotation = Quaternion.AngleAxis(45f, Vector3.forward);
            }
        }
        else
        {
            ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(rotation), Vector3.forward), false);
            if ((bool)visualization)
            {
                visualization.localRotation = Quaternion.identity;
            }
        }
    }

    private Vector3 VisualRightDir()
    {
        return Quaternion.AngleAxis(GetRotationAngle(m_gridRotation), Vector3.forward) * Vector3.right;
    }

    public override void Initialize()
    {
        if (m_partTier != PartTier.Epic && m_partTier != PartTier.Common)
        {
            base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), 1);
        }
    }

    public override void OnPhysicsCollisionEnter(PhysicsCollision c)
    {
        base.OnPhysicsCollisionEnter(c);
        if (m_partTier == PartTier.Regular && c.relativeVelocity.magnitude > ((no90 == 1) ? 96f : m_triggerSpeed) && !base.HasGeneratorRef)
        {
            Explode();
        }
        if (m_partTier == PartTier.Rare && c.relativeVelocity.magnitude > 96f && !base.HasGeneratorRef)
        {
            Explode();
        }
        if (customPartIndex == 3 && c.relativeVelocity.magnitude > 240f && !base.HasGeneratorRef)
        {
            Explode();
        }
        if (customPartIndex == 4 && c.relativeVelocity.magnitude > 96f && !base.HasGeneratorRef)
        {
            Explode();
        }
    }

    public override void ChangeVisualConnections()
    {
        bool active = base.contraption.CanConnectTo(this, BasePart.Rotate(Direction.Up, m_gridRotation));
        bool active2 = base.contraption.CanConnectTo(this, BasePart.Rotate(Direction.Down, m_gridRotation));
        bool active3 = base.contraption.CanConnectTo(this, BasePart.Rotate(Direction.Left, m_gridRotation));
        bool active4 = base.contraption.CanConnectTo(this, BasePart.Rotate(Direction.Right, m_gridRotation));
        if ((bool)m_leftAttachment)
        {
            m_leftAttachment.SetActive(active3);
        }
        if ((bool)m_rightAttachment)
        {
            m_rightAttachment.SetActive(active4);
        }
        if ((bool)m_topAttachment)
        {
            m_topAttachment.SetActive(active);
        }
        if ((bool)m_bottomAttachment)
        {
            m_bottomAttachment.SetActive(active2);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(base.transform.position, m_explosionRadius);
    }

    protected override void OnTouch()
    {
        if (customPartIndex != 3)
        {
            Explode();
        }
    }

    public override void PrePlaced()
    {
        base.PrePlaced();
        if (INSettings.GetBool(INFeature.RotatableTNT))
        {
            m_autoAlign = AutoAlignType.Rotate;
        }
    }

    public virtual void Explode()
    {
        if (IsMultiplayerReplica() || m_triggered)
        {
            return;
        }
        m_triggered = true;
        base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), -1);
        float num = 1f;
        if (m_partTier == PartTier.Regular)
        {
            num = INSettings.GetFloat(INFeature.TNTExplosionRadius);
        }
        else if (m_partTier == PartTier.Common)
        {
            num = 1.13f * INSettings.GetFloat(INFeature.TNTExplosionRadius);
        }
        else if (m_partTier == PartTier.Rare)
        {
            num = 2f * INSettings.GetFloat(INFeature.TNTExplosionRadius);
        }
        else if (customPartIndex == 3)
        {
            num = 0.25f * INSettings.GetFloat(INFeature.TNTExplosionRadius);
        }
        else if (customPartIndex == 4)
        {
            num = 3f * INSettings.GetFloat(INFeature.TNTExplosionRadius);
        }
        else if (m_partTier == PartTier.Legendary)
        {
            num = INSettings.GetFloat(INFeature.TNTExplosionRadius);
        }
        float explosionForce = INSettings.GetFloat(INFeature.TNTExplosionForce);
        Collider[] array = Physics.OverlapSphere(base.transform.position, m_explosionRadius * num);
        foreach (Collider collider in array)
        {
            GameObject gameObject = FindParentWithRigidBody(collider.gameObject);
            if (gameObject != null)
            {
                int num2 = CountChildColliders(gameObject, 0);
                AddExplosionForce(gameObject, explosionForce / (float)num2);
            }
            BasePart component = collider.GetComponent<BasePart>();
            // 军事版功能：核弹不参与 TNT 的连锁引爆（INMilitaryFeatures.Enabled 关闭时不生效）
            if (component is TNT tNT && !(component is AlienTNT) && !component.HasGeneratorRef && (!INMilitaryFeatures.Enabled || !tNT.IsNuclearBomb()))
            {
                bool flag = false;
                if (tNT.m_partTier == PartTier.Regular)
                {
                    flag = true;
                }
                else if (tNT.customPartIndex == 4 && customPartIndex == 4)
                {
                    flag = true;
                }
                if (flag)
                {
                    tNT.Explode();
                }
            }
            if (INSettings.GetBool(INFeature.BlasterTNT) && component is BlasterTNT blasterTNT && !component.HasGeneratorRef && Vector.DistanceSquared2(base.transform.position, component.transform.position) < 4f)
            {
                blasterTNT.ExplodeSpecial();
            }
        }
        // 灼烧模式：普通 TNT 旋转为对角朝向（no90 == 1）时，附加 360° 范围灼烧
        if (m_partTier == PartTier.Regular && no90 == 1)
        {
            ApplyBurnDamage();
        }
        Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.tntExplosion, base.transform.position);
        WPFMonoBehaviour.effectManager.CreateParticles(smokeCloud, base.transform.position - Vector3.forward * 5f, force: true);
        if ((bool)extraEffect)
        {
            WPFMonoBehaviour.effectManager.CreateParticles(extraEffect, base.transform.position - Vector3.forward * 4f, force: true);
        }
        CheckForTNTAchievement();
        base.contraption.RemovePart(this);
        List<Joint> list = base.contraption.FindPartJoints(this);
        if (list.Count > 0)
        {
            foreach (Joint item in list)
            {
                bool flag2 = item.gameObject == this || item.connectedBody == this;
                if (!float.IsInfinity(item.breakForce) || flag2)
                {
                    UnityEngine.Object.Destroy(item);
                }
            }
            HandleJointBreak();
        }
        else
        {
            HandleJointBreak(playEffects: false);
        }
        StartCoroutine(ShineLight());
    }

    protected int CountChildColliders(GameObject obj, int count)
    {
        if ((bool)obj.GetComponent<Collider>())
        {
            count++;
        }
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            count = CountChildColliders(obj.transform.GetChild(i).gameObject, count);
        }
        return count;
    }

    protected GameObject FindParentWithRigidBody(GameObject obj)
    {
        if ((bool)obj.GetComponent<Rigidbody>())
        {
            return obj;
        }
        if ((bool)obj.transform.parent)
        {
            return FindParentWithRigidBody(obj.transform.parent.gameObject);
        }
        return null;
    }

    protected void AddExplosionForce(GameObject target, float forceFactor)
    {
        if ((m_partTier == PartTier.Legendary && no90 == 0) | (m_partTier == PartTier.Regular))
        {
            Vector3 vector = target.transform.position - base.transform.position;
            float num = Mathf.Max(vector.magnitude, 1f);
            float num2 = forceFactor * m_explosionImpulse / Mathf.Pow(num, 1.5f);
            if (m_partTier == PartTier.Regular && no90 == 1)
            {
                // 灼烧模式：原爆炸冲击大幅削减，伤害主要由锥形灼烧承担
                num2 *= BurnImpulseScale;
            }
            Rigidbody component = target.GetComponent<Rigidbody>();
            if (component.mass < 0.1f)
            {
                num2 *= component.mass;
            }
            else if (component.mass < 0.4f)
            {
                num2 *= component.mass / 0.4f;
            }
            Pig component2 = target.GetComponent<Pig>();
            if ((bool)component2)
            {
                component2.PrepareForTNT(base.transform.position, num2);
                num2 *= 1.15f;
            }
            component.EngineAddForce(num2 * vector.normalized, ForceMode.Impulse);
            return;
        }
        if (m_partTier == PartTier.Common)
        {
            Vector3 right = VisualRightDir();
            Vector3 vector2 = Quaternion.Euler(0f, 0f, -90f) * right;
            Vector3 vector3 = target.transform.position - base.transform.position;
            float num3 = Vector3.Dot(vector3, vector2);
            float magnitude = (vector3 - vector2 * num3).magnitude;
            if (num3 < 0.25f || num3 > 9f || magnitude > 0.9f)
            {
                return;
            }
            Rigidbody component3 = target.GetComponent<Rigidbody>();
            if (component3 == null)
            {
                return;
            }
            float num4 = forceFactor * 16f * m_explosionImpulse;
            if (component3.mass < 5.5f)
            {
                num4 *= component3.mass;
            }
            else if (component3.mass < 5.5f)
            {
                num4 *= 5.5f + 0.5f * (component3.mass - 5.5f);
            }
            component3.EngineAddForce(num4 * vector2, ForceMode.Impulse);
        }
        if (m_partTier == PartTier.Rare)
        {
            float num5 = -1f;
            if (no90 == 1 || m_gridRotation == GridRotation.Deg_180)
            {
                num5 = 1f;
            }
            Vector3 vector4 = target.transform.position - base.transform.position;
            float num6 = Mathf.Max(vector4.magnitude, 1f);
            float num7 = forceFactor * 10f * m_explosionImpulse / Mathf.Pow(num6, 1.5f);
            Rigidbody component4 = target.GetComponent<Rigidbody>();
            if (component4.mass < 0.1f)
            {
                num7 *= component4.mass;
            }
            else if (component4.mass < 0.4f)
            {
                num7 *= component4.mass / 0.4f;
            }
            component4.EngineAddForce(num5 * num7 * vector4.normalized, ForceMode.Impulse);
            return;
        }
        if (m_partTier == PartTier.Legendary && no90 == 1)
        {
            Vector3 vector5 = target.transform.position - base.transform.position;
            float num8 = Mathf.Max(vector5.magnitude, 1f);
            float num9 = forceFactor * 10f * m_explosionImpulse / Mathf.Pow(num8, 1.5f);
            Rigidbody component5 = target.GetComponent<Rigidbody>();
            if (component5.mass < 0.1f)
            {
                num9 *= component5.mass;
            }
            else if (component5.mass < 0.4f)
            {
                num9 *= component5.mass / 0.4f;
            }
            component5.EngineAddForce(-1f * num9 * vector5.normalized, ForceMode.Impulse);
            return;
        }
        if (customPartIndex == 3)
        {
            Vector3 right2 = VisualRightDir();
            Vector3 vector6 = Quaternion.Euler(0f, 0f, 90f) * right2;
            Vector3 vector7 = target.transform.position - base.transform.position;
            Vector3.Dot(vector7, VisualRightDir());
            Vector3.Dot(vector7, base.transform.forward);
            if (Vector3.Dot(vector7.normalized, vector6) < 0.2f)
            {
                return;
            }
            Rigidbody component6 = target.GetComponent<Rigidbody>();
            if (component6 == null)
            {
                return;
            }
            float num10 = forceFactor * 200f * m_explosionImpulse;
            if (component6.mass < 0.1f)
            {
                num10 *= component6.mass;
            }
            else if (component6.mass < 0.4f)
            {
                num10 *= component6.mass / 0.4f;
            }
            component6.EngineAddForce(num10 * vector6, ForceMode.Impulse);
        }
        if (customPartIndex == 4)
        {
            float num11 = 1f;
            if (no90 == 1 || m_gridRotation == GridRotation.Deg_180)
            {
                num11 = -1f;
            }
            Vector3 vector8 = target.transform.position - base.transform.position;
            float num12 = Mathf.Max(vector8.magnitude, 1f);
            float num13 = forceFactor * 8f * m_explosionImpulse;
            float num14 = m_explosionRadius * 3f * INSettings.GetFloat(INFeature.TNTExplosionRadius);
            float num15 = Mathf.Clamp01(1f - num12 / num14);
            num13 *= num15;
            Rigidbody component7 = target.GetComponent<Rigidbody>();
            if (component7.mass < 0.1f)
            {
                num13 *= component7.mass;
            }
            else if (component7.mass < 0.4f)
            {
                num13 *= component7.mass / 0.4f;
            }
            component7.EngineAddForce(num11 * num13 * vector8.normalized, ForceMode.Impulse);
            return;
        }
    }

    // ===== 灼烧模式实现（JetEngine 灼烧逻辑的 360° 全向变体） =====

    private struct BurnTargetData : IComparable<BurnTargetData>
    {
        public readonly BasePart Part;

        public readonly float Dist;

        public readonly int Bucket;

        public BurnTargetData(BasePart part, float dist, int bucket)
        {
            Part = part;
            Dist = dist;
            Bucket = bucket;
        }

        public int CompareTo(BurnTargetData other)
        {
            if (Dist < other.Dist)
            {
                return -1;
            }
            if (Dist > other.Dist)
            {
                return 1;
            }
            return 0;
        }
    }

    /// <summary>
    /// 360° 火球灼烧：
    /// 以爆炸点为圆心，所有方向一视同仁；距离越远 falloff 越低，灼烧概率越低。
    /// 零件按距离近→远排序，近处零件会按方位角遮挡后方零件的灼烧概率。
    /// </summary>
    private void ApplyBurnDamage()
    {
        if (m_partTier != PartTier.Regular || no90 != 1)
        {
            return;
        }
        float maxDist = BurnMaxRange;
        Vector3 position = base.transform.position;
        List<BurnTargetData> list = new List<BurnTargetData>();
        foreach (BasePart part in base.contraption.Parts)
        {
            if (part == this)
            {
                continue;
            }
            Vector3 vector = part.transform.position - position;
            float dist = vector.magnitude;
            if (dist < maxDist)
            {
                // 震动破坏：爆炸力基础上额外叠加震动力（方向真随机、力度随机、随距离衰减）
                ApplyShakeForce(part, dist, maxDist);
                float num = Mathf.Atan2(vector.y, vector.x);
                int bucket = (int)((num + Mathf.PI) / (2f * Mathf.PI) * (float)BurnOcclusionBuckets) % BurnOcclusionBuckets;
                list.Add(new BurnTargetData(part, dist, bucket));
            }
        }
        if (list.Count == 0)
        {
            return;
        }
        list.Sort();
        float[] occlusion = new float[BurnOcclusionBuckets];
        for (int i = 0; i < occlusion.Length; i++)
        {
            occlusion[i] = 1f;
        }
        bool flag = false;
        foreach (BurnTargetData data in list)
        {
            // 360° 距离衰减：1 - (dist/maxDist)^3，衰减更缓，中远距离伤害占比更高
            float falloff =0f+ 1f - (data.Dist / maxDist) * (data.Dist / maxDist) * (data.Dist / maxDist);
            float probability = BurnProbabilityScale * falloff * occlusion[data.Bucket];
            Vector3 dir = (data.Part.transform.position - position).normalized;
            if (dir == Vector3.zero)
            {
                dir = Vector3.up;
            }
            flag |= BurnPart(data.Part, probability, dir, data.Dist, occlusion, data.Bucket);
        }
        if (flag)
        {
            base.contraption.UpdateConnectedComponents();
        }
    }

    /// <summary>
    /// 震动破坏：在爆炸力基础上额外施加随机力度、随距离衰减的震动力；
    /// 方向为每次爆炸、每个零件独立随机的全角度（0~2π）震动方向；
    /// 仅彩灯框与拳套承受震动伤害。
    /// </summary>
    private void ApplyShakeForce(BasePart part, float dist, float maxDist)
    {
        if (part.rigidbody == null || part.rigidbody.IsFixed())
        {
            return; // 静态/冻结刚体（含固定南瓜）不参与震动
        }
        // 震动伤害只作用于彩灯框和拳套，其他零件不受震动
        if (!IsShakeTarget(part))
        {
            return;
        }
        // 真随机方向：每个零件每次爆炸独立掷骰 0~2π 全角度
        float num = s_random.NextSingle(0f, 2f * Mathf.PI);
        Vector3 dir = new Vector3(Mathf.Cos(num), Mathf.Sin(num), 0f);
        float falloff = 1f - dist / maxDist; // 距离越远震动越弱
        float force = ShakeForceScale * s_random.NextSingle(ShakeRandomMin, ShakeRandomMax) * falloff;
        Rigidbody rigidbody = part.rigidbody;
        if (rigidbody.mass < 0.1f)
        {
            force *= rigidbody.mass;
        }
        else if (rigidbody.mass < 0.4f)
        {
            force *= rigidbody.mass / 0.4f;
        }
        rigidbody.EngineAddForce(force * dir, ForceMode.Impulse);
    }

    /// <summary>
    /// 震动伤害目标判定：只有彩灯框（ColoredFrame/灯框）和拳套（SpringBoxingGlove）承受震动，其他零件不受震动。
    /// </summary>
    private static bool IsShakeTarget(BasePart part)
    {
        return part is ColoredFrame || part.IsLightFrame() || part.Type == BasePart.PartType.SpringBoxingGlove;
    }

    /// <summary>
    /// 灼烧单个零件（仿 JetEngine.OnPartBurnt）：
    /// 概率足够时可能引爆燃料箱、烧毁关节，并施加沿灼烧方向的推力；同时更新前部遮挡。
    /// </summary>
    private bool BurnPart(BasePart part, float probability, Vector3 dir, float dist, float[] occlusion, int bucket)
    {
        bool result = false;
        if (IsProtectedFromBurn(part))
        {
            return false;
        }
        float factor = GetBurnDefenseFactor(part);
        float num = probability / factor;
        if (num > 0.025f)
        {
            if (part is FuelBox fuelBox && s_random.NextSingle(0f, 1f) < (num - 0.05f) * 0.1f)
            {
                result = true;
                fuelBox.Explode();
            }
            foreach (Joint item in base.contraption.FindPartJoints(part))
            {
                if (item != null && s_random.NextSingle(0f, 1f) < num - 0.025f)
                {
                    result = true;
                    UnityEngine.Object.Destroy(item);
                }
            }
            float num2 = 1000f * (num - 0.025f);
            if (part.rigidbody != null)
            {
                part.rigidbody.EngineAddForce(num2 * dir);
            }
        }
        if (part.rigidbody != null)
        {
            // 近处零件按方位角遮挡后方（喷气引擎 function.Set 的 360° 变体）
            float r = INContraption.GetBounds(part.rigidbody).R;
            float halfAng = (r >= dist) ? Mathf.PI : Mathf.Asin(r / Mathf.Max(dist, 0.01f));
            float bucketAngle = 2f * Mathf.PI / (float)BurnOcclusionBuckets;
            int spread = Mathf.Max(1, Mathf.CeilToInt(halfAng / bucketAngle));
            for (int i = -spread; i <= spread; i++)
            {
                int b = (bucket + i + BurnOcclusionBuckets) % BurnOcclusionBuckets;
                float num3 = Mathf.Abs(i) / (float)spread;
                if (num3 < 1f)
                {
                    occlusion[b] *= 1f - 0.5f * factor * (1f - num3);
                }
            }
        }
        return result;
    }

    private bool IsProtectedFromBurn(BasePart part)
    {
        // 固定南瓜(customPartIndex == 0)或已冻结刚体属于关卡静态结构, 不受灼烧
        if (part.m_partType == BasePart.PartType.Pumpkin && part.customPartIndex == 0)
        {
            return true;
        }
        return part.rigidbody != null && part.rigidbody.IsFixed();
    }

    private float GetBurnDefenseFactor(BasePart part)
    {
        if (part.IsMetalBox())
        {
            return 1f;
        }
        if (part is JetEngine || part is FuelTube)
        {
            return 0.7f;
        }
        switch (part.GetJointConnectionStrength())
        {
            case JointConnectionStrength.Weak:
            case JointConnectionStrength.Normal:
                return 0.3f;
            case JointConnectionStrength.High:
                return 0.4f;
            case JointConnectionStrength.Extreme:
                return 0.5f;
            case JointConnectionStrength.HighlyExtreme:
                return 0.6f;
            default:
                return 0f;
        }
    }

    public void CheckForTNTAchievement()
    {
        if (!Singleton<SocialGameManager>.IsInstantiated() || !Singleton<GameManager>.Instance.IsInGame())
        {
            return;
        }
        int brokenTNTs = GameProgress.GetInt("Broken_TNTs") + 1;
        GameProgress.SetInt("Broken_TNTs", brokenTNTs);
        ((Action<List<string>>)delegate (List<string> achievements)
        {
            foreach (string achievement in achievements)
            {
                if (Singleton<SocialGameManager>.Instance.TryReportAchievementProgress(achievement, 100.0, (int limit) => brokenTNTs > limit))
                {
                    break;
                }
            }
        })(new List<string> { "grp.BOOM_BOOM_III", "grp.BOOM_BOOM_II", "grp.BOOM_BOOM_I" });
    }

    protected virtual IEnumerator ShineLight()
    {
        PointLightSource pls = GetComponentInChildren<PointLightSource>();
        if ((bool)pls)
        {
            MeshRenderer componentInChildren = base.transform.GetComponentInChildren<MeshRenderer>();
            if ((bool)componentInChildren)
            {
                componentInChildren.enabled = false;
            }
            pls.onLightTurnOff = (Action)Delegate.Combine(pls.onLightTurnOff, (Action)delegate
            {
                UnityEngine.Object.Destroy(base.gameObject);
            });
            pls.isEnabled = true;
            yield return new WaitForSeconds(pls.turnOnCurve[pls.turnOnCurve.length - 1].time);
            pls.isEnabled = false;
        }
        else
        {
            UnityEngine.Object.Destroy(base.gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (customPartIndex == 3 && base.rigidbody != null && !base.HasGeneratorRef && !m_triggered)
        {
            Vector3 velocity = base.rigidbody.velocity;
            float num = 600f;
            float num2 = (velocity - m_lastVelocity).magnitude / Time.fixedDeltaTime;
            m_lastVelocity = velocity;
            if (num2 > num)
            {
                m_explosionImpulse = 0f;
                Explode();
            }
        }
    }

    public override void OnLightEnter(EntityLightCollision collision)
    {
        if (m_partTier != PartTier.Epic && INSettings.GetBool(INFeature.CanLightTriggerExplosion))
        {
            Explode();
        }
    }
}
