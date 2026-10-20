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

    // Original grenade damage in the project.
    // This does NOT change the real damage.
    // It is only used to estimate incoming damage.
    const float ARTILLERY_DAMAGE = 10f;

    // The real projectile travel time is 4 seconds.
    // We keep the reservation slightly longer so that
    // the real damage has enough time to be applied.
    const float PENDING_SHOT_LIFETIME = 4.5f;


    [SerializeField]
    float m_SlowDroneSpeedThreshold = 1.0f;

    [SerializeField]
    float m_DroneClusterRadius = 5f;

    [SerializeField]
    int m_MinDroneClusterSize = 2;


    // =========================================================
    // PENDING ARTILLERY DAMAGE
    // =========================================================

    class PendingArtilleryShot
    {
        public Turret Target;
        public float ExpireTime;
    }


    readonly List<PendingArtilleryShot>
        m_PendingArtilleryShots =
            new List<PendingArtilleryShot>();


    // Called exactly when a RED artillery piece fires.
    public void RegisterPendingArtilleryShot(
        GameObject target)
    {
        if (target == null)
        {
            return;
        }


        Turret targetTurret =
            target.GetComponent<Turret>();


        // We only reserve damage against enemy turrets.
        // Drone shots are not tracked here.
        if (targetTurret == null)
        {
            return;
        }


        CleanupPendingShots();


        PendingArtilleryShot shot =
            new PendingArtilleryShot();


        shot.Target =
            targetTurret;

        shot.ExpireTime =
            Time.time +
            PENDING_SHOT_LIFETIME;


        m_PendingArtilleryShots.Add(
            shot
        );
    }


    void CleanupPendingShots()
    {
        m_PendingArtilleryShots.RemoveAll(
            shot =>
                shot.Target == null ||
                Time.time >= shot.ExpireTime
        );
    }


    float GetPendingDamage(
        Turret target)
    {
        CleanupPendingShots();


        int incomingShots =
            m_PendingArtilleryShots.Count(
                shot =>
                    shot.Target == target
            );


        return incomingShots *
               ARTILLERY_DAMAGE;
    }


    float GetExpectedRemainingHealth(
        Turret target)
    {
        return target.Health -
               GetPendingDamage(target);
    }


    // =========================================================
    // RED ARTILLERY TARGET SELECTION
    // =========================================================

    public GameObject GetBestTargetForRedTurret(
        GameObject requestingTurret,
        Vector3 turretPosition,
        float minRadius,
        float maxRadius)
    {
        CleanupPendingShots();


        // =====================================================
        // PRIORITY 1:
        // ENEMY ARTILLERY
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
        // PRIORITY 2A:
        // SLOW / STATIONARY DRONES
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
        // PRIORITY 2B:
        // DENSE SLOW-DRONE CLUSTER
        // =====================================================

        if (slowDrones.Count >=
            m_MinDroneClusterSize)
        {
            Drone bestClusterCenter =
                null;

            int bestClusterSize =
                0;


            foreach (
                Drone candidate
                in slowDrones
            )
            {
                int clusterSize =
                    slowDrones.Count(
                        other =>
                            Vector3.Distance(
                                candidate
                                    .transform
                                    .position,

                                other
                                    .transform
                                    .position
                            )
                            <= m_DroneClusterRadius
                    );


                if (clusterSize >
                    bestClusterSize)
                {
                    bestClusterSize =
                        clusterSize;

                    bestClusterCenter =
                        candidate;
                }
            }


            if (
                bestClusterCenter != null &&
                bestClusterSize >=
                m_MinDroneClusterSize
            )
            {
                return
                    bestClusterCenter
                        .gameObject;
            }
        }


        // =====================================================
        // PRIORITY 2C:
        // SINGLE SLOW DRONE
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
            return
                nearestSlowDrone.gameObject;
        }


        // =====================================================
        // PRIORITY 3:
        // NO GOOD STATIC TARGET.
        // ATTACK NEAREST DRONE.
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
            return
                fallbackDrone.gameObject;
        }


        return null;
    }


    // =========================================================
    // ORIGINAL PROJECT CODE
    // =========================================================

    public override void ArmyElementHasBeenKilled(
        GameObject go)
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


    public void GreenArmyIsDead(
        string deadArmyTag)
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