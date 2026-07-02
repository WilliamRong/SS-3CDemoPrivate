using UnityEngine;

public class HitTest : MonoBehaviour
{
    private void Start()
    {
        // var col = GetComponent<Collider>();
        // var rb = GetComponentInParent<Rigidbody>();
        // Debug.Log($"[HitTest] Start {name} layer={LayerMask.LayerToName(gameObject.layer)} " +
        //           $"collider={col} isTrigger={(col!=null && col.isTrigger)} " +
        //           $"attachedRB={(rb!=null ? rb.name : "<none>")} isKinematic={(rb!=null && rb.isKinematic)}", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Debug.Log($"[HitTest] Enter self={name}({LayerMask.LayerToName(gameObject.layer)}) " +
        //           $"other={other.name}({LayerMask.LayerToName(other.gameObject.layer)})", this);
    }


}
