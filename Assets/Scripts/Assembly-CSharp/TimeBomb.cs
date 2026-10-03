using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeBomb : BasePart
{
	public class BombOutOfBounds : EventManager.Event
	{
	}

	[SerializeField]
	private float m_explosionImpulse;

	[SerializeField]
	private float m_explosionRadius;

	[SerializeField]
	private float m_triggerSpeed;

	[SerializeField]
	private GameObject smokeCloudPrefab;

	private GameObject m_Visualization;

	[SerializeField]
	private bool m_checkRotation;

	private bool m_triggered;

	public override bool CanBeEnclosed()
	{
		return true;
	}

	public override bool ValidatePart()
	{
		return true;
	}

	public override bool IsTriggerable()
	{
		return false;
	}

	public override void Awake()
	{
		base.Awake();
		m_Visualization = base.transform.Find("Visualization").gameObject;
	}

	private void OnDestroy()
	{
		EventManager.Disconnect<GameStateChanged>(OnGameStateChanged);
	}

	public override void Initialize()
	{
		base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), 1);
		EventManager.Connect<GameStateChanged>(OnGameStateChanged);
		CheckRotations();
	}

	private void OnGameStateChanged(GameStateChanged data)
	{
		if (data.state == LevelManager.GameState.CakeRaceCompleted)
		{
			EventManager.Disconnect<GameStateChanged>(OnGameStateChanged);
			Explode();
		}
	}

	protected override void OnTouch()
	{
		Explode();
	}

	// Multiplayer：拆除（副本被主机 OneOff 流移除）时重放的音效/粒子，供 MultiplayerMachineAnchor.RetirePart 调用
	public override void PlayRemovalEffect()
	{
		if (WPFMonoBehaviour.gameData != null && Singleton<AudioManager>.IsInstantiated())
		{
			Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.tntExplosion, base.transform.position);
		}
		if (WPFMonoBehaviour.effectManager != null)
		{
			WPFMonoBehaviour.effectManager.CreateParticles(smokeCloudPrefab, base.transform.position - Vector3.forward * 12f, force: true);
		}
	}

	public void Explode()
	{
		if (IsMultiplayerReplica() || m_triggered)
		{
			return;
		}
		m_triggered = true;
		base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), -1);
		Collider[] array = Physics.OverlapSphere(base.transform.position, 0f);
		for (int i = 0; i < array.Length; i++)
		{
			TNT component = array[i].GetComponent<TNT>();
			if (component && !component.HasGeneratorRef)
			{
				component.Explode();
			}
		}
		foreach (Collider collider in Physics.OverlapSphere(base.transform.position, 48f))
		{
			GameObject gameObject = FindParentWithRigidBody(collider.gameObject);
			if (gameObject != null)
			{
				int num = CountChildColliders(gameObject, 0);
				AddExplosionForce(gameObject, 1f / (float)num);
			}
		}
		PlayRemovalEffect();
		CheckForAchievements();
		base.contraption.RemovePart(this);
		List<Joint> list = base.contraption.FindPartJoints(this);
		if (list.Count > 0)
		{
			for (int j = 0; j < list.Count; j++)
			{
				bool flag = list[j].gameObject == this || list[j].connectedBody == this;
				if (!float.IsInfinity(list[j].breakForce) || flag)
				{
					UnityEngine.Object.Destroy(list[j]);
				}
			}
			HandleJointBreak();
		}
		else
		{
			HandleJointBreak(playEffects: false);
		}
		StartCoroutine(ShineLight());
		EventManager.Disconnect<GameStateChanged>(OnGameStateChanged);
		EventManager.Send(default(TimeBombExplodeEvent));
	}

	private int CountChildColliders(GameObject obj, int count)
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

	private GameObject FindParentWithRigidBody(GameObject obj)
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

	private void AddExplosionForce(GameObject target, float forceFactor)
	{
		float num = 0f;
		if (m_gridRotation == GridRotation.Deg_0)
		{
			num = 0f;
		}
		else if (m_gridRotation == GridRotation.Deg_90)
		{
			num = 90f;
		}
		else if (m_gridRotation == GridRotation.Deg_180)
		{
			num = 180f;
		}
		else if (m_gridRotation == GridRotation.Deg_270)
		{
			num = 270f;
		}
		else if (m_gridRotation == GridRotation.Deg_45)
		{
			num = 45f;
		}
		else if (m_gridRotation == GridRotation.Deg_135)
		{
			num = 135f;
		}
		else if (m_gridRotation == GridRotation.Deg_225)
		{
			num = 225f;
		}
		else if (m_gridRotation == GridRotation.Deg_315)
		{
			num = 315f;
		}
		Vector3 right = base.transform.right;
		Vector3 vector = Quaternion.Euler(0f, 0f, num - 90f) * right;
		Vector3 vector2 = target.transform.position - base.transform.position;
		Vector3.Dot(vector2, base.transform.right);
		Vector3.Dot(vector2, base.transform.forward);
		if (Vector3.Dot(vector2.normalized, vector) < 0.97f)
		{
			return;
		}
		Rigidbody component = target.GetComponent<Rigidbody>();
		if (component == null)
		{
			return;
		}
		float num2 = Mathf.Max(vector2.magnitude, 1f);
		if (num2 < 5f)
		{
			float num3 = forceFactor * 56f * m_explosionImpulse;
			if (component.mass < 5.5f)
			{
				num3 *= component.mass / num2;
			}
			else if (component.mass >= 5.5f && component.mass < 25f)
			{
				num3 *= (5.5f + 0.1f * (component.mass - 5.5f)) / num2;
			}
			else if (component.mass >= 25f)
			{
				num3 *= 1f / num2;
			}
			component.EngineAddForce(num3 * vector2, ForceMode.Impulse);
			return;
		}
		if (num2 >= 5f)
		{
			float num4 = forceFactor * 270f * m_explosionImpulse / Mathf.Pow(num2, 1.5f);
			if (component.mass < 0.1f)
			{
				num4 *= component.mass;
			}
			else if (component.mass < 0.4f)
			{
				num4 *= component.mass / 0.4f;
			}
			component.EngineAddForce(num4 * vector, ForceMode.Impulse);
		}
	}

	public void CheckForAchievements()
	{
		if (Singleton<SocialGameManager>.IsInstantiated())
		{
			Singleton<GameManager>.Instance.IsInGame();
		}
	}

	private void Update()
	{
		Vector3 position = base.transform.position;
		LevelManager.CameraLimits currentCameraLimits = WPFMonoBehaviour.levelManager.CurrentCameraLimits;
		if (position.x > currentCameraLimits.topLeft.x + currentCameraLimits.size.x * 1.1f || position.x < currentCameraLimits.topLeft.x - currentCameraLimits.size.x * 0.1f)
		{
			EventManager.Send(new BombOutOfBounds());
		}
	}

	private IEnumerator ShineLight()
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

	public override void SetRotation(GridRotation rotation)
	{
		m_gridRotation = rotation;
		m_Visualization.transform.localRotation = Quaternion.AngleAxis(GetRotationAngle(rotation), Vector3.forward);
		ApplyBuildRotation(Quaternion.identity, false);
		CheckRotations();
	}

	private void FlipRotation(Transform target, bool flipX, bool flipY)
	{
		Vector3 localScale = target.localScale;
		if (flipX)
		{
			localScale.x = 0f - Mathf.Abs(localScale.x);
		}
		else
		{
			localScale.x = Mathf.Abs(localScale.x);
		}
		if (flipY)
		{
			localScale.y = 0f - Mathf.Abs(localScale.y);
		}
		else
		{
			localScale.y = Mathf.Abs(localScale.y);
		}
		target.localScale = localScale;
	}

	private void CheckRotations()
	{
		if (m_checkRotation && m_gridRotation == GridRotation.Deg_90)
		{
			FlipRotation(m_Visualization.transform, flipX: true, flipY: false);
		}
		else if (m_checkRotation)
		{
			FlipRotation(m_Visualization.transform, flipX: false, flipY: false);
		}
	}
}
