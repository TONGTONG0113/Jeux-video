using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class ArmyManagerRed : ArmyManager
{
    // =========================================================
    // ARTILLERY & MISSILE CONFIGURATION
    // =========================================================

    const float ARTILLERY_DAMAGE = 10f;
    const float PENDING_SHOT_LIFETIME = 4.5f;

    const float MISSILE_DAMAGE = 5f;
    const float MISSILE_LIFETIME = 4.5f;

    [SerializeField]
    float m_DroneFinishOffHealthRatio = 0.5f;

    [SerializeField]
    float m_DroneFinishOffAbsHealth = 5f;


    // =========================================================
    // PENDING DAMAGE
    // =========================================================

    class PendingShot
    {
        public IArmyElement Target;
        public float Damage;
        public float ExpireTime;
    }

    readonly List<PendingShot> m_PendingShots =
        new List<PendingShot>();


    public void RegisterPendingArtilleryShot(
        GameObject target)
    {
        RegisterPendingShot(
            target,
            ARTILLERY_DAMAGE,
            PENDING_SHOT_LIFETIME
        );
    }


    public void RegisterPendingMissileShot(
        GameObject target,
        int missileCount = 2)
    {
        if (target == null)
            return;

        int count =
            Mathf.Max(
                1,
                missileCount
            );

        for (int i = 0; i < count; i++)
        {
            RegisterPendingShot(
                target,
                MISSILE_DAMAGE,
                MISSILE_LIFETIME
            );
        }
    }


    void RegisterPendingShot(
        GameObject target,
        float damage,
        float lifetime)
    {
        if (target == null)
            return;

        IArmyElement element =
            target.GetComponent<IArmyElement>();

        if (element == null)
            return;


        CleanupPendingShots();


        m_PendingShots.Add(
            new PendingShot
            {
                Target = element,
                Damage = damage,
                ExpireTime =
                    Time.time + lifetime
            }
        );
    }


    void CleanupPendingShots()
    {
        m_PendingShots.RemoveAll(
            shot =>
                shot.Target == null ||
                Time.time >= shot.ExpireTime
        );
    }


    float GetPendingDamage(
        IArmyElement target)
    {
        CleanupPendingShots();

        return m_PendingShots
            .Where(
                shot =>
                    shot.Target == target
            )
            .Sum(
                shot =>
                    shot.Damage
            );
    }


    float GetExpectedRemainingHealth(
        IArmyElement target)
    {
        return
            target.Health -
            GetPendingDamage(target);
    }


    // =========================================================
    // UNIFIED TARGET REQUEST
    // =========================================================

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

                return GetBestTargetForRedTurret(
                    null,
                    requesterPos,
                    minRadius,
                    maxRadius
                );


            case RequesterType.Drone:

                return GetBestTargetForRedDrone(
                    requesterPos,
                    minRadius,
                    maxRadius
                );


            case RequesterType.Medic:

                return GetBestHealTarget(
                    requesterPos,
                    minRadius,
                    maxRadius
                );


            default:

                return null;
        }
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


        // ---------------------------------------------------------
        // PRIORITY 1 : ENEMY TURRETS
        //
        // Strategic coordination:
        //
        // RED artillery attacks first the GREEN turrets which are
        // difficult for RED drones to reach with the NavMesh.
        //
        // Easy / flat-area turrets are therefore left to the raider
        // drones whenever possible.
        //
        // This avoids wasting RED drones on turrets located on
        // terraces, map borders or positions requiring large detours.
        // ---------------------------------------------------------

        List<Turret> enemyTurretsInRange =
            GetAllEnemiesOfType<Turret>(false)
            .Where(
                turret =>
                {
                    float distance =
                        Vector3.Distance(
                            turretPosition,
                            turret.transform.position
                        );

                    return
                        distance > minRadius &&
                        distance < maxRadius;
                }
            )
            .ToList();


        // Normally SelectRedTurretTarget passes the actual turret.
        // RequestTarget(Turret) can however call this method without
        // one, so use another RED unit as a safe fallback for the tag.
        string redTag =
            GetRedArmyTag(
                requestingTurret
            );


        var turretCandidates =
            enemyTurretsInRange

            // Anti-overkill:
            // ignore a turret already expected to die from shots
            // which are currently travelling towards it.
            .Where(
                turret =>
                    GetExpectedRemainingHealth(
                        turret
                    ) > 0f
            )

            .Select(
                turret => new
                {
                    Turret = turret,

                    ExpectedHealth =
                        GetExpectedRemainingHealth(
                            turret
                        ),

                    // 0 = easy for drones
                    // 1 = extremely difficult / inaccessible
                    Difficulty =
                        RedDroneTactics
                            .GetTurretAccessDifficulty(
                                turret.transform,
                                redTag
                            )
                }
            )

            .ToList();


        Turret bestEnemyTurret =
            turretCandidates

            // 1. Hardest terrain / NavMesh access first.
            .OrderByDescending(
                candidate =>
                    candidate.Difficulty
            )

            // 2. Among similar targets, finish the weakest turret.
            .ThenBy(
                candidate =>
                    candidate.ExpectedHealth
            )

            // 3. Final tie-breaker: distance from this artillery.
            .ThenBy(
                candidate =>
                    Vector3.Distance(
                        turretPosition,
                        candidate
                            .Turret
                            .transform
                            .position
                    )
            )

            .Select(
                candidate =>
                    candidate.Turret
            )

            .FirstOrDefault();


        if (bestEnemyTurret != null)
        {
            return
                bestEnemyTurret.gameObject;
        }


        // ---------------------------------------------------------
        // PRIORITY 2 : ENEMY DRONES
        // ---------------------------------------------------------

        List<Drone> enemyDronesInRange =
            GetAllEnemiesOfType<Drone>(false)
            .Where(
                drone =>
                {
                    float distance =
                        Vector3.Distance(
                            turretPosition,
                            drone.transform.position
                        );

                    return
                        distance > minRadius &&
                        distance < maxRadius;
                }
            )
            .ToList();


        // ---------------------------------------------------------
        // PRIORITY 2A : SLOW / STATIONARY DRONES
        //
        // Artillery is much more effective against targets whose
        // current NavMesh velocity is low.
        // ---------------------------------------------------------

        List<Drone> slowDrones =
            enemyDronesInRange
            .Where(
                drone =>
                {
                    NavMeshAgent agent =
                        drone.GetComponent<NavMeshAgent>();

                    if (agent == null)
                        return false;

                    return
                        agent.velocity.magnitude <= 1.0f;
                }
            )
            .ToList();


        if (slowDrones.Count > 0)
        {
            Drone nearestSlowDrone =
                slowDrones

                .OrderBy(
                    drone =>
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
        }


        // ---------------------------------------------------------
        // PRIORITY 3 : NEAREST ENEMY DRONE
        // ---------------------------------------------------------

        Drone fallbackDrone =
            enemyDronesInRange

            .OrderBy(
                drone =>
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
    // ARMY TAG HELPER
    // =========================================================

    string GetRedArmyTag(
        GameObject requestingTurret)
    {
        if (requestingTurret != null)
        {
            return
                requestingTurret.tag;
        }


        // Fallback used if the generic RequestTarget interface
        // requests a turret target without passing the requester.
        foreach (IArmyElement element in m_ArmyElements)
        {
            if (element == null)
                continue;

            Component component =
                element as Component;

            if (component != null)
            {
                return
                    component.gameObject.tag;
            }
        }


        // Last-resort fallback.
        return gameObject.tag;
    }


    // =========================================================
    // RED DRONE TARGET SELECTION
    // =========================================================

    GameObject GetBestTargetForRedDrone(
        Vector3 dronePos,
        float minRadius,
        float maxRadius)
    {
        CleanupPendingShots();


        // ---------------------------------------------------------
        // PRIORITY 1 : FINISH WEAKENED ENEMY TURRETS
        // ---------------------------------------------------------

        Turret finishableTurret =
            GetAllEnemiesOfType<Turret>(false)

            .Where(
                turret =>
                {
                    float distance =
                        Vector3.Distance(
                            dronePos,
                            turret.transform.position
                        );

                    return
                        distance > minRadius &&
                        distance < maxRadius;
                }
            )

            .Where(
                turret =>
                {
                    float expected =
                        GetExpectedRemainingHealth(
                            turret
                        );

                    return
                        expected > 0f &&
                        (
                            expected <=
                            turret.Health *
                            m_DroneFinishOffHealthRatio
                            ||
                            expected <=
                            m_DroneFinishOffAbsHealth
                        );
                }
            )

            .OrderBy(
                turret =>
                    GetExpectedRemainingHealth(
                        turret
                    )
            )

            .ThenBy(
                turret =>
                    Vector3.Distance(
                        dronePos,
                        turret.transform.position
                    )
            )

            .FirstOrDefault();


        if (finishableTurret != null)
        {
            return
                finishableTurret.gameObject;
        }


        // ---------------------------------------------------------
        // PRIORITY 2 : NEAREST ENEMY
        //
        // Ruofan's RedDroneTactics performs the more advanced
        // focus-fire / raider / anti-clustering decisions.
        // ---------------------------------------------------------

        var enemyDrones =
            GetAllEnemiesOfType<ArmyElement>(false)

            .Where(
                enemy =>
                    enemy.Health > 0 &&
                    !enemy.CompareTag(
                        gameObject.tag
                    )
            )

            .OrderBy(
                enemy =>
                    Vector3.Distance(
                        dronePos,
                        enemy.transform.position
                    )
            )

            .FirstOrDefault();


        if (enemyDrones != null)
        {
            return
                enemyDrones.gameObject;
        }


        return null;
    }


    // =========================================================
    // MEDIC TARGET SELECTION
    // =========================================================

    GameObject GetBestHealTarget(
        Vector3 medicPos,
        float minRadius,
        float maxRadius)
    {
        IArmyElement best = null;

        float bestHealth =
            float.MaxValue;


        foreach (
            IArmyElement ally
            in m_ArmyElements)
        {
            if (ally == null)
                continue;

            if (ally.Health <= 0f)
                continue;


            Component allyComponent =
                ally as Component;


            if (allyComponent == null)
                continue;


            if (
                allyComponent.gameObject ==
                gameObject
            )
            {
                continue;
            }


            Health healthComp =
                allyComponent
                    .GetComponent<Health>();


            if (
                healthComp != null &&
                ally.Health >=
                healthComp.MaxValue
            )
            {
                continue;
            }


            float distance =
                Vector3.Distance(
                    medicPos,
                    allyComponent
                        .transform
                        .position
                );


            if (
                distance <= minRadius ||
                distance >= maxRadius
            )
            {
                continue;
            }


            if (ally.Health < bestHealth)
            {
                bestHealth =
                    ally.Health;

                best =
                    ally;
            }
        }


        if (best == null)
            return null;


        Component bestComponent =
            best as Component;


        return
            bestComponent != null
                ? bestComponent.gameObject
                : null;
    }


    // =========================================================
    // HELPER : GET ALLIES
    // =========================================================

    public List<T> GetAllAlliesOfType<T>(
        bool includeDead = false)
        where T : Component
    {
        List<T> allies =
            new List<T>();


        foreach (
            var element
            in m_ArmyElements)
        {
            if (element == null)
                continue;


            if (
                !includeDead &&
                element.Health <= 0f
            )
            {
                continue;
            }


            if (element is T comp)
            {
                allies.Add(comp);
            }
        }


        return allies;
    }


    // =========================================================
    // BATTLE END
    // =========================================================

    public override void ArmyElementHasBeenKilled(
        GameObject go)
    {
        base.ArmyElementHasBeenKilled(
            go
        );


        if (m_ArmyElements.Count == 0)
        {
            GUIUtility.systemCopyBuffer =
                "0\t" +
                ((int)Timer.Value).ToString() +
                "\t0\t0\t0";


            BattleStatsManager.Instance?
                .PrintBattleResult(
                    "2"
                );
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
            ((int)Timer.Value).ToString() +
            "\t" +
            nDrones.ToString() +
            "\t" +
            nTurrets.ToString() +
            "\t" +
            health.ToString();


        RefreshHudDisplay();


        BattleStatsManager.Instance?
            .PrintBattleResult(
                "1"
            );
    }
}