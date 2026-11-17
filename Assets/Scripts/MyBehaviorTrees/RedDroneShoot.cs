using UnityEngine;
using UnityEngine.AI;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Tir des drones rouges.
///  - Tir CONVERGENT : chaque missile part de son propre point de lancement vers
///    le point de visée anticipé (Drone.ShootAt). Avant, les deux missiles partaient
///    parallèlement à l'axe du drone : avec un écart latéral entre les deux lanceurs
///    proche du rayon du collider cible (0,4 m), un des deux missiles ratait souvent.
///    La correction est limitée à quelques degrés autour de l'axe du drone.
///  - Enregistrement des dégâts en vol auprès d'ArmyManagerRed (anti overkill).
///  - Cadence : jamais plus d'un tir par seconde (Wait Min de l'Annexe A), même si
///    une esquive a interrompu le Wait de fin de boucle.
/// </summary>
[TaskCategory("MyTasks/Red Drone")]
[TaskDescription("RED drone: fires converging, lead-corrected missiles and registers their pending damage.")]
public class RedDroneShoot : Action
{
    public SharedTransform target;

    [BehaviorDesigner.Runtime.Tasks.Tooltip("Intervalle minimum entre deux salves (= Wait Min de l'Annexe A)")]
    public SharedFloat minShotInterval = 1f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Correction angulaire maximale de chaque missile par rapport à l'axe du drone")]
    public SharedFloat maxAimCorrection = 8f;

    Drone m_Drone;
    NavMeshAgent m_Agent;
    IArmyElement m_ArmyElement;
    float m_LastShotTime = -999f;

    public override void OnAwake()
    {
        m_Drone = GetComponent<Drone>();
        m_Agent = GetComponent<NavMeshAgent>();
        m_ArmyElement = (IArmyElement)GetComponent(typeof(IArmyElement));
    }

    public override TaskStatus OnUpdate()
    {
        if (m_Drone == null) return TaskStatus.Failure;
        if (!RedDroneTactics.IsAlive(target.Value)) return TaskStatus.Failure;

        // Respect de la cadence de tir d'origine
        if (Time.time - m_LastShotTime < minShotInterval.Value) return TaskStatus.Running;

        float missileStartSpeed = m_Agent != null ? m_Agent.speed : 10f;
        Vector3 aimPoint = RedDroneTactics.PredictAimPoint(transform.position, missileStartSpeed, target.Value);

        // Enregistrer les dégâts AVANT le tir pour que l'estimation anti-overkill
        // soit à jour pour les prochaines demandes de cible.
        ArmyManagerRed manager = m_ArmyElement != null ? m_ArmyElement.ArmyManager as ArmyManagerRed : null;
        if (manager != null)
            manager.RegisterPendingMissileShot(target.Value.gameObject, m_Drone.MissileCount);

        // Suivi local des dégâts en vol pour le tir concentré (anti overkill).
        // Durée courte : un missile raté libère vite la cible.
        RedDroneTactics.RegisterPendingDamage(target.Value, 5f * m_Drone.MissileCount, 1.5f);

        m_Drone.ShootAt(aimPoint, maxAimCorrection.Value);
        m_LastShotTime = Time.time;

        return TaskStatus.Success;
    }

    public override void OnReset()
    {
        target = null;
        minShotInterval = 1f;
        maxAimCorrection = 8f;
    }
}
