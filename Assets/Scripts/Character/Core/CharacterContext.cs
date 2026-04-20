using UnityEngine;

namespace Character.Core
{
    public sealed class CharacterContext
    {
      public CharacterController Controller { get; private set; }
      public Transform Root { get; private set; }
      public Camera ViewCamera{get;private set;}
      public Vector3 Velocity;
      public float MaxHp { get; private set; } = 100f;
      public float CurrentHp { get; private set; } = 100f;
      public bool IsInvincible { get; set; }
      public bool IsDead => CurrentHp <= 0f;

      public CharacterContext(CharacterController controller, Transform root, Camera viewCamera)
      {
        Controller = controller;
        Root = root;
        ViewCamera = viewCamera;
        Velocity = Vector3.zero;
      }

      public void ConfigureHealth(float maxHp)
      {
        MaxHp = Mathf.Max(1f, maxHp);
        CurrentHp = MaxHp;
      }

      public void ApplyDamage(float damage)
      {
        if (IsDead || IsInvincible) return;
        CurrentHp = Mathf.Max(0f, CurrentHp - Mathf.Max(0f, damage));
      }

      public void Revive(float hp)
      {
        CurrentHp = Mathf.Clamp(hp, 1f, MaxHp);
        IsInvincible = false;
      }

      public bool IsGrounded => Controller != null && Controller.isGrounded;

      public void GetCameraBasis(out Vector3 forward, out Vector3 right){
        forward = Vector3.forward;
        right = Vector3.right;

        if(ViewCamera == null) return;

        var f = ViewCamera.transform.forward;
        var r = ViewCamera.transform.right;

        forward = new Vector3(f.x, 0, f.z).normalized;
        right = new Vector3(r.x, 0, r.z).normalized;
      }
    }
}
