using UnityEngine;
using UnityEngine.AI;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Déplacement latéral pendant le Wait de fin de boucle (exécuté en parallèle
/// du Wait via un ParallelSelector).
///
/// Constat en simulation : les drones verts touchent ~35-40 % de leurs missiles,
/// alors qu'ils visent la position ACTUELLE de leur cible, sans anticipation,
/// avec des missiles parallèles. Un drone rouge qui reste immobile pendant le
/// Wait (1 à 2 s) est une cible facile ; un drone qui se décale latéralement
/// fait rater une grande partie de ces tirs.
///
/// La cadence de tir n'est pas modifiée : le Wait (1-2 s, Annexe A) est toujours
/// exécuté, le drone bouge simplement pendant ce temps.
/// Cette tâche ne se termine jamais d'elle-même (Running) : c'est le Wait qui
/// met fin au ParallelSelector.
/// </summary>
[TaskCategory("MyTasks/Red Drone")]
[TaskDescription("RED drone: strafes sideways while waiting for the next volley (never ends by itself, run it in a ParallelSelector with Wait).")]
public class RedDroneStrafe : Action
{
    public SharedTransform target;

    [BehaviorDesigner.Runtime.Tasks.Tooltip("Annexe A : vitesse du drone (non modifiée)")]
    public SharedFloat speed = 10f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Annexe A : vitesse angulaire du drone (non modifiée)")]
    public SharedFloat angularSpeed = 180f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Distance minimale à garder avec la cible")]
    public SharedFloat minRange = 8f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Distance maximale à garder avec la cible")]
    public SharedFloat maxRange = 18f;
    public SharedFloat arriveTolerance = 1f;

    NavMeshAgent m_Agent;
    GameObject m_Id;
    float m_Side = 1f;
    bool m_HasPoint;
    Vector3 m_Point;
    float m_PointTime;

    public override void OnAwake()
    {
        m_Agent = GetComponent<NavMeshAgent>();
        m_Id = gameObject;
        m_Side = Random.value < 0.5f ? 1f : -1f;
    }

    public override void OnStart()
    {
        m_HasPoint = false;
        if (m_Agent != null)
        {
            m_Agent.speed = speed.Value;
            m_Agent.angularSpeed = angularSpeed.Value;
            m_Agent.updateRotation = true;
            m_Agent.isStopped = false;
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (m_Agent == null || !m_Agent.isOnNavMesh) return TaskStatus.Running;

        // Contre une tourelle on reste dans son angle mort : pas de strafe.
        Transform t = target.Value;
        if (t != null && t.GetComponent<Turret>() != null) return TaskStatus.Running;

        bool arrived = m_HasPoint &&
                       RedDroneTactics.FlatDistance(transform.position, m_Point) <= arriveTolerance.Value;
        bool tooLong = m_HasPoint && Time.time - m_PointTime > 2f;

        if (!m_HasPoint || arrived || tooLong)
        {
            Vector3 p;
            float chosenSide;
            if (RedDroneTactics.FindStrafePoint(
                    transform, m_Id, gameObject.tag, RedDroneTactics.IsAlive(t) ? t : null,
                    minRange.Value, maxRange.Value, m_Agent.baseOffset, -m_Side,
                    out p, out chosenSide))
            {
                m_Point = p;
                m_Side = chosenSide;
                m_HasPoint = true;
                m_PointTime = Time.time;
                RedDroneTactics.ReserveSlot(m_Id, p);
                m_Agent.isStopped = false;
                m_Agent.SetDestination(p);
            }
        }
        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        if (m_Agent != null && m_Agent.isOnNavMesh && m_Agent.hasPath)
            m_Agent.isStopped = true;
    }

    public override void OnReset()
    {
        target = null;
        speed = 10f;
        angularSpeed = 180f;
        minRange = 8f;
        maxRange = 18f;
        arriveTolerance = 1f;
    }
}
