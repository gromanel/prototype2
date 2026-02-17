using UnityEngine;

public class turnOnLight : MonoBehaviour
{
    [Header("Sphere references")]
    [Tooltip("Sphere 1: Opti-tracked element representing the light")]
    public Transform sphere1;
    [Tooltip("Sphere 2: Reference/target sphere")]
    public Transform sphere2;
    [Tooltip("Sphere 3: Appears when sphere 1 pauses inside its boundary for 3 seconds")]
    public Transform sphere3;

    [Header("Trigger zone")]
    [Tooltip("Radius of sphere 3's boundary (use sphere 3's scale if not set)")]
    public float boundaryRadius = 1f;

    [Tooltip("Seconds sphere 1 must stay inside sphere 3's boundary")]
    public float requiredPauseTime = 3f;

    [Tooltip("Multiply boundary radius by this for 'left zone' (e.g. 1.1 = must be 10% outside to turn off). Stops flicker at edge.")]
    [Min(1f)]
    public float exitRadiusMultiplier = 1.05f;

    [Tooltip("Seconds sphere 1 must stay outside the zone before the light turns off.")]
    public float requiredTimeOutsideToTurnOff = 3f;

    [Tooltip("Sphere 1 is always kept active (only its renderers are hidden) so OptiTrack keeps updating and 'leave zone' can be detected.")]
    public bool keepSphere1InScene = true;

    float _timeInsideBoundary;
    float _timeOutsideBoundary;
    bool _triggered;

    void Start()
    {
        _timeInsideBoundary = 0f;
        _timeOutsideBoundary = 0f;
        _triggered = false;

        // Sphere 3 is hidden at the beginning (renderer-only so this script can live on sphere 3 and still run)
        if (sphere3 != null)
        {
            SetSphereVisible(sphere3, false, rendererOnly: true);
        }
    }

    void Update()
    {
        if (sphere1 == null || sphere3 == null) return;

        float radius = boundaryRadius > 0f ? boundaryRadius : GetSphereRadius(sphere3);
        float dist = Vector3.Distance(sphere1.position, sphere3.position);

        if (_triggered)
        {
            float exitRadius = radius * exitRadiusMultiplier;
            if (dist > exitRadius)
            {
                _timeOutsideBoundary += Time.deltaTime;
                if (_timeOutsideBoundary >= requiredTimeOutsideToTurnOff)
                {
                    _triggered = false;
                    _timeOutsideBoundary = 0f;
                    SetSphereVisible(sphere3, false, rendererOnly: true);
                    if (sphere1 != null)
                        SetSphereVisible(sphere1, true, rendererOnly: true);
                    if (sphere2 != null) SetSphereVisible(sphere2, true);
                }
            }
            else
            {
                _timeOutsideBoundary = 0f; // back inside, reset turn-off timer
            }
            return;
        }

        if (dist <= radius)
        {
            _timeInsideBoundary += Time.deltaTime;
            if (_timeInsideBoundary >= requiredPauseTime)
            {
                _triggered = true;
                // Sphere 3 appears, spheres 1 and 2 disappear
                ShowSphere3();
                if (sphere1 != null)
                    SetSphereVisible(sphere1, false, rendererOnly: true); // keep sphere 1 active so position updates and leave-zone works
                if (sphere2 != null) SetSphereVisible(sphere2, false);
            }
        }
        else
        {
            _timeInsideBoundary = 0f;
        }
    }

    void ShowSphere3()
    {
        if (sphere3 == null) return;
        sphere3.gameObject.SetActive(true);
        var renderers = sphere3.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = true;
        // Activate any inactive children so nested meshes appear
        var all = sphere3.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            all[i].gameObject.SetActive(true);
    }

    void SetSphereVisible(Transform sphere, bool visible, bool rendererOnly = false)
    {
        if (sphere == null) return;
        if (rendererOnly)
        {
            foreach (var r in sphere.GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;
            return;
        }
        sphere.gameObject.SetActive(visible);
    }

    float GetSphereRadius(Transform sphere)
    {
        if (sphere == null) return 1f;
        // Use local scale as approximate radius (sphere is typically uniform)
        return Mathf.Max(sphere.lossyScale.x, sphere.lossyScale.y, sphere.lossyScale.z) * 0.5f;
    }

    // Draw the trigger boundary around SPHERE 3 (so you see the zone in the right place in Scene view)
    void OnDrawGizmosSelected()
    {
        if (sphere3 == null) return;
        float radius = boundaryRadius > 0f ? boundaryRadius : GetSphereRadius(sphere3);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(sphere3.position, radius);
    }
}
