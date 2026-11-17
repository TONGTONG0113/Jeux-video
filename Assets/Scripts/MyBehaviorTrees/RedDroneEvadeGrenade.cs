using UnityEngine;
using UnityEngine.AI;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Esquive : le drone sort de la zone d'impact prévue des grenades ennemies.
/// Une grenade met 4 s à tomber ; un drone (10 m/s, 8 m/s²) sort d'un rayon de 7 m
/// en ~1,3 s. Le point de fuite reste si possible à portée de la cible et
/// espacé des autres drones rouges.
/// </summary>
[TaskCategory("MyTasks/Red Drone")]
[TaskDescription("RED drone: runs out of the predicted blast zone of enemy grenades.")]
public class RedDroneEvadeGrenade : Action
{
    public SharedTransform target;

    [BehaviorDesigner.Runtime.Tasks.Tooltip("Annexe A : vitesse du drone (non modifiée)")]
    public SharedFloat speed = 10f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Annexe A : vitesse angulaire du drone (non modifiée)")]
    public SharedFloat angularSpeed = 180f;
    public SharedFloat dangerRadius = 7f;
    public SharedFloat recomputeInterval = 0.25f;
    public SharedFloat timeout = 3f;

    NavMeshAgent m_Agent;
    GameObject m_Id;
    float m_StartTime;
    float m_NextRecomputeTime;

    public override void OnAwake()
    {
        m_Agent = GetComponent<NavMeshAgent>();
        m_Id = gameObject;
    }

    public override void OnStart()
    {
        m_StartTime = Time.time;
        m_NextRecomputeTime = 0f;

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
        if (m_Agent == null || !m_Agent.isOnNavMesh) return TaskStatus.Failure;

        // Hors de danger (toutes grenades confondues) -> terminé
        if (!RedDroneTactics.IsInGrenadeDanger(transform.position, gameObject.tag, dangerRadius.Value, 10f))
            return TaskStatus.Success;

        if (Time.time - m_StartTime > timeout.Value) return TaskStatus.Success;

        if (Time.time >= m_NextRecomputeTime)
        {
            m_NextRecomputeTime = Time.time + recomputeInterval.Value;

            Vector3 escape;
            if (RedDroneTactics.FindEscapePoint(
                    transform, m_Id, gameObject.tag, target.Value, dangerRadius.Value, out escape))
            {
                RedDroneTactics.ReserveSlot(m_Id, escape);
                m_Agent.isStopped = false;
                m_Agent.SetDestination(escape);
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
        dangerRadius = 7f;
        recomputeInterval = 0.25f;
        timeout = 3f;
    }
}
