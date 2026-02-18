using UnityEngine;

/// <summary>
/// Media follows Person 1 and is shared when Person 2 approaches Person 1.
/// Designed for OptiTrack: assign Person 1 and Person 2 to transforms driven by OptitrackRigidBody.
///
/// Behavior:
/// - Media is attached to Person 1 and follows their movement at normal size.
/// - When Person 2 enters outer radius: media grows but still follows Person 1.
/// - When Person 2 enters inner radius: media centers along X between both players.
/// - When Person 2 leaves for leaveConfirmSeconds: media returns to following Person 1 at normal size.
/// - If Person 2 was in inner radius for innerRadiusSecondsToSplit or more, when they leave the media
///   duplicates so each person gets their own smaller media attached to them as they move.
/// </summary>
public class TVSceneMediaInteraction : MonoBehaviour
{
    [Header("Motion-tracked objects (OptiTrack rigid bodies or test transforms)")]
    [Tooltip("Player 1 — media stays in front of this when Player 2 is far or has left.")]
    public Transform person1;
    [Tooltip("Player 2 — approaching this player triggers shared view and recentering.")]
    public Transform person2;

    [Header("Radii (world units)")]
    [Tooltip("Outer radius: when Player 2 enters this, media grows but stays in front of Player 1.")]
    public float outerRadius = 12f;
    [Tooltip("Inner radius: when Player 2 enters this, media moves to center between both players.")]
    public float innerRadius = 5f;
    [Tooltip("When not duplicating: Person 2 must stay outside outer radius this long for the shared media to return to Person 1 only. Duplication has no delay.")]
    public float leaveConfirmSeconds = 2f;
    [Tooltip("If Person 2 has been in the inner radius this long (seconds), as they leave the inner radius the media duplicates so each person has their own (starts in outer radius, follows P2 all the way out).")]
    public float innerRadiusSecondsToSplit = 5f;

    [Header("Media attachment to Person 1")]
    [Tooltip("Offset from Person 1's position where media follows (in Person 1's local space or world space).")]
    public Vector3 offsetFromPerson1 = new Vector3(0f, 0f, -12f);
    [Tooltip("If true, offset is relative to Person 1's rotation (follows Person 1's facing). If false, uses world space offset.")]
    public bool usePerson1LocalSpace = true;
    [Tooltip("Height offset for media (world Y). If 0, uses Person 1's Y + offsetFromPerson1.y.")]
    public float mediaHeightOffset = 0f;

    [Header("Scaling")]
    [Tooltip("Scale multiplier when Player 2 is in outer radius or centered (e.g. 1.1 = 10% bigger).")]
    [Min(1f)]
    public float scaleMultiplierWhenShared = 1.15f;

    [Header("Smoothing")]
    [Tooltip("How quickly position and scale lerp toward targets (higher = snappier).")]
    public float positionLerpSpeed = 4f;
    public float scaleLerpSpeed = 4f;

    public enum State
    {
        InFrontOfP1,      // P2 far or has left: media in front of P1, normal size
        OuterRadius,      // P2 in outer radius: media in front of P1, larger
        CenteredBetween,  // P2 in inner radius: media at midpoint, larger
        Exiting           // P2 left; waiting leaveConfirmSeconds before going back to InFrontOfP1
    }

    public State CurrentState => _state;

    State _state = State.InFrontOfP1;
    float _leaveTimer;
    float _timeInInnerRadius;
    Vector3 _initialScale;
    bool _hasInitialScale;
    bool _splitMode;
    int _followWhichPerson; // 1 or 2 when in split mode
    bool _hasSplit;

    void Start()
    {
        if (_splitMode) return; // Clone already configured by EnterSplitMode
        _state = State.InFrontOfP1;
        _leaveTimer = 0f;
        _timeInInnerRadius = 0f;
        _hasSplit = false;
        if (transform != null)
        {
            _initialScale = transform.localScale;
            _hasInitialScale = true;
        }
    }

    /// <summary>Call after instantiate to make this instance follow only one person (split mode).</summary>
    public void EnterSplitMode(int followPerson1Or2, Vector3? normalScale = null)
    {
        _splitMode = true;
        _followWhichPerson = Mathf.Clamp(followPerson1Or2, 1, 2);
        if (normalScale.HasValue)
        {
            _initialScale = normalScale.Value;
            _hasInitialScale = true;
        }
    }

    void Update()
    {
        if (person1 == null || person2 == null)
            return;

        if (!_hasInitialScale)
        {
            _initialScale = transform.localScale;
            _hasInitialScale = true;
        }

        if (_splitMode)
        {
            Transform follow = _followWhichPerson == 2 ? person2 : person1;
            Vector3 targetPos = GetPositionFollowingPerson(follow);
            transform.position = Vector3.Lerp(transform.position, targetPos, positionLerpSpeed * Time.deltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, _initialScale, scaleLerpSpeed * Time.deltaTime);
            return;
        }

        float dist = Vector3.Distance(person1.position, person2.position);

        switch (_state)
        {
            case State.InFrontOfP1:
                if (dist <= outerRadius)
                {
                    if (dist <= innerRadius)
                        _state = State.CenteredBetween;
                    else
                        _state = State.OuterRadius;
                }
                break;

            case State.OuterRadius:
                if (dist <= innerRadius)
                    _state = State.CenteredBetween;
                else if (dist > outerRadius)
                {
                    _state = State.Exiting;
                    _leaveTimer = 0f;
                }
                break;

            case State.CenteredBetween:
                _timeInInnerRadius += Time.deltaTime;
                if (dist > innerRadius)
                {
                    // Person 2 is leaving the inner radius; duplicate now if they were here 5+ sec (no 2s wait)
                    if (_timeInInnerRadius >= innerRadiusSecondsToSplit && !_hasSplit)
                    {
                        DoSplit();
                        return; // this frame we're now in split mode; Update will handle position next frame
                    }
                    if (dist > outerRadius)
                    {
                        _state = State.Exiting;
                        _leaveTimer = 0f;
                    }
                    else
                        _state = State.OuterRadius;
                }
                break;

            case State.Exiting:
                if (dist <= outerRadius)
                {
                    _leaveTimer = 0f;
                    if (dist <= innerRadius)
                        _state = State.CenteredBetween;
                    else
                        _state = State.OuterRadius;
                }
                else
                {
                    _leaveTimer += Time.deltaTime;
                    if (_leaveTimer >= leaveConfirmSeconds)
                    {
                        // Media returns to Person 1 only (no duplication)
                        _state = State.InFrontOfP1;
                        _timeInInnerRadius = 0f;
                        _leaveTimer = 0f;
                    }
                }
                break;
        }

        Vector3 targetPosition = GetTargetPosition();
        Vector3 targetScale = GetTargetScale();

        transform.position = Vector3.Lerp(transform.position, targetPosition, positionLerpSpeed * Time.deltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleLerpSpeed * Time.deltaTime);
    }

    void DoSplit()
    {
        _hasSplit = true;
        EnterSplitMode(1);

        GameObject clone = Instantiate(gameObject);
        clone.name = gameObject.name + " (Person 2)";
        TVSceneMediaInteraction cloneScript = clone.GetComponent<TVSceneMediaInteraction>();
        if (cloneScript != null)
            cloneScript.EnterSplitMode(2, _initialScale);

        _timeInInnerRadius = 0f;
    }

    Vector3 GetTargetPosition()
    {
        if (_state == State.CenteredBetween)
        {
            // Center only along X; keep same Z (depth) as when following Person 1
            float centerX = (person1.position.x + person2.position.x) * 0.5f;
            Vector3 followP1 = GetPositionFollowingPerson1();
            float y = mediaHeightOffset != 0f ? mediaHeightOffset : followP1.y;
            return new Vector3(centerX, y, followP1.z);
        }

        // InFrontOfP1, OuterRadius, or Exiting: media follows Person 1
        return GetPositionFollowingPerson1();
    }

    Vector3 GetPositionFollowingPerson1()
    {
        return GetPositionFollowingPerson(person1);
    }

    Vector3 GetPositionFollowingPerson(Transform person)
    {
        Vector3 basePosition;
        
        if (usePerson1LocalSpace)
        {
            basePosition = person.position + person.rotation * offsetFromPerson1;
        }
        else
        {
            basePosition = person.position + offsetFromPerson1;
        }

        if (mediaHeightOffset != 0f)
        {
            basePosition.y = mediaHeightOffset;
        }
        else if (!usePerson1LocalSpace)
        {
            basePosition.y = person.position.y + offsetFromPerson1.y;
        }

        return basePosition;
    }

    Vector3 GetTargetScale()
    {
        if (!_hasInitialScale)
            return transform.localScale;

        bool useSharedScale = _state == State.OuterRadius || _state == State.CenteredBetween;
        float mult = useSharedScale ? scaleMultiplierWhenShared : 1f;
        return _initialScale * mult;
    }

    void OnValidate()
    {
        innerRadius = Mathf.Clamp(innerRadius, 0.1f, outerRadius - 0.1f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (person1 == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(person1.position, outerRadius);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.4f);
        Gizmos.DrawWireSphere(person1.position, innerRadius);
    }
#endif
}
