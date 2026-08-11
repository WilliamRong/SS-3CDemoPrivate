using UnityEngine;

namespace Character.Core
{
    /// <summary>
    /// 为纯 C# 状态对象提供稳定的角色依赖和易失运行时数据，避免状态直接搜索场景组件。
    /// </summary>
    public sealed class CharacterContext
    {
        public CharacterController Controller { get; private set; }
        public Transform Root { get; private set; }
        public Camera ViewCamera { get; private set; }
        public Vector3 Velocity;
        public float MaxHp { get; private set; } = 100f;
        public float CurrentHp { get; private set; } = 100f;
        public bool IsDead => CurrentHp <= 0f;
        public bool IsGrounded => Controller != null && Controller.isGrounded;

        public CharacterContext(CharacterController controller, Transform root, Camera viewCamera)
        {
            Controller = controller;
            Root = root;
            ViewCamera = viewCamera;
            Velocity = Vector3.zero;
        }

        // ============ 生命状态 ============

        public void ConfigureHealth(float maxHp)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            CurrentHp = MaxHp;
        }

        public void SetHealth(float currentHp, float maxHp)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            CurrentHp = Mathf.Clamp(currentHp, 0f, MaxHp);
        }


        public void ApplyDamage(float damage)
        {
            if (IsDead) return;
            CurrentHp = Mathf.Max(0f, CurrentHp - Mathf.Max(0f, damage));
        }

        public void Revive(float hp)
        {
            CurrentHp = Mathf.Clamp(hp, 1f, MaxHp);
        }

        // ============ 相机坐标基 ============

        /// <summary>
        /// 把相机方向投影到水平面，避免镜头俯仰把垂直分量带入角色移动。
        /// </summary>
        public void GetCameraBasis(out Vector3 forward, out Vector3 right)
        {
            forward = Vector3.forward;
            right = Vector3.right;

            if (ViewCamera == null) return;

            var f = ViewCamera.transform.forward;
            var r = ViewCamera.transform.right;

            forward = new Vector3(f.x, 0, f.z).normalized;
            right = new Vector3(r.x, 0, r.z).normalized;
        }
    }
}
