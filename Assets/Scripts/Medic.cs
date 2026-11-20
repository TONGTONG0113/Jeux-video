using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class Medic : ArmyElement
{
    public const string RedTeamTag = "1";

    const float SearchInterval = 0.35f;
    const float RepathInterval = 0.25f;
    const float RetryDelay = 2.5f;
    const float StallTimeout = 2f;
    const float MinimumProgress = 0.3f;
    const float TargetRepathDistance = 0.5f;

    [Header("Medic Settings")]
    [Min(0.1f)] public float HealAmount = 5f;
    [Min(0.5f)] public float HealRadius = 5f;

    [SerializeField] LineRenderer m_HealingBeam;
    [SerializeField] LineRenderer m_HealingRing;

    // The scene reload creates a fresh Medic and manager, including their target and retry state.
    readonly Dictionary<ArmyElement, float> m_RetryAt = new Dictionary<ArmyElement, float>();
    NavMeshAgent m_Agent;
    NavMeshPath m_Path;
    ArmyElement m_Target;
    float m_NextSearch;
    float m_NextRepath;
    float m_ProgressDeadline;
    Vector3 m_ProgressPosition;
    Vector3 m_LastTargetPosition;
    bool m_Moving;
    bool m_IsDead;

    public ArmyElement CurrentTarget => m_Target;

    bool IsOnNavMesh => m_Agent != null && m_Agent.isActiveAndEnabled && m_Agent.isOnNavMesh;

    void Awake()
    {
        m_Agent = GetComponent<NavMeshAgent>();
        m_Path = new NavMeshPath();
        UpdateHealingEffects(null);
    }

    void Start()
    {
        if (m_Agent.isActiveAndEnabled && !MedicNavigation.TryAttach(m_Agent))
            Debug.LogWarning("Place the RED Medic on the battle NavMesh.", this);

        StopMoving();
    }

    void LateUpdate()
    {
        UpdateHealingEffects(HealOrMove());
    }

    ArmyElement HealOrMove()
    {
        BattleStatsManager stats = BattleStatsManager.Instance;
        if (stats != null && stats.IsBattleEnded)
        {
            // Stop on an off-mesh link without ResetPath completing the traversal.
            if (IsOnNavMesh && m_Agent.isOnOffMeshLink)
                m_Agent.isStopped = true;

            ClearTarget();
            m_Agent.updatePosition = false;
            return null;
        }

        // ArmyManager.Start assigns units by tag after yielding one frame.
        if (ArmyManager is not ArmyManagerRed army || army == null || !IsOnNavMesh ||
            UnitHealth == null || UnitHealth.Value <= 0f || HealAmount <= 0f || HealRadius <= 0f)
        {
            ClearTarget();
            return null;
        }

        // Let a traversal finish before stopping or replacing its path.
        if (m_Agent.isOnOffMeshLink)
            return null;

        if (m_Target is not null && !army.IsInjuredHealTarget(m_Target))
            ClearTarget();

        if (Time.time >= m_NextSearch && (m_Target == null || m_Target is Turret))
            FindTarget(army, dronesOnly: m_Target != null);

        ArmyElement target = m_Target;
        if (target == null)
            return null;

        if (!IsInRange(target.transform.position))
        {
            if (!FollowTarget(target.transform.position))
                RejectTarget(target);

            return null;
        }

        StopMoving();
        Health health = target.UnitHealth;
        float previousHealth = health.Value;
        health.Heal(HealAmount * Time.deltaTime);
        if (!health.CanHeal)
        {
            ClearTarget();
            return null;
        }

        return health.Value > previousHealth ? target : null;
    }

    void FindTarget(ArmyManagerRed army, bool dronesOnly)
    {
        m_NextSearch = Time.time + SearchInterval;
        ArmyElement target = army.GetBestHealTarget(
            transform.position, 0f, float.PositiveInfinity, CanReach, dronesOnly);
        if (target == null)
            return;

        // The manager stops at the first reachable candidate, leaving its path ready to use.
        m_Target = target;
        m_Moving = false;
        Vector3 position = target.transform.position;
        if (!IsInRange(position) && !ApplyPath(position))
            RejectTarget(target);
    }

    bool CanReach(ArmyElement target)
    {
        if (IsInRange(target.transform.position))
            return true;

        if (m_RetryAt.TryGetValue(target, out float retryTime) && Time.time < retryTime)
            return false;

        if (MedicNavigation.TryFindApproach(m_Agent, target.transform.position, HealRadius, m_Path))
        {
            m_RetryAt.Remove(target);
            return true;
        }

        m_RetryAt[target] = Time.time + RetryDelay;
        return false;
    }

    bool FollowTarget(Vector3 position)
    {
        // Updating a path for a moving ally must not reset the stall timer.
        if (m_Moving && (transform.position - m_ProgressPosition).sqrMagnitude >=
            MinimumProgress * MinimumProgress)
        {
            RecordProgress();
        }

        if (m_Moving && Time.time >= m_ProgressDeadline)
            return false;

        if (m_Moving && Time.time < m_NextRepath)
            return true;

        m_NextRepath = Time.time + RepathInterval;
        if (m_Moving && m_Agent.hasPath && !m_Agent.isPathStale &&
            m_Agent.pathStatus == NavMeshPathStatus.PathComplete &&
            (position - m_LastTargetPosition).sqrMagnitude < TargetRepathDistance * TargetRepathDistance)
        {
            return true;
        }

        return MedicNavigation.TryFindApproach(m_Agent, position, HealRadius, m_Path) &&
            ApplyPath(position);
    }

    bool ApplyPath(Vector3 targetPosition)
    {
        if (!m_Agent.SetPath(m_Path))
            return false;

        m_Agent.isStopped = false;
        m_LastTargetPosition = targetPosition;
        m_NextRepath = Time.time + RepathInterval;
        if (!m_Moving)
            RecordProgress();

        m_Moving = true;
        return true;
    }

    void RecordProgress()
    {
        m_ProgressPosition = transform.position;
        m_ProgressDeadline = Time.time + StallTimeout;
    }

    bool IsInRange(Vector3 position)
    {
        return (position - transform.position).sqrMagnitude <= HealRadius * HealRadius;
    }

    void RejectTarget(ArmyElement target)
    {
        // Retry is bounded so that another ally can be helped while this route is blocked.
        m_RetryAt[target] = Time.time + RetryDelay;
        ClearTarget();
    }

    void ClearTarget()
    {
        m_Target = null;
        m_NextSearch = 0f;
        StopMoving();
    }

    void StopMoving()
    {
        m_Moving = false;
        if (!IsOnNavMesh || m_Agent.isOnOffMeshLink)
            return;

        if (m_Agent.hasPath || m_Agent.pathPending)
            m_Agent.ResetPath();
        if (!m_Agent.isStopped)
            m_Agent.isStopped = true;
    }

    void UpdateHealingEffects(ArmyElement target)
    {
        bool healing = target != null;
        if (m_HealingBeam != null)
        {
            if (healing)
            {
                Collider hitbox = target.GetComponent<Collider>();
                Vector3 endpoint = hitbox != null ? hitbox.bounds.center : target.transform.position;
                m_HealingBeam.SetPosition(0, m_HealingBeam.transform.position);
                m_HealingBeam.SetPosition(1, endpoint);
            }

            m_HealingBeam.enabled = healing;
        }

        if (m_HealingRing != null)
            m_HealingRing.enabled = healing;
    }

    public void TakeDamage(float amount)
    {
        if (!m_IsDead && UnitHealth != null && amount > 0f)
            UnitHealth.InflictDamage(amount);
    }

    public new void Die()
    {
        if (m_IsDead)
            return;

        m_IsDead = true;
        enabled = false;
        if (ArmyManager != null)
            ArmyManager.ArmyElementHasBeenKilled(gameObject);

        Destroy(gameObject);
    }

    void OnDisable()
    {
        ClearTarget();
        UpdateHealingEffects(null);
    }
}
