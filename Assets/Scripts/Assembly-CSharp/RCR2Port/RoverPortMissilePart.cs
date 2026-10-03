using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// The missile launcher ported from RC3.apk (Cocos Creator sand-box car
    /// builder, part id 70012, localization key 40368 "导弹"). Source values
    /// were recovered by decrypting the game's script bundle and prefab - see
    /// Tools/RCR2/ExtractRC3Missile.py. Original behaviour: the trigger
    /// button fires a missile from the launcher nose after an 8.2 s cooldown
    /// (the first shot is instant); the missile flies straight, homes on a
    /// random enemy part at 90°/s (6x when close), and after hitting
    /// something or after a 3 s lifetime explodes, damaging everything in
    /// its blast.
    ///
    /// This port keeps those numbers but replaces the source game's
    /// boom-and-damage with BPLE's own TNT explosion (same OverlapSphere /
    /// impulse falloff / TNT chain reaction / smoke cloud / audio pipeline),
    /// and homes on BPLE pigs instead of enemy car parts, which have no
    /// equivalent in the sandbox.
    /// </summary>
    public class RoverPortMissilePart : RoverPortPart
    {
        [SerializeField] private float m_cooldown = 8.2f;
        [SerializeField] private float m_explosionRadius = 4.5f;
        [SerializeField] private float m_explosionImpulse = 16f;
        [SerializeField] private float m_projectileSpeed = 78.125f;   // 2500 px/s at 32 px per unit
        [SerializeField] private float m_projectileLifeTime = 7f;
        [SerializeField] private float m_homingRotateSpeed = 90f;
        [SerializeField] private float m_homingCloseRange = 6f;
        [SerializeField] private float m_homingCloseMultiplier = 6f;
        [SerializeField] private float m_recoilImpulse = 120f;
        // (A `m_muzzleOffset = 0.95f` used to sit here. Nothing ever read it - the muzzle and the
        // projectile both come from the part's own `FireFlash` / `ProjectileArt` nodes - so it only
        // produced CS0414 and a dead knob in the Inspector.)

        private float m_currentTime;
        private bool m_fired;

        public float Cooldown => m_cooldown;
        public float CurrentTime => m_currentTime;
        public bool HasFired => m_fired;

        /// <summary>Sprite carried by a named art node of the port data
        /// ("ProjectileArt" = the fired projectile, "FireFlash" = the muzzle
        /// flash; both nodes are inactive and only exist to transport their
        /// sprites).</summary>
        private RoverPortSpriteData FindNodeSprite(string nodeName)
        {
            if (m_portData == null)
            {
                return null;
            }
            string nodeId = null;
            foreach (RoverPortNodeData node in m_portData.nodes ?? new RoverPortNodeData[0])
            {
                if (node != null && node.name == nodeName)
                {
                    nodeId = node.id;
                    break;
                }
            }
            if (nodeId == null)
            {
                return null;
            }
            // The art lives in both renderer sets (construction ME tree and
            // physics PE tree); a sprite may be carried by either.
            foreach (RoverPortRendererData renderer in m_portData.renderers ?? new RoverPortRendererData[0])
            {
                if (renderer != null && renderer.sprite != null && renderer.node == nodeId)
                {
                    return renderer.sprite;
                }
            }
            foreach (RoverPortRendererData renderer in m_portData.construction_renderers ?? new RoverPortRendererData[0])
            {
                if (renderer != null && renderer.sprite != null && renderer.node == nodeId)
                {
                    return renderer.sprite;
                }
            }
            return null;
        }

        public override Direction EffectDirection()
        {
            // The source mount points the missile along the launcher's up axis; rotating the part
            // moves the trigger to the matching direction bucket.
            return BasePart.Rotate(Direction.Up, m_gridRotation);
        }

        public override void Initialize()
        {
            base.Initialize();
            contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), 1);
        }

        protected virtual void Update()
        {
            if (m_fired)
            {
                m_currentTime += Time.deltaTime;
            }
        }

        protected override void OnTouch()
        {
            TryFire();
        }

        public bool IsReady()
        {
            return !m_fired || m_currentTime >= m_cooldown;
        }

        public bool TryFire()
        {
            if (!IsReady())
            {
                return false;
            }
            Rigidbody body = rigidbody;
            if (body == null)
            {
                // The trigger button only exists while the contraption runs,
                // which is also when EnsureRigidbody has created the root
                // body; guard anyway so build-mode clicks are inert.
                return false;
            }
            m_fired = true;
            m_currentTime = 0f;

            Transform missile = FindNode("PE:4");
            Vector3 direction = missile != null ? missile.right : transform.up;
            Vector3 position = missile != null ? missile.position : transform.position;
            SpawnProjectile(position, direction);

            // RC3 recoil: an impulse on the launcher opposite to the shot.
            body.EngineAddForce(-direction * m_recoilImpulse, ForceMode.Impulse);
            PlayLaunchAudio(position);
            ShowMuzzleFlash(position, direction);
            return true;
        }

        /// <summary>INUnity.LoadMaterial throws when the resource table has
        /// not been initialized (edit-mode audits); fall back to a plain
        /// sprite shader material instead.</summary>
        private static Material LoadPartMaterial()
        {
            try
            {
                Material material = INUnity.LoadMaterial("IngameAtlas");
                if (material != null)
                {
                    return material;
                }
            }
            catch (Exception)
            {
            }
            Shader shader = Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) : null;
        }

        /// <summary>
        /// A material showing exactly this sprite's texture. The template is
        /// BPLE's shared IngameAtlas - it must be cloned and re-textured,
        /// otherwise the quad's full [0,1] UVs sample a random region of the
        /// shared atlas. Extra-catalog textures are not in RoverPortCatalog's
        /// texture table, so the texture loads straight from Resources.
        /// </summary>
        private static Material LoadSpriteMaterial(RoverPortSpriteData spriteData)
        {
            if (spriteData == null)
            {
                return null;
            }
            Texture2D texture = RoverPortCatalog.Instance.GetTexture(spriteData.texture_guid);
            if (texture == null && !string.IsNullOrEmpty(spriteData.texture_file))
            {
                texture = Resources.Load<Texture2D>("RCR2Ported/Textures/"
                    + System.IO.Path.GetFileNameWithoutExtension(spriteData.texture_file));
            }
            if (texture == null)
            {
                return null;
            }
            Material template = LoadPartMaterial();
            Material material = template != null ? new Material(template) : null;
            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                material = shader != null ? new Material(shader) : null;
            }
            if (material != null)
            {
                material.name = "RCR2_" + spriteData.name;
                material.mainTexture = texture;
                material.hideFlags = HideFlags.DontSave;
            }
            return material;
        }

        private void SpawnProjectile(Vector3 position, Vector3 direction)
        {
            RoverPortSpriteData projectileSprite = FindNodeSprite("ProjectileArt");
            if (projectileSprite == null)
            {
                Debug.LogWarning("[RCR2Missile] no projectile sprite on " + name + " - fire aborted");
                return;
            }
            GameObject projectile = new GameObject("RC3MissileProjectile");
            projectile.transform.position = position;
            projectile.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            projectile.layer = LayerMask.NameToLayer("Contraption");
            RoverPortRC3MissileProjectile component = projectile.AddComponent<RoverPortRC3MissileProjectile>();
            Material material = LoadPartMaterial();
            component.Launch(projectileSprite, LoadSpriteMaterial(projectileSprite), direction, m_projectileSpeed,
                m_projectileLifeTime, m_homingRotateSpeed, m_homingCloseRange, m_homingCloseMultiplier,
                m_explosionRadius, m_explosionImpulse, contraption, contraption != null ? contraption.gameObject : gameObject);
        }

        /// <summary>WPFMonoBehaviour.gameData walks into the GameManager
        /// singleton and throws outside play mode; audit-safe accessor.</summary>
        private static CommonAudio SafeCommonAudio()
        {
            try
            {
                GameData gameData = WPFMonoBehaviour.gameData;
                return gameData != null ? gameData.commonAudioCollection : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void PlayLaunchAudio(Vector3 position)
        {
            CommonAudio audio = SafeCommonAudio();
            if (audio != null && audio.grapplingHookLaunch != null && Singleton<AudioManager>.IsInstantiated())
            {
                Singleton<AudioManager>.Instance.SpawnOneShotEffect(audio.grapplingHookLaunch, position);
            }
        }

        private void ShowMuzzleFlash(Vector3 position, Vector3 direction)
        {
            RoverPortSpriteData flashSprite = FindNodeSprite("FireFlash");
            if (flashSprite == null)
            {
                return;
            }
            Material material = LoadSpriteMaterial(flashSprite);
            if (material == null)
            {
                return;
            }
            GameObject flash = new GameObject("RC3MissileFlash");
            flash.transform.position = position + direction * 0.3f;
            flash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            flash.AddComponent<MeshFilter>().sharedMesh = RoverPortMeshSprite.BuildMesh(flashSprite, false, false);
            MeshRenderer renderer = flash.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 50;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            flash.AddComponent<RoverPortTimedHide>().duration = 0.25f;
        }
    }

    /// <summary>One-shot sprite that hides itself after a short time.</summary>
    public class RoverPortTimedHide : MonoBehaviour
    {
        public float duration = 0.25f;
        private float m_time;

        private void Update()
        {
            m_time += Time.deltaTime;
            if (m_time >= duration)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// The RC3 missile projectile: straight flight with pig homing, explodes
    /// on the first contact (or at lifetime end) using BPLE's TNT explosion.
    /// </summary>
    public class RoverPortRC3MissileProjectile : MonoBehaviour
    {
        private Vector3 m_direction;
        private float m_speed;
        private float m_lifeTime;
        private float m_rotateSpeed;
        private float m_closeRange;
        private float m_closeMultiplier;
        private float m_explosionRadius;
        private float m_explosionImpulse;
        private Contraption m_ownerContraption;
        private GameObject m_ownerRoot;
        private Transform m_target;
        private Rigidbody m_body;
        private float m_probeDistance;
        private bool m_exploded;

        public void Launch(RoverPortSpriteData spriteData, Material material,
            Vector3 direction, float speed, float lifeTime, float rotateSpeed, float closeRange,
            float closeMultiplier, float explosionRadius, float explosionImpulse, Contraption owner,
            GameObject ownerRoot)
        {
            m_direction = direction.normalized;
            m_speed = speed;
            m_lifeTime = lifeTime;
            m_rotateSpeed = rotateSpeed;
            m_closeRange = closeRange;
            m_closeMultiplier = closeMultiplier;
            m_explosionRadius = explosionRadius;
            m_explosionImpulse = explosionImpulse;
            m_ownerContraption = owner;
            m_ownerRoot = ownerRoot;

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = RoverPortMeshSprite.BuildMesh(spriteData, false, false);
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(Mathf.Max(0.3f, spriteData.width / spriteData.ppu * 0.8f), 0.3f, 1f);
            // The warhead is the sprite's nose end: probe ahead of the centre
            // so a long missile detonates when its nose reaches something.
            m_probeDistance = collider.size.x * 0.5f;

            m_body = gameObject.AddComponent<Rigidbody>();
            m_body.constraints = (RigidbodyConstraints)56;   // planar, like the parts
            m_body.useGravity = false;                        // RC3 bullet: gravityScale 0
            m_body.drag = 0f;
            m_body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            m_body.velocity = m_direction * m_speed;

            if (ownerRoot != null)
            {
                // The missile must not physically bounce off its own launcher
                // (or, at runtime, any other part of its contraption).
                foreach (Collider partCollider in ownerRoot.GetComponentsInChildren<Collider>(true))
                {
                    GamePhysics.IgnoreCollision(collider, partCollider, true);
                }
            }
            AcquireTarget();
        }

        private void FixedUpdate()
        {
            Step(Time.fixedDeltaTime);
        }

        /// <summary>One flight step. Public so edit-mode audits can drive the
        /// flight manually (Physics.Simulate does not run MonoBehaviours).</summary>
        public void Step(float deltaTime)
        {
            if (m_exploded)
            {
                return;
            }
            if (m_target == null)
            {
                AcquireTarget();
            }
            else
            {
                SteerTowards(m_target.position);
            }
            m_body.velocity = m_direction * m_speed;
            float angle = Mathf.Atan2(m_direction.y, m_direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            ProbeCollision();
            m_lifeTime -= deltaTime;
            if (m_lifeTime <= 0f)
            {
                Explode();
            }
        }

        /// <summary>
        /// RC3 detonates the missile through its contact sensors; probe for
        /// anything solid near the warhead so the explosion does not depend
        /// on a physics contact callback (fast flight can tunnel, and the
        /// collision matrix may keep some layers apart).
        /// </summary>
        private void ProbeCollision()
        {
            const float probeRadius = 0.35f;
            Vector3 probeCenter = transform.position + m_direction * m_probeDistance;
            foreach (Collider collider in Physics.OverlapSphere(probeCenter, probeRadius))
            {
                if (collider.attachedRigidbody == m_body)
                {
                    continue;
                }
                if (m_ownerRoot != null && collider.transform.root == m_ownerRoot.transform)
                {
                    continue;
                }
                Explode();
                return;
            }
        }

        public void OnCollisionEnter(UnityEngine.Collision collision) { OnPhysicsCollisionEnter(new PhysicsCollision(collision)); }

	private void OnPhysicsCollisionEnter(PhysicsCollision collision)
        {
            if (m_exploded)
            {
                return;
            }
            // RC3: the missile only ever detonates away from its own vehicle;
            // it flies through the launcher (and its contraption) untouched.
            if (m_ownerRoot != null && collision.transform.root == m_ownerRoot.transform)
            {
                return;
            }
            Explode();
        }

        /// <summary>RC3 picks a random enemy part; the sandbox equivalent is the nearest pig.</summary>
        private void AcquireTarget()
        {
            Pig best = null;
            float bestDistance = float.MaxValue;
            Pig[] pigs = FindObjectsOfType<Pig>();
            foreach (Pig pig in pigs)
            {
                if (pig == null || !pig.gameObject.activeInHierarchy)
                {
                    continue;
                }
                float distance = Vector3.Distance(pig.transform.position, transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pig;
                }
            }
            m_target = best != null ? best.transform : null;
        }

        private void SteerTowards(Vector3 targetPosition)
        {
            Vector3 toTarget = targetPosition - transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }
            float current = Mathf.Atan2(m_direction.y, m_direction.x) * Mathf.Rad2Deg;
            float desired = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float delta = desired - current;
            if (delta > 180f) delta -= 360f;
            if (delta < -180f) delta += 360f;
            float rotate = m_rotateSpeed * Time.deltaTime;
            if (toTarget.magnitude < m_closeRange)
            {
                rotate *= m_closeMultiplier;
            }
            float turned = current + Mathf.Min(Mathf.Abs(delta), rotate) * Mathf.Sign(delta);
            float radians = turned * Mathf.Deg2Rad;
            m_direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        }

        /// <summary>BPLE's own TNT explosion - same force falloff, TNT chain
        /// reaction, smoke cloud and audio as <see cref="TNT.Explode"/>.</summary>
        private void Explode()
        {
            if (m_exploded)
            {
                return;
            }
            m_exploded = true;
            Vector3 position = transform.position;
            float radius = m_explosionRadius * INSettings.GetFloat(INFeature.TNTExplosionRadius);
            Collider[] colliders = Physics.OverlapSphere(position, radius);
            foreach (Collider collider in colliders)
            {
                GameObject target = FindParentWithRigidBody(collider.gameObject);
                if (target != null)
                {
                    int count = CountChildColliders(target, 0);
                    AddExplosionForce(target, INSettings.GetFloat(INFeature.TNTExplosionForce) / count);
                }
                BasePart part = collider.GetComponent<BasePart>();
                if (part is TNT tnt && !(part is AlienTNT) && !part.HasGeneratorRef)
                {
                    tnt.Explode();
                }
            }
            if (Singleton<AudioManager>.IsInstantiated() && WPFMonoBehaviour.gameData != null
                && WPFMonoBehaviour.gameData.commonAudioCollection != null)
            {
                Singleton<AudioManager>.Instance.SpawnOneShotEffect(
                    WPFMonoBehaviour.gameData.commonAudioCollection.tntExplosion, position);
            }
            GameObject smokeCloud = LoadSmokeCloud();
            if (smokeCloud != null && WPFMonoBehaviour.effectManager != null)
            {
                WPFMonoBehaviour.effectManager.CreateParticles(smokeCloud, position - Vector3.forward * 5f, true);
            }
            // SetActive before Destroy so edit-mode physics simulation (which
            // never runs the deferred destroy) still sees the missile as gone.
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private static GameObject LoadSmokeCloud()
        {
            try
            {
                GameData gameData = WPFMonoBehaviour.gameData;
                if (gameData == null)
                {
                    return null;
                }
                GameObject tntPrefab = gameData.GetPart(BasePart.PartType.TNT);
                TNT tnt = tntPrefab != null ? tntPrefab.GetComponentInChildren<TNT>() : null;
                return tnt != null ? tnt.smokeCloud : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GameObject FindParentWithRigidBody(GameObject obj)
        {
            if (obj.GetComponent<Rigidbody>())
            {
                return obj;
            }
            if (obj.transform.parent != null)
            {
                return FindParentWithRigidBody(obj.transform.parent.gameObject);
            }
            return null;
        }

        private static int CountChildColliders(GameObject obj, int count)
        {
            if (obj.GetComponent<Collider>())
            {
                count++;
            }
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                count = CountChildColliders(obj.transform.GetChild(i).gameObject, count);
            }
            return count;
        }

        private void AddExplosionForce(GameObject target, float forceFactor)
        {
            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }
            Vector3 delta = target.transform.position - transform.position;
            float distance = Mathf.Max(delta.magnitude, 1f);
            float impulse = forceFactor * m_explosionImpulse / Mathf.Pow(distance, 1.5f);
            if (body.mass < 0.1f)
            {
                impulse *= body.mass;
            }
            else if (body.mass < 0.4f)
            {
                impulse *= body.mass / 0.4f;
            }
            Pig pig = target.GetComponent<Pig>();
            if (pig != null)
            {
                pig.PrepareForTNT(transform.position, impulse);
                impulse *= 1.15f;
            }
            body.EngineAddForce(impulse * delta.normalized, ForceMode.Impulse);
        }
    }
}
