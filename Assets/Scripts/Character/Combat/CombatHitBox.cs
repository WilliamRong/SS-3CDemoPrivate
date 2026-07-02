using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Character.Combat
{

	[DisallowMultipleComponent]
	[RequireComponent(typeof(Collider))]
	public sealed class CombatHitBox : MonoBehaviour
	{
		private static readonly List<CombatHitBox> Active = new();

		[SerializeField] private CombatActor _owner;
		[SerializeField] private Collider _shapeCollider;
		[SerializeField] private HitBoxSlot _slot = HitBoxSlot.Katana;
		[SerializeField] private LayerMask _hurtBoxMask;
		[SerializeField] private QueryTriggerInteraction _queryTriggerInteraction = QueryTriggerInteraction.Collide;

		public static IReadOnlyList<CombatHitBox> ActiveHitBoxes => Active;
		public CombatActor Owner => _owner;
		public HitBoxSlot Slot => _slot;

		private void Reset()
		{
			EnsureReferences();
		}

		private void Awake()
		{
			EnsureReferences();
		}

		private void OnEnable()
		{
			if (!Active.Contains(this))
			{
				Active.Add(this);
			}
		}

		private void OnDisable()
		{
			Active.Remove(this);
		}


		public bool IsConfigured()
		{
			EnsureReferences();
			return _owner != null && _shapeCollider != null && _slot != HitBoxSlot.None;
		}

		public int OverlapHurtBoxesNonAlloc(Collider[] results)
		{
			if (!IsConfigured() || results == null || results.Length == 0)
			{
				return 0;
			}

			return _shapeCollider switch
			{
				CapsuleCollider capsule => OverlapCapsule(capsule, results),
				BoxCollider box => OverlapBox(box, results),
				SphereCollider sphere => OverlapSphere(sphere, results),
				_ => OverlapBounds(_shapeCollider, results)
			};
		}



		private void EnsureReferences()
		{
			if (_owner == null)
			{
				_owner = GetComponentInParent<CombatActor>();
			}

			if (_shapeCollider == null)
			{
				_shapeCollider = GetComponent<Collider>();
			}

			if (_shapeCollider != null)
			{
				_shapeCollider.isTrigger = true;
			}

			if (_hurtBoxMask.value == 0)
			{
				int mask = LayerMask.GetMask("HurtBox");
				_hurtBoxMask = mask != 0 ? mask : ~0;
			}
		}


		private int OverlapCapsule(CapsuleCollider capsule, Collider[] results)
		{
			Transform tr = capsule.transform;
			Vector3 center = tr.TransformPoint(capsule.center);
			Vector3 scale = Abs(tr.lossyScale);

			Vector3 axis;
			float radiusScale;
			float heightScale;

			switch (capsule.direction)
			{
				case 0:
					axis = tr.right;
					radiusScale = Mathf.Max(scale.y, scale.z);
					heightScale = scale.x;
					break;
				case 1:
					axis = tr.up;
					radiusScale = Mathf.Max(scale.x, scale.z);
					heightScale = scale.y;
					break;
				default:
					axis = tr.forward;
					radiusScale = Mathf.Max(scale.x, scale.y);
					heightScale = scale.z;
					break;
			}

			float radius = Mathf.Max(0.001f, capsule.radius * radiusScale);
			float height = Mathf.Max(radius * 2f, capsule.height * heightScale);
			float halfSegment = Mathf.Max(0f, height * 0.5f - radius);

			Vector3 p0 = center + axis * halfSegment;
			Vector3 p1 = center - axis * halfSegment;

			return Physics.OverlapCapsuleNonAlloc(
				p0,
				p1,
				radius,
				results,
				_hurtBoxMask,
				_queryTriggerInteraction);
		}

		private int OverlapBox(BoxCollider box, Collider[] results)
		{
			Transform tr = box.transform;
			Vector3 center = tr.TransformPoint(box.center);
			Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, Abs(tr.lossyScale));

			return Physics.OverlapBoxNonAlloc(
				center,
				halfExtents,
				results,
				tr.rotation,
				_hurtBoxMask,
				_queryTriggerInteraction);
		}

		private int OverlapSphere(SphereCollider sphere, Collider[] results)
		{
			Transform tr = sphere.transform;
			Vector3 center = tr.TransformPoint(sphere.center);
			Vector3 scale = Abs(tr.lossyScale);
			float radius = sphere.radius * Mathf.Max(scale.x, scale.y, scale.z);

			return Physics.OverlapSphereNonAlloc(
				center,
				radius,
				results,
				_hurtBoxMask,
				_queryTriggerInteraction);
		}

		private int OverlapBounds(Collider col, Collider[] results)
		{
			Bounds bounds = col.bounds;
			return Physics.OverlapBoxNonAlloc(
				bounds.center,
				bounds.extents,
				results,
				Quaternion.identity,
				_hurtBoxMask,
				_queryTriggerInteraction);
		}

		private static Vector3 Abs(Vector3 v)
		{
			return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
		}
	}
}
