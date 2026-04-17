using UnityEngine;

namespace Character.Core
{
    public sealed class CharacterContext
    {
      public CharacterController Controller { get; private set; }
      public Camera ViewCamera{get;private set;}
      public Vector3 Velocity;

      public CharacterContext(CharacterController controller, Camera viewCamera)
      {
        Controller = controller;
        ViewCamera = viewCamera;
        Velocity = Vector3.zero;
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
