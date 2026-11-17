using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Remplace "My Seek" pour les drones rouges.
///
/// Au lieu de foncer tous sur la position de la cible (ce qui regroupait les drones
/// rouges et en faisait des proies pour les grenades vertes), chaque drone choisit
/// une POSITION DE TIR autour de la cible :
///  - à distance de tir (≈13 m d'un drone ; 4 m d'une tourelle = dans son angle mort,
///    les tourelles vertes ont un Min Radius de 5 m),
///  - espacée des autres drones rouges (réservation des destinations),
///  - avec une ligne de tir dégagée,
///  - hors des zones d'impact de grenades prévues.
///
/// Anti-inactivité : recalcul de chemin quand la cible bouge, chemin partiel/invalide
/// détecté, détection de blocage, et timeout. La tâche ne peut donc plus rester
/// indéfiniment en Running (cause des drones immobiles observés).
///
/// Vitesse 10 m/s et vitesse angulaire 180 °/s : valeurs de l'Annexe A conservées.
/// </summary>
[TaskCategory("MyTasks/Red Drone")]
[TaskDescription("RED drone: moves to a spaced firing slot around the target, with line of fire and anti-stuck logic.")]
public class RedDroneEngage : Action
{
    public SharedTransform target;

    [BehaviorDesigner.Runtime.Tasks.Tooltip("Annexe A : vitesse du drone (non modifiée)")]
    public SharedFloat speed = 10f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Annexe A : vitesse angulaire du drone (non modifiée)")]
    public SharedFloat angularSpeed = 180f;

    [BehaviorDesigner.Runtime.Tasks.Tooltip("Distance de tir visée contre un drone")]
    public SharedFloat preferredRangeVsDrone = 13f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Distance de tir visée contre une tourelle (< Min Radius 5 m des tourelles)")]
    public SharedFloat preferredRangeVsTurret = 4f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Contre un drone : distance jusqu'à laquelle on tire tout de suite sans se repositionner")]
    public SharedFloat directFireRange = 20f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Un allié plus proche que cette distance oblige à se décaler")]
    public SharedFloat minAllyDistance = 3.5f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Au-delà, on ne tire pas même en cas de timeout")]
    public SharedFloat maxFireRange = 25f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Tolérance d'arrivée sur la position de tir")]
    public SharedFloat slotTolerance = 1.5f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Espacement souhaité entre drones rouges (rayon grenade = 5 m)")]
    public SharedFloat allySpacing = 6f;
    public SharedFloat repathInterval = 0.3f;
    public SharedFloat stuckDelay = 1.2f;
    public SharedFloat timeout = 6f;

    NavMeshAgent m_Agent;
    GameObject m_Id;

    Vector3 m_Slot;
    Vector3 m_SlotTargetPos;
    bool m_HasSlot;
    bool m_ForceNewSlot;

    float m_StartTime;
    float m_NextRepathTime;
    float m_LastProgressTime;
    Vector3 m_LastPosition;
    int m_StuckCount;

    readonly List<Vector3> m_RejectedSlots = new List<Vector3>();

    public override void OnAwake()
    {
        m_Agent = GetComponent<NavMeshAgent>();
        m_Id = gameObject;
    }

    public override void OnStart()
    {
        m_StartTime = Time.time;
        m_NextRepathTime = 0f;
        m_LastProgressTime = Time.time;
        m_LastPosition = transform.position;
        m_StuckCount = 0;
        m_HasSlot = false;
        m_ForceNewSlot = false;
        m_RejectedSlots.Clear();

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
        if (!RedDroneTactics.IsAlive(target.Value)) return TaskStatus.Failure;

        Transform t = target.Value;
        Vector3 myPos = transform.position;
        float distToTarget = RedDroneTactics.FlatDistance(myPos, t.position);
        float preferredRange = (t.GetComponent<Turret>() != null)
            ? preferredRangeVsTurret.Value
            : preferredRangeVsDrone.Value;

        // ---------------------------------------------------------
        // 1. Déjà en bonne position ? -> on passe à la visée
        // ---------------------------------------------------------
        bool isTurret = t.GetComponent<Turret>() != null;
        bool lineOfFire = RedDroneTactics.HasLineOfFire(myPos, t, gameObject.tag);
        bool crowded = RedDroneTactics.AllyCrowdPenalty(myPos, m_Id, gameObject.tag, minAllyDistance.Value) > 0f;

        // Contre un drone : on tire dès qu'on est à portée (cadence de tir maximale).
        // Contre une tourelle : on va d'abord dans son angle mort (< 5 m).
        float fireNowRange = isTurret ? preferredRange + slotTolerance.Value : directFireRange.Value;
        if (lineOfFire && !crowded && distToTarget <= fireNowRange)
            return TaskStatus.Success;

        if (m_HasSlot && lineOfFire && distToTarget <= maxFireRange.Value &&
            RedDroneTactics.FlatDistance(myPos, m_Slot) <= slotTolerance.Value)
            return TaskStatus.Success;

        // ---------------------------------------------------------
        // 2. Timeout : on ne reste jamais bloqué dans cette tâche
        // ---------------------------------------------------------
        if (Time.time - m_StartTime > timeout.Value)
            return distToTarget <= maxFireRange.Value ? TaskStatus.Success : TaskStatus.Failure;

        // ---------------------------------------------------------
        // 3. Détection de blocage
        // ---------------------------------------------------------
        if (RedDroneTactics.FlatDistance(myPos, m_LastPosition) > 0.5f)
        {
            m_LastPosition = myPos;
            m_LastProgressTime = Time.time;
        }
        else if (Time.time - m_LastProgressTime > stuckDelay.Value)
        {
            m_LastProgressTime = Time.time;
            m_StuckCount++;
            if (m_HasSlot) m_RejectedSlots.Add(m_Slot);
            m_ForceNewSlot = true;
            m_NextRepathTime = 0f;

            if (m_StuckCount >= 3)
                return distToTarget <= maxFireRange.Value ? TaskStatus.Success : TaskStatus.Failure;
        }

        // ---------------------------------------------------------
        // 4. (Re)calcul de la position de tir
        // ---------------------------------------------------------
        if (Time.time >= m_NextRepathTime)
        {
            m_NextRepathTime = Time.time + repathInterval.Value;

            bool targetMoved = m_HasSlot && RedDroneTactics.FlatDistance(t.position, m_SlotTargetPos) > 2f;
            bool badPath = !m_Agent.pathPending && m_HasSlot &&
                           m_Agent.pathStatus != NavMeshPathStatus.PathComplete;

            if (badPath) m_RejectedSlots.Add(m_Slot);

            if (!m_HasSlot || targetMoved || badPath || m_ForceNewSlot)
            {
                m_ForceNewSlot = false;
                Vector3 slot;

                if (RedDroneTactics.FindFiringSlot(
                        transform, m_Id, gameObject.tag, t,
                        preferredRange, allySpacing.Value, m_Agent.baseOffset,
                        m_RejectedSlots, out slot))
                {
                    m_Slot = slot;
                    m_SlotTargetPos = t.position;
                    m_HasSlot = true;
                    RedDroneTactics.ReserveSlot(m_Id, slot);
                    m_Agent.isStopped = false;
                    m_Agent.SetDestination(slot);
                }
                else
                {
                    // Aucun emplacement valide : comportement par défaut (aller vers la cible)
                    m_HasSlot = false;
                    m_Agent.isStopped = false;
                    m_Agent.SetDestination(t.position);
                }
            }
        }

        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        if (m_Agent != null && m_Agent.isOnNavMesh && m_Agent.hasPath)
            m_Agent.isStopped = true;
    }

    public override void OnBehaviorComplete()
    {
        RedDroneTactics.ReleaseSlot(m_Id);
    }

    public override void OnReset()
    {
        target = null;
        speed = 10f;
        angularSpeed = 180f;
        preferredRangeVsDrone = 13f;
        preferredRangeVsTurret = 4f;
        maxFireRange = 25f;
        directFireRange = 20f;
        minAllyDistance = 3.5f;
        slotTolerance = 1.5f;
        allySpacing = 6f;
        repathInterval = 0.3f;
        stuckDelay = 1.2f;
        timeout = 6f;
    }
}
