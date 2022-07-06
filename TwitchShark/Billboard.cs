using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Range(0f, 100f)]
    [SerializeField]
    private float hideDistanceFromCamera;
    [SerializeField]
    private GameObject bilboardObject;
    [SerializeField]
    private Transform targetToLookAt;

    private void Update()
    {
        if (base.transform == null || this.targetToLookAt == null)
        {
            return;
        }
        base.transform.rotation = Quaternion.LookRotation(base.transform.position - this.targetToLookAt.position);
        if (this.hideDistanceFromCamera > 0f)
        {
            float num = Vector3.Distance(base.transform.position, this.targetToLookAt.position);
            if (this.bilboardObject.activeInHierarchy && num >= this.hideDistanceFromCamera)
            {
                this.bilboardObject.SetActive(false);
                return;
            }
            if (!this.bilboardObject.activeInHierarchy && num < this.hideDistanceFromCamera)
            {
                this.bilboardObject.SetActive(true);
            }
        }
    }

    public void SetTarget(Transform targetToLookAt)
    {
        this.targetToLookAt = targetToLookAt;
    }

    void LateUpdate()
    {
        if (Camera.main == null) return;
        transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward, Camera.main.transform.rotation * Vector3.up);
    }
}
