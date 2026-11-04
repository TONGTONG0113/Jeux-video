using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class ArmyManagerRed : ArmyManager
{
    // =========================================================
    // ARTILLERY CONFIGURATION
    // =========================================================

    const float ARTILLERY_DAMAGE = 10f;
    const float PENDING_SHOT_LIFETIME = 4.5f;

    // [ADDED] Missile constants for RED drone shots.
    // Damage per missile = 5 (Annex A). A drone fires 2 missiles,
    // so RegisterPendingMissileShot registers one entry per missile.
    const float MISSILE_DAMAGE = 5f;
    const float MISSILE_LIFETIME = 4.5f;


    [SerializeField]
    float m_SlowDroneSpeedThreshold = 1.0f;

    [SerializeField]
    float m_DroneClusterRadius = 5f;

    [SerializeField]
    int m_MinDroneClusterSize = 2;

    // [ADDED] Drone targeting: only finish off turrets that are
    // already weakened (or already covered by our artillery).
    [SerializeField]
    float m_DroneFinishOffHealthRatio = 0.5f;

    [SerializeField]
    float m_DroneFinishOffAbsHealth = 5f;


    // =========================================================
    // PENDING DAMAGE (artillery + missiles)
    // =========================================================

    // [CHANGED] Was: class PendingArtilleryShot { Turret Target; float ExpireTime; }
    // Now generic: target can be Turret or Drone, and damage is stored
    // so that artillery (10) and missiles (5 each) coexist in the same list.
    class PendingShot
    {
        public IArmyElement Target;
        public float Damage;
        public float ExpireTime;
    }

    readonly List<PendingShot> m_PendingShots =
        new List<PendingShot>();


    // Called exactly when a RED artillery piece fires.
    // [KEPT] Signature unchanged so RedTurretShoot.cs keeps working.
    public void RegisterPendingArtilleryShot(GameObject target)
    {
        RegisterPendingShot(target, ARTILLERY_DAMAGE, PENDING_SHOT_LIFETIME);
    }


    // [ADDED] Called by RED drones when they fire.
    // A drone fires m_MissileSpawnPos.Length missiles (usually 2).
    // Ruofan: call this with missileCount = m_MissileSpawnPos.Length.
    public void RegisterPendingMissileShot(GameObject target, int missileCount = 2)
    {
        if (target == null)
        {
            return;
        }

        int count = Mathf.Max(1, missileCount);

        for (int i = 0; i < count; i++)
        {
            RegisterPendingShot(target, MISSILE_DAMAGE, MISSILE_LIFETIME);
        }
    }


    // [ADDED] Shared implementation for both artillery and missiles.
    void RegisterPendingShot(GameObject target, float damage, float lifetime)
    {
        if (target == null)
        {
            return;
        }

        IArmyElement element = target.GetComponent<IArmyElement>();

        if (element == null)
        {
            return;
        }

        CleanupPendingShots();

        m_PendingShots.Add(new PendingShot
        {
            Target = element,
            Damage = damage,
            ExpireTime = Time.time + lifetime
        });
    }


    void CleanupPendingShots()
    {
        m_PendingShots.RemoveAll(
            shot =>
                shot.Target == null ||
                Time.time >= shot.ExpireTime
        );
    }


    // [CHANGED] Parameter type: Turret -> IArmyElement.
    // [CHANGED] Sum now uses each shot's own Damage instead of * ARTILLERY_DAMAGE.
    float GetPendingDamage(IArmyElement target)
    {
        CleanupPendingShots();

        return m_PendingShots
            .Where(shot => shot.Target == target)
            .Sum(shot => shot.Damage);
    }


    // [CHANGED] Parameter type: Turret -> IArmyElement.
    float GetExpectedRemainingHealth(IArmyElement target)
    {
        return target.Health -
               GetPendingDamage(target);
    }


    // =========================================================
    // UNIFIED TARGET REQUEST (entry point for ALL red units)
    // =========================================================

    // [ADDED] One entry point for drones, turrets and (later) medics.
    // Keeps the turret logic untouched: it simply forwards to
    // GetBestTargetForRedTurret with the existing behaviour.
    public enum RequesterType
    {
        Drone,
        Turret,
        Medic
    }

    public GameObject RequestTarget(
        RequesterType requesterType,
        Vector3 requesterPos,
        float minRadius,
        float maxRadius)
    {
        switch (requesterType)
        {
            case RequesterType.Turret:
                // Forward to the existing, battle-tested method.
                // requestingTurret is unused in the current implementation.
                return GetBestTargetForRedTurret(
                    null, requesterPos, minRadius, maxRadius);

            case RequesterType.Drone:
                return GetBestTargetForRedDrone(
                    requesterPos, minRadius, maxRadius);

            case RequesterType.Medic:
                return GetBestHealTarget(
                    requesterPos, minRadius, maxRadius);

            default:
                return null;
        }
    }


    // =========================================================
    // RED ARTILLERY TARGET SELECTION (unchanged logic)
    // =========================================================

    public GameObject GetBestTargetForRedTurret(
        GameObject requestingTurret,
        Vector3 turretPosition,
        float minRadius,
        float maxRadius)
    {
        CleanupPendingShots();


        // =====================================================
        // PRIORITY 1: ENEMY ARTILLERY
        // =====================================================

        List<Turret> enemyTurretsInRange =
            GetAllEnemiesOfType<Turret>(false)
            .Where(turret =>
            {
                float distance =
                    Vector3.Distance(
                        turretPosition,
                        turret.transform.position
                    );

                return distance > minRadius &&
                       distance < maxRadius;
            })
            .ToList();


        /*
         * Important:
         *
         * We don't look only at current Health.
         *
         * Example:
         *
         * Turret HP = 20
         * 2 grenades already flying to it
         *
         * Expected HP =
         * 20 - 2 * 10 = 0
         *
         * The turret is therefore considered
         * "already committed to destruction".
         */
        Turret bestEnemyTurret =
            enemyTurretsInRange
            .Where(turret =>
                GetExpectedRemainingHealth(
                    turret
                ) > 0f
            )
            .OrderBy(turret =>
                GetExpectedRemainingHealth(
                    turret
                )
            )
            .ThenBy(turret =>
                Vector3.Distance(
                    turretPosition,
                    turret.transform.position
                )
            )
            .FirstOrDefault();


        if (bestEnemyTurret != null)
        {
            return bestEnemyTurret.gameObject;
        }


        // =====================================================
        // PRIORITY 2:
        // ALL REACHABLE TURRETS ALREADY HAVE ENOUGH
        // INCOMING DAMAGE.
        //
        // Use artillery against good drone targets instead.
        // =====================================================

        List<Drone> enemyDronesInRange =
            GetAllEnemiesOfType<Drone>(false)
            .Where(drone =>
            {
                float distance =
                    Vector3.Distance(
                        turretPosition,
                        drone.transform.position
                    );

                return distance > minRadius &&
                       distance < maxRadius;
            })
            .ToList();


        // =====================================================
        // PRIORITY 2A: SLOW / STATIONARY DRONES
        // =====================================================

        List<Drone> slowDrones =
            enemyDronesInRange
            .Where(drone =>
            {
                NavMeshAgent agent =
                    drone.GetComponent<NavMeshAgent>();

                if (agent == null)
                {
                    return false;
                }

                return
                    agent.velocity.magnitude
                    <= m_SlowDroneSpeedThreshold;
            })
            .ToList();


        // =====================================================
        // PRIORITY 2B: DENSE SLOW-DRONE CLUSTER
        // =====================================================

        if (slowDrones.Count >= m_MinDroneClusterSize)
        {
            Drone bestClusterCenter = null;
            int bestClusterSize = 0;

            foreach (Drone candidate in slowDrones)
            {
                int clusterSize =
                    slowDrones.Count(
                        other =>
                            Vector3.Distance(
                                candidate.transform.position,
                                other.transform.position
                            )
                            <= m_DroneClusterRadius
                    );

                if (clusterSize > bestClusterSize)
                {
                    bestClusterSize = clusterSize;
                    bestClusterCenter = candidate;
                }
            }

            if (bestClusterCenter != null &&
                bestClusterSize >= m_MinDroneClusterSize)
            {
                return bestClusterCenter.gameObject;
            }
        }


        // =====================================================
        // PRIORITY 2C: SINGLE SLOW DRONE
        // =====================================================

        Drone nearestSlowDrone =
            slowDrones
            .OrderBy(drone =>
                Vector3.Distance(
                    turretPosition,
                    drone.transform.position
                )
            )
            .FirstOrDefault();

        if (nearestSlowDrone != null)
        {
            return nearestSlowDrone.gameObject;
        }


        // =====================================================
        // PRIORITY 3: NO GOOD STATIC TARGET. ATTACK NEAREST DRONE.
        // =====================================================

        Drone fallbackDrone =
            enemyDronesInRange
            .OrderBy(drone =>
                Vector3.Distance(
                    turretPosition,
                    drone.transform.position
                )
            )
            .FirstOrDefault();

        if (fallbackDrone != null)
        {
            return fallbackDrone.gameObject;
        }

        return null;
    }


    // =========================================================
    // RED DRONE TARGET SELECTION (new)
    // =========================================================

    // [ADDED] Strategy: drones act as "finishers".
    // They only engage enemy turrets that are already weakened
    // (<= m_DroneFinishOffHealthRatio of max HP) or nearly dead
    // in absolute terms (<= m_DroneFinishOffAbsHealth).
    // If no such turret exists, they engage the nearest enemy drone.
    GameObject GetBestTargetForRedDrone(
        Vector3 dronePos, float minRadius, float maxRadius)
    {
        CleanupPendingShots();

        // ---- 1. Finish off weakened enemy turrets ----
        Turret finishableTurret =
            GetAllEnemiesOfType<Turret>(false)
            .Where(t =>
            {
                float d = Vector3.Distance(
                    dronePos, t.transform.position);
                return d > minRadius && d < maxRadius;
            })
            .Where(t =>
            {
                float expected = GetExpectedRemainingHealth(t);
                return expected > 0f &&
                       (expected <= t.Health * m_DroneFinishOffHealthRatio
                        || expected <= m_DroneFinishOffAbsHealth);
            })
            .OrderBy(t => GetExpectedRemainingHealth(t))
            .ThenBy(t =>
                Vector3.Distance(dronePos, t.transform.position))
            .FirstOrDefault();

        if (finishableTurret != null)
        {
            return finishableTurret.gameObject;
        }

        // ---- 2. Otherwise: nearest enemy drone ----
        Drone nearestDrone =
            GetAllEnemiesOfType<Drone>(false)
            .Where(d =>
            {
                float dist = Vector3.Distance(
                    dronePos, d.transform.position);
                return dist > minRadius && dist < maxRadius;
            })
            .Where(d => GetExpectedRemainingHealth(d) > 0f)
            .OrderBy(d => Vector3.Distance(
                dronePos, d.transform.position))
            .FirstOrDefault();

        if (nearestDrone != null)
        {
            return nearestDrone.gameObject;
        }

        return null;
    }


    // =========================================================
    // MEDIC TARGET SELECTION (placeholder)
    // =========================================================

    // [ADDED] Placeholder for the MedicDrone feature.
    // Picks the most wounded living ally within [minRadius, maxRadius].
    // Uses m_ArmyElements (protected List<IArmyElement> from ArmyManager).
    GameObject GetBestHealTarget(
        Vector3 medicPos, float minRadius, float maxRadius)
    {
        IArmyElement best = null;
        float bestHealth = float.MaxValue;

        foreach (IArmyElement ally in m_ArmyElements)
        {
            if (ally == null)
            {
                continue;
            }

            if (ally.Health <= 0f)
            {
                continue;
            }

            Component allyComponent = ally as Component;

            if (allyComponent == null)
            {
                continue;
            }

            // Skip the medic itself.
            if (allyComponent.gameObject == gameObject)
            {
                continue;
            }

            float d = Vector3.Distance(
                medicPos, allyComponent.transform.position);

            if (d <= minRadius || d >= maxRadius)
            {
                continue;
            }

            // We don't know max HP from IArmyElement, so use absolute HP.
            // Lower HP = higher priority.
            if (ally.Health < bestHealth)
            {
                bestHealth = ally.Health;
                best = ally;
            }
        }

        if (best == null)
        {
            return null;
        }

        Component bestComponent = best as Component;
        return bestComponent != null ? bestComponent.gameObject : null;
    }


    // =========================================================
    // ORIGINAL PROJECT CODE (unchanged)
    // =========================================================

    public override void ArmyElementHasBeenKilled(GameObject go)
    {
        base.ArmyElementHasBeenKilled(go);

        if (m_ArmyElements.Count == 0)
        {
            GUIUtility.systemCopyBuffer =
                "0\t" +
                ((int)Timer.Value)
                    .ToString() +
                "\t0\t0\t0";
        }
    }


    public void GreenArmyIsDead(string deadArmyTag)
    {
        int nDrones = 0;
        int nTurrets = 0;
        int health = 0;

        ComputeStatistics(
            ref nDrones,
            ref nTurrets,
            ref health
        );

        GUIUtility.systemCopyBuffer =
            "1\t" +
            ((int)Timer.Value)
                .ToString() +
            "\t" +
            nDrones.ToString() +
            "\t" +
            nTurrets.ToString() +
            "\t" +
            health.ToString();

        RefreshHudDisplay();
    }
}