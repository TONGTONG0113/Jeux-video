using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Outils tactiques partagés par les tâches Behavior Designer des drones ROUGES.
/// (Ruofan - DroneRedBehavior)
///
/// - Tir anticipé (lead)
/// - Ligne de tir
/// - Menaces d'artillerie
/// - Positions de tir espacées
/// - Analyse d'accessibilité NavMesh des tourelles
///
/// Aucune caractéristique de l'Annexe A n'est modifiée.
/// </summary>
public static class RedDroneTactics
{
    // =========================================================
    // ANNEXE A (lecture seule)
    // =========================================================

    public const float MISSILE_ACCELERATION = 40f;
    public const float MISSILE_MAX_SPEED = 20f;
    public const float MISSILE_LIFE_DURATION = 3f;
    public const float GRENADE_DAMAGE_RADIUS = 5f;
    public const float DRONE_COLLIDER_RADIUS = 0.4f;

    const float DRONE_PIVOT_HEIGHT = 1f;

    // Limite actuelle du lead.
    const float MAX_LEAD_DISTANCE = 6f;


    // =========================================================
    // OUTILS GÉNÉRAUX
    // =========================================================

    public static Vector3 Flat(
        Vector3 v)
    {
        return
            new Vector3(
                v.x,
                0f,
                v.z
            );
    }


    public static float FlatDistance(
        Vector3 a,
        Vector3 b)
    {
        float dx =
            a.x - b.x;

        float dz =
            a.z - b.z;


        return
            Mathf.Sqrt(
                dx * dx +
                dz * dz
            );
    }


    public static bool IsAlive(
        Transform t)
    {
        if (t == null)
            return false;


        IArmyElement element =
            t.GetComponent<IArmyElement>();


        return
            element != null &&
            element.Health > 0f;
    }


    /// <summary>
    /// Centre du collider de la cible.
    /// </summary>
    public static Vector3 GetAimCenter(
        Transform target)
    {
        Collider col =
            target.GetComponent<Collider>();


        if (col == null)
        {
            col =
                target.GetComponentInChildren<Collider>();
        }


        return
            col != null
                ? col.bounds.center
                : target.position;
    }


    // =========================================================
    // CACHE DES UNITÉS
    // =========================================================

    static int s_UnitsFrame = -1;

    static readonly List<ArmyElement>
        s_AllUnits =
            new List<ArmyElement>();


    static List<ArmyElement> AllUnits()
    {
        if (s_UnitsFrame != Time.frameCount)
        {
            s_UnitsFrame =
                Time.frameCount;


            s_AllUnits.Clear();


            ArmyElement[] units =
                Object.FindObjectsByType<ArmyElement>(
                    FindObjectsSortMode.None
                );


            foreach (ArmyElement unit in units)
            {
                if (
                    unit != null &&
                    unit.Health > 0f
                )
                {
                    s_AllUnits.Add(
                        unit
                    );
                }
            }
        }


        return
            s_AllUnits;
    }


    public static bool IsNearAllyTurret(
        Vector3 pos,
        string myTag,
        float radius)
    {
        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (
                unit is Turret &&
                unit.CompareTag(myTag) &&
                FlatDistance(
                    pos,
                    unit.transform.position
                ) <= radius
            )
            {
                return true;
            }
        }


        return false;
    }


    public static int CountEnemyDrones(
        string myTag)
    {
        int count = 0;


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (
                unit is Drone &&
                !unit.CompareTag(myTag)
            )
            {
                count++;
            }
        }


        return count;
    }


    public static GameObject FindNearestEnemy(
        Vector3 pos,
        string myTag)
    {
        ArmyElement best = null;

        float bestDist =
            float.MaxValue;


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (unit.CompareTag(myTag))
                continue;


            float distance =
                FlatDistance(
                    pos,
                    unit.transform.position
                );


            if (distance < bestDist)
            {
                bestDist =
                    distance;

                best =
                    unit;
            }
        }


        return
            best != null
                ? best.gameObject
                : null;
    }


    // =========================================================
    // TIR CONCENTRÉ / ANTI-OVERKILL
    // =========================================================

    class PendingMissile
    {
        public Transform Target;
        public float Damage;
        public float ExpireTime;
    }


    static readonly Dictionary<GameObject, Transform>
        s_DroneTargets =
            new Dictionary<GameObject, Transform>();


    static readonly List<PendingMissile>
        s_PendingMissiles =
            new List<PendingMissile>();


    static readonly List<GameObject>
        s_DeadKeys =
            new List<GameObject>();


    public static void SetDroneTarget(
        GameObject drone,
        Transform target)
    {
        if (drone == null)
            return;


        if (target == null)
        {
            s_DroneTargets.Remove(
                drone
            );
        }
        else
        {
            s_DroneTargets[drone] =
                target;
        }
    }


    public static int CountAttackers(
        Transform target,
        GameObject except)
    {
        int count = 0;


        s_DeadKeys.Clear();


        foreach (
            KeyValuePair<GameObject, Transform> kv
            in s_DroneTargets)
        {
            if (kv.Key == null)
            {
                s_DeadKeys.Add(
                    kv.Key
                );

                continue;
            }


            if (kv.Key == except)
                continue;


            if (kv.Value == target)
                count++;
        }


        foreach (
            GameObject key
            in s_DeadKeys)
        {
            s_DroneTargets.Remove(
                key
            );
        }


        return count;
    }


    public static void RegisterPendingDamage(
        Transform target,
        float damage,
        float lifetime)
    {
        if (target == null)
            return;


        s_PendingMissiles.Add(
            new PendingMissile
            {
                Target = target,
                Damage = damage,
                ExpireTime =
                    Time.time + lifetime
            }
        );
    }


    public static float GetPendingDamage(
        Transform target)
    {
        s_PendingMissiles.RemoveAll(
            pending =>
                pending.Target == null ||
                Time.time >=
                pending.ExpireTime
        );


        float sum = 0f;


        foreach (
            PendingMissile pending
            in s_PendingMissiles)
        {
            if (pending.Target == target)
            {
                sum +=
                    pending.Damage;
            }
        }


        return sum;
    }


    public static float GetExpectedHealth(
        Transform target)
    {
        IArmyElement element =
            target != null
                ? target.GetComponent<IArmyElement>()
                : null;


        if (element == null)
            return 0f;


        return
            element.Health -
            GetPendingDamage(
                target
            );
    }


    // =========================================================
    // FOCUS FIRE
    // =========================================================

    public static Transform SelectFocusTarget(
        Transform self,
        string myTag,
        int maxAttackersBonus)
    {
        Vector3 myPos =
            self.position;


        Transform bestDrone = null;

        float bestScore =
            float.MinValue;


        Transform bestTurret = null;

        float bestTurretScore =
            float.MinValue;


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (unit.CompareTag(myTag))
                continue;


            Transform target =
                unit.transform;


            float expected =
                GetExpectedHealth(
                    target
                );


            if (expected <= 0f)
                continue;


            float distance =
                FlatDistance(
                    myPos,
                    target.position
                );


            if (unit is Drone)
            {
                int attackers =
                    System.Math.Min(
                        CountAttackers(
                            target,
                            self.gameObject
                        ),
                        maxAttackersBonus
                    );


                float score =
                    -distance
                    +
                    15f * attackers
                    +
                    0.8f *
                    (50f - expected);


                // Protéger notre artillerie.
                if (
                    IsNearAllyTurret(
                        target.position,
                        myTag,
                        12f
                    )
                )
                {
                    score += 20f;
                }


                // Une cible lente est plus facile à toucher.
                if (
                    GetTargetVelocity(
                        target
                    ).sqrMagnitude < 1f
                )
                {
                    score += 8f;
                }


                if (score > bestScore)
                {
                    bestScore =
                        score;

                    bestDrone =
                        target;
                }
            }
            else if (unit is Turret)
            {
                float score =
                    -distance
                    -
                    0.5f * expected
                    +
                    10f *
                    System.Math.Min(
                        CountAttackers(
                            target,
                            self.gameObject
                        ),
                        maxAttackersBonus
                    );


                if (score > bestTurretScore)
                {
                    bestTurretScore =
                        score;

                    bestTurret =
                        target;
                }
            }
        }


        return
            bestDrone != null
                ? bestDrone
                : bestTurret;
    }


    // =========================================================
    // RAIDERS : SÉLECTION DES TOURELLES ACCESSIBLES
    // =========================================================

    /// <summary>
    /// Un raider ne choisit désormais que les tourelles qu'il peut
    /// réellement atteindre via un chemin NavMesh complet.
    ///
    /// Les tourelles faciles d'accès sont favorisées.
    /// Les tourelles difficiles sont laissées à l'artillerie RED.
    /// </summary>
    public static Transform SelectTurretRushTarget(
        Transform self,
        string myTag,
        int maxAttackersPerTurret)
    {
        Vector3 myPos =
            self.position;


        Transform best = null;

        float bestScore =
            float.MinValue;


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (
                !(unit is Turret) ||
                unit.CompareTag(myTag)
            )
            {
                continue;
            }


            Transform target =
                unit.transform;


            float expected =
                GetExpectedHealth(
                    target
                );


            if (expected <= 0f)
                continue;


            // Vérifier que ce drone peut réellement atteindre
            // une position de tir autour de la tourelle.
            int reachableSlots =
                CountReachableTurretAttackSlots(
                    self,
                    target
                );


            // Aucun emplacement accessible :
            // ne surtout pas sélectionner cette tourelle.
            if (reachableSlots <= 0)
                continue;


            float difficulty =
                GetTurretAccessDifficultyForDrone(
                    self,
                    target
                );


            int attackers =
                CountAttackers(
                    target,
                    self.gameObject
                );


            float score =
                // Cible proche.
                -FlatDistance(
                    myPos,
                    target.position
                )

                // Finir les tourelles déjà affaiblies.
                -0.3f * expected

                // Focus fire limité.
                +
                12f *
                System.Math.Min(
                    attackers,
                    maxAttackersPerTurret - 1
                )

                // Éviter que trop de raiders attaquent
                // exactement la même tourelle.
                -
                40f *
                System.Math.Max(
                    0,
                    attackers -
                    (maxAttackersPerTurret - 1)
                )

                // Plus il existe de positions accessibles,
                // plus la cible est intéressante pour un drone.
                +
                4f * reachableSlots

                // Forte pénalité pour une tourelle nécessitant
                // un long détour ou située sur un terrain complexe.
                -
                60f * difficulty;


            if (score > bestScore)
            {
                bestScore =
                    score;

                best =
                    target;
            }
        }


        return best;
    }


    // =========================================================
    // RÔLES : RAIDERS
    // =========================================================

    static readonly HashSet<GameObject>
        s_Raiders =
            new HashSet<GameObject>();


    static float s_RaidersAssignedAt =
        -1f;


    static int s_RaidersRequested =
        -1;


    public static bool IsRaider(
        GameObject drone,
        string myTag,
        int raidersCount)
    {
        if (raidersCount <= 0)
            return false;


        bool newBattle =
            s_RaidersAssignedAt < 0f
            ||
            Time.timeSinceLevelLoad <
            s_RaidersAssignedAt;


        if (
            newBattle ||
            s_RaidersRequested !=
            raidersCount
        )
        {
            AssignRaiders(
                myTag,
                raidersCount
            );
        }


        return
            s_Raiders.Contains(
                drone
            );
    }


    static void AssignRaiders(
        string myTag,
        int raidersCount)
    {
        s_Raiders.Clear();


        s_RaidersAssignedAt =
            Time.timeSinceLevelLoad;


        s_RaidersRequested =
            raidersCount;


        List<ArmyElement> myDrones =
            new List<ArmyElement>();


        List<Vector3> enemyTurrets =
            new List<Vector3>();


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (
                unit is Drone &&
                unit.CompareTag(myTag)
            )
            {
                myDrones.Add(
                    unit
                );
            }
            else if (
                unit is Turret &&
                !unit.CompareTag(myTag)
            )
            {
                enemyTurrets.Add(
                    unit.transform.position
                );
            }
        }


        if (enemyTurrets.Count == 0)
            return;


        myDrones.Sort(
            (a, b) =>
                DistanceToClosest(
                    a.transform.position,
                    enemyTurrets
                )
                .CompareTo(
                    DistanceToClosest(
                        b.transform.position,
                        enemyTurrets
                    )
                )
        );


        for (
            int i = 0;
            i < myDrones.Count &&
            i < raidersCount;
            i++)
        {
            s_Raiders.Add(
                myDrones[i].gameObject
            );
        }
    }


    static float DistanceToClosest(
        Vector3 pos,
        List<Vector3> points)
    {
        float best =
            float.MaxValue;


        foreach (Vector3 point in points)
        {
            best =
                Mathf.Min(
                    best,
                    FlatDistance(
                        pos,
                        point
                    )
                );
        }


        return best;
    }


    public static int CountEnemyTurrets(
        string myTag)
    {
        int count = 0;


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (
                unit is Turret &&
                !unit.CompareTag(myTag)
            )
            {
                count++;
            }
        }


        return count;
    }


    // =========================================================
    // ACCESSIBILITÉ DES TOURELLES
    // =========================================================

    // Distance à laquelle un RED drone tente normalement
    // d'attaquer une tourelle.
    const float TURRET_ATTACK_RADIUS = 4f;

    // Rayon utilisé pour projeter les positions candidates
    // sur le NavMesh.
    const float TURRET_NAVMESH_SAMPLE_RADIUS = 1.5f;

    // Nombre de positions testées autour de chaque tourelle.
    const int TURRET_ACCESS_SAMPLES = 8;

    // Pour déterminer la difficulté globale d'une tourelle,
    // on vérifie les trois drones RED actuellement les plus proches.
    const int TURRET_ACCESS_DRONES_TO_CHECK = 3;

    // Le terrain ne change pas, mais la position des drones change.
    // Recalcul une fois par seconde maximum pour éviter des dizaines
    // de calculs NavMesh par frame.
    const float TURRET_DIFFICULTY_CACHE_DURATION = 1f;


    class TurretDifficultyCacheEntry
    {
        public float Difficulty;
        public float ExpireTime;
    }


    static readonly Dictionary<Transform, TurretDifficultyCacheEntry>
        s_TurretDifficultyCache =
            new Dictionary<Transform, TurretDifficultyCacheEntry>();


    /// <summary>
    /// Longueur réelle d'un chemin NavMesh.
    /// </summary>
    static float GetPathLength(
        NavMeshPath path)
    {
        if (
            path == null ||
            path.corners == null ||
            path.corners.Length < 2
        )
        {
            return 0f;
        }


        float length = 0f;


        for (
            int i = 1;
            i < path.corners.Length;
            i++)
        {
            length +=
                Vector3.Distance(
                    path.corners[i - 1],
                    path.corners[i]
                );
        }


        return length;
    }


    /// <summary>
    /// Analyse l'accessibilité d'une tourelle depuis un drone donné.
    ///
    /// reachableSlots :
    /// nombre de positions d'attaque accessibles autour de la tourelle.
    ///
    /// bestDetourRatio :
    /// longueur réelle du meilleur chemin / distance directe.
    ///
    /// 1.0 = chemin presque direct.
    /// > 1.0 = détour imposé par le terrain.
    /// </summary>
    static void AnalyseTurretAccess(
        Transform drone,
        Transform turret,
        out int reachableSlots,
        out float bestDetourRatio)
    {
        reachableSlots = 0;

        bestDetourRatio =
            float.PositiveInfinity;


        if (
            drone == null ||
            turret == null
        )
        {
            return;
        }


        NavMeshHit sourceHit;


        // Le pivot du drone peut être légèrement au-dessus
        // du NavMesh à cause du baseOffset.
        if (
            !NavMesh.SamplePosition(
                drone.position,
                out sourceHit,
                2f,
                NavMesh.AllAreas
            )
        )
        {
            return;
        }


        Vector3 sourcePos =
            sourceHit.position;


        Vector3 baseDir =
            Flat(
                drone.position -
                turret.position
            );


        if (baseDir.sqrMagnitude < 0.01f)
        {
            baseDir =
                Vector3.forward;
        }
        else
        {
            baseDir.Normalize();
        }


        for (
            int i = 0;
            i < TURRET_ACCESS_SAMPLES;
            i++)
        {
            float angle =
                i *
                (
                    360f /
                    TURRET_ACCESS_SAMPLES
                );


            Vector3 direction =
                Quaternion.Euler(
                    0f,
                    angle,
                    0f
                ) *
                baseDir;


            Vector3 candidate =
                turret.position
                +
                direction *
                TURRET_ATTACK_RADIUS;


            NavMeshHit targetHit;


            if (
                !NavMesh.SamplePosition(
                    candidate,
                    out targetHit,
                    TURRET_NAVMESH_SAMPLE_RADIUS,
                    NavMesh.AllAreas
                )
            )
            {
                continue;
            }


            NavMeshPath path =
                new NavMeshPath();


            bool pathFound =
                NavMesh.CalculatePath(
                    sourcePos,
                    targetHit.position,
                    NavMesh.AllAreas,
                    path
                );


            if (
                !pathFound ||
                path.status !=
                NavMeshPathStatus.PathComplete
            )
            {
                continue;
            }


            reachableSlots++;


            float pathLength =
                GetPathLength(
                    path
                );


            float directDistance =
                Mathf.Max(
                    1f,
                    FlatDistance(
                        sourcePos,
                        targetHit.position
                    )
                );


            float detourRatio =
                pathLength /
                directDistance;


            bestDetourRatio =
                Mathf.Min(
                    bestDetourRatio,
                    detourRatio
                );
        }
    }


    /// <summary>
    /// Nombre de positions d'attaque réellement accessibles
    /// pour un drone précis.
    ///
    /// 0 = inaccessible.
    /// 8 = très facilement accessible.
    /// </summary>
    public static int CountReachableTurretAttackSlots(
        Transform drone,
        Transform turret)
    {
        int reachableSlots;

        float bestDetourRatio;


        AnalyseTurretAccess(
            drone,
            turret,
            out reachableSlots,
            out bestDetourRatio
        );


        return reachableSlots;
    }


    /// <summary>
    /// Difficulté d'accès d'une tourelle pour un drone donné.
    ///
    /// 0 = très facile.
    /// 1 = très difficile / inaccessible.
    ///
    /// Le score prend en compte :
    /// - le nombre de positions d'attaque accessibles ;
    /// - le détour imposé par le NavMesh ;
    /// - la différence de hauteur.
    /// </summary>
    public static float GetTurretAccessDifficultyForDrone(
        Transform drone,
        Transform turret)
    {
        if (
            drone == null ||
            turret == null
        )
        {
            return 1f;
        }


        int reachableSlots;

        float bestDetourRatio;


        AnalyseTurretAccess(
            drone,
            turret,
            out reachableSlots,
            out bestDetourRatio
        );


        if (reachableSlots <= 0)
        {
            return 1f;
        }


        // 0 si les 8 positions sont accessibles.
        // 1 si aucune ne l'est.
        float accessPenalty =
            1f -
            (
                reachableSlots /
                (float)TURRET_ACCESS_SAMPLES
            );


        // Un chemin direct produit environ 1.
        // Les grands détours dus aux terrasses,
        // falaises ou bords de carte augmentent ce score.
        float detourPenalty =
            float.IsInfinity(
                bestDetourRatio
            )
            ?
            1f
            :
            Mathf.Clamp01(
                (
                    bestDetourRatio -
                    1f
                )
                /
                1.5f
            );


        // La hauteur n'est qu'un indicateur secondaire :
        // elle ne suffit pas à elle seule à déclarer
        // une tourelle difficile.
        float heightPenalty =
            Mathf.Clamp01(
                Mathf.Abs(
                    drone.position.y -
                    turret.position.y
                )
                /
                8f
            );


        return
            0.65f * accessPenalty
            +
            0.25f * detourPenalty
            +
            0.10f * heightPenalty;
    }


    /// <summary>
    /// Difficulté globale d'une tourelle pour l'armée RED.
    ///
    /// On teste les trois drones RED les plus proches.
    /// Si même le meilleur d'entre eux a du mal à atteindre
    /// la tourelle, celle-ci devient une cible prioritaire
    /// pour l'artillerie.
    /// </summary>
    public static float GetTurretAccessDifficulty(
        Transform turret,
        string myTag)
    {
        if (turret == null)
            return 1f;


        TurretDifficultyCacheEntry cached;


        if (
            s_TurretDifficultyCache.TryGetValue(
                turret,
                out cached
            )
            &&
            Time.time <
            cached.ExpireTime
        )
        {
            return
                cached.Difficulty;
        }


        List<Drone> alliedDrones =
            new List<Drone>();


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            Drone drone =
                unit as Drone;


            if (drone == null)
                continue;


            if (!drone.CompareTag(myTag))
                continue;


            if (drone.Health <= 0f)
                continue;


            alliedDrones.Add(
                drone
            );
        }


        // Plus aucun drone RED :
        // l'artillerie traite normalement les tourelles restantes.
        if (alliedDrones.Count == 0)
        {
            return 1f;
        }


        alliedDrones.Sort(
            (a, b) =>
                FlatDistance(
                    a.transform.position,
                    turret.position
                )
                .CompareTo(
                    FlatDistance(
                        b.transform.position,
                        turret.position
                    )
                )
        );


        int dronesToCheck =
            Mathf.Min(
                TURRET_ACCESS_DRONES_TO_CHECK,
                alliedDrones.Count
            );


        // On cherche le drone pour lequel cette tourelle
        // est la PLUS FACILE.
        //
        // Si même ce drone a du mal, alors la tourelle
        // doit être fortement prioritaire pour l'artillerie.
        float easiestDifficulty =
            1f;


        for (
            int i = 0;
            i < dronesToCheck;
            i++)
        {
            float difficulty =
                GetTurretAccessDifficultyForDrone(
                    alliedDrones[i].transform,
                    turret
                );


            easiestDifficulty =
                Mathf.Min(
                    easiestDifficulty,
                    difficulty
                );


            if (easiestDifficulty <= 0.01f)
            {
                break;
            }
        }


        s_TurretDifficultyCache[turret] =
            new TurretDifficultyCacheEntry
            {
                Difficulty =
                    easiestDifficulty,

                ExpireTime =
                    Time.time +
                    TURRET_DIFFICULTY_CACHE_DURATION
            };


        return
            easiestDifficulty;
    }


    // =========================================================
    // TIR ANTICIPÉ
    // =========================================================

    public static float MissileDistanceAt(
        float t,
        float startSpeed)
    {
        float tAcc =
            Mathf.Max(
                0f,
                (
                    MISSILE_MAX_SPEED -
                    startSpeed
                )
                /
                MISSILE_ACCELERATION
            );


        if (t <= tAcc)
        {
            return
                startSpeed * t
                +
                0.5f *
                MISSILE_ACCELERATION *
                t * t;
        }


        float dAcc =
            startSpeed * tAcc
            +
            0.5f *
            MISSILE_ACCELERATION *
            tAcc * tAcc;


        return
            dAcc
            +
            MISSILE_MAX_SPEED *
            (t - tAcc);
    }


    public static float MissileTimeToDistance(
        float distance,
        float startSpeed)
    {
        float tAcc =
            Mathf.Max(
                0f,
                (
                    MISSILE_MAX_SPEED -
                    startSpeed
                )
                /
                MISSILE_ACCELERATION
            );


        float dAcc =
            startSpeed * tAcc
            +
            0.5f *
            MISSILE_ACCELERATION *
            tAcc * tAcc;


        if (distance <= dAcc)
        {
            float a =
                0.5f *
                MISSILE_ACCELERATION;


            float disc =
                startSpeed *
                startSpeed
                +
                4f *
                a *
                distance;


            return
                (
                    -startSpeed +
                    Mathf.Sqrt(disc)
                )
                /
                (2f * a);
        }


        return
            tAcc
            +
            (
                distance -
                dAcc
            )
            /
            MISSILE_MAX_SPEED;
    }


    public static Vector3 GetTargetVelocity(
        Transform target)
    {
        NavMeshAgent agent =
            target.GetComponent<NavMeshAgent>();


        if (
            agent != null &&
            agent.enabled
        )
        {
            return
                agent.velocity;
        }


        return
            Vector3.zero;
    }


    public static Vector3 PredictAimPoint(
        Vector3 shooterPos,
        float missileStartSpeed,
        Transform target)
    {
        Vector3 targetPos =
            GetAimCenter(
                target
            );


        Vector3 velocity =
            Flat(
                GetTargetVelocity(
                    target
                )
            );


        if (velocity.sqrMagnitude < 0.04f)
        {
            return
                targetPos;
        }


        Vector3 aim =
            targetPos;


        for (int i = 0; i < 4; i++)
        {
            float distance =
                FlatDistance(
                    shooterPos,
                    aim
                );


            float time =
                Mathf.Min(
                    MissileTimeToDistance(
                        distance,
                        missileStartSpeed
                    ),
                    MISSILE_LIFE_DURATION
                );


            aim =
                targetPos
                +
                Vector3.ClampMagnitude(
                    velocity * time,
                    MAX_LEAD_DISTANCE
                );
        }


        return aim;
    }


    // =========================================================
    // LIGNE DE TIR
    // =========================================================

    static readonly RaycastHit[]
        s_Hits =
            new RaycastHit[32];


    class HitDistanceComparer :
        IComparer<RaycastHit>
    {
        public int Compare(
            RaycastHit a,
            RaycastHit b)
        {
            return
                a.distance
                    .CompareTo(
                        b.distance
                    );
        }
    }


    static readonly HitDistanceComparer
        s_HitComparer =
            new HitDistanceComparer();


    public static bool HasLineOfFire(
        Vector3 from,
        Transform target,
        string myTag)
    {
        Vector3 to =
            GetAimCenter(
                target
            );


        Vector3 dir =
            to - from;


        float distance =
            dir.magnitude;


        if (distance < 0.05f)
            return true;


        int hitCount =
            Physics.RaycastNonAlloc(
                from,
                dir / distance,
                s_Hits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore
            );


        System.Array.Sort(
            s_Hits,
            0,
            hitCount,
            s_HitComparer
        );


        for (
            int i = 0;
            i < hitCount;
            i++)
        {
            Transform hitTransform =
                s_Hits[i]
                    .collider
                    .transform;


            if (
                hitTransform == target ||
                hitTransform.IsChildOf(
                    target
                )
            )
            {
                return true;
            }


            IArmyElement element =
                s_Hits[i]
                    .collider
                    .GetComponentInParent<IArmyElement>();


            if (element != null)
            {
                Component component =
                    element as Component;


                if (
                    component != null &&
                    component.CompareTag(
                        myTag
                    )
                )
                {
                    continue;
                }


                return true;
            }


            if (
                s_Hits[i]
                    .collider
                    .CompareTag(
                        myTag
                    )
            )
            {
                continue;
            }


            return false;
        }


        return true;
    }


    // =========================================================
    // MENACES : GRENADES ENNEMIES
    // =========================================================

    public struct GrenadeThreat
    {
        public Vector3 Impact;
        public float TimeToImpact;
    }


    class RocketSample
    {
        public Vector3 Position;
        public float Time;
        public int Frame;
    }


    static int s_ThreatFrame =
        -1;


    static string s_ThreatTag;


    static readonly List<GrenadeThreat>
        s_Threats =
            new List<GrenadeThreat>();


    static readonly Dictionary<Rocket, RocketSample>
        s_RocketSamples =
            new Dictionary<Rocket, RocketSample>();


    static readonly List<Rocket>
        s_ToRemove =
            new List<Rocket>();


    public static List<GrenadeThreat>
        GetEnemyGrenadeThreats(
            string myTag)
    {
        if (
            s_ThreatFrame ==
            Time.frameCount
            &&
            s_ThreatTag ==
            myTag
        )
        {
            return
                s_Threats;
        }


        s_ThreatFrame =
            Time.frameCount;


        s_ThreatTag =
            myTag;


        s_Threats.Clear();


        Rocket[] rockets =
            Object.FindObjectsByType<Rocket>(
                FindObjectsSortMode.None
            );


        foreach (Rocket rocket in rockets)
        {
            if (
                rocket == null ||
                rocket.CompareTag(myTag)
            )
            {
                continue;
            }


            Rocket id =
                rocket;


            Vector3 pos =
                rocket.transform.position;


            Vector3 velocity =
                Vector3.zero;


            bool hasVelocity =
                false;


            Rigidbody rb =
                rocket.GetComponent<Rigidbody>();


            if (
                rb != null &&
                !rb.isKinematic &&
                rb.linearVelocity.sqrMagnitude > 1f
            )
            {
                velocity =
                    rb.linearVelocity;

                hasVelocity =
                    true;
            }


            RocketSample sample;


            if (
                s_RocketSamples.TryGetValue(
                    id,
                    out sample
                )
            )
            {
                float dt =
                    Time.time -
                    sample.Time;


                if (
                    !hasVelocity &&
                    dt > 0.0001f
                )
                {
                    velocity =
                        (
                            pos -
                            sample.Position
                        )
                        /
                        dt;


                    hasVelocity =
                        velocity.sqrMagnitude > 1f;
                }
            }
            else
            {
                sample =
                    new RocketSample();


                s_RocketSamples[id] =
                    sample;
            }


            sample.Position =
                pos;


            sample.Time =
                Time.time;


            sample.Frame =
                Time.frameCount;


            if (!hasVelocity)
                continue;


            Vector3 impact;
            float time;


            if (
                PredictGrenadeImpact(
                    pos,
                    velocity,
                    out impact,
                    out time
                )
            )
            {
                s_Threats.Add(
                    new GrenadeThreat
                    {
                        Impact = impact,
                        TimeToImpact = time
                    }
                );
            }
        }


        s_ToRemove.Clear();


        foreach (
            KeyValuePair<Rocket, RocketSample> kv
            in s_RocketSamples)
        {
            if (
                kv.Key == null ||
                Time.frameCount -
                kv.Value.Frame >
                30
            )
            {
                s_ToRemove.Add(
                    kv.Key
                );
            }
        }


        foreach (
            Rocket rocket
            in s_ToRemove)
        {
            s_RocketSamples.Remove(
                rocket
            );
        }


        return
            s_Threats;
    }


    public static bool PredictGrenadeImpact(
        Vector3 pos,
        Vector3 velocity,
        out Vector3 impact,
        out float time)
    {
        impact =
            pos;


        time =
            0f;


        float gravity =
            Physics.gravity.y;


        if (gravity > -0.01f)
            return false;


        float groundY =
            GroundHeight(
                pos,
                pos.y - 10f
            );


        for (int i = 0; i < 3; i++)
        {
            float yEnd =
                groundY +
                DRONE_PIVOT_HEIGHT;


            float a =
                0.5f *
                gravity;


            float b =
                velocity.y;


            float c =
                pos.y -
                yEnd;


            float disc =
                b * b
                -
                4f *
                a *
                c;


            if (disc < 0f)
                return false;


            float t =
                (
                    -b -
                    Mathf.Sqrt(disc)
                )
                /
                (2f * a);


            if (t < 0f)
                return false;


            time =
                t;


            impact =
                new Vector3(
                    pos.x +
                    velocity.x * t,

                    yEnd,

                    pos.z +
                    velocity.z * t
                );


            groundY =
                GroundHeight(
                    impact,
                    groundY
                );
        }


        return true;
    }


    static float GroundHeight(
        Vector3 pos,
        float fallback)
    {
        if (
            TerrainManager.Instance ==
            null
        )
        {
            return fallback;
        }


        Vector3 terrainPos =
            Vector3.zero;


        Vector3 normal =
            Vector3.zero;


        if (
            TerrainManager.Instance
                .GetVerticallyAlignedPositionOnTerrain(
                    pos,
                    ref terrainPos,
                    ref normal
                )
        )
        {
            return
                terrainPos.y;
        }


        return fallback;
    }


    public static bool IsInGrenadeDanger(
        Vector3 pos,
        string myTag,
        float dangerRadius,
        float maxTime)
    {
        foreach (
            GrenadeThreat threat
            in GetEnemyGrenadeThreats(
                myTag
            ))
        {
            if (
                threat.TimeToImpact >
                maxTime
            )
            {
                continue;
            }


            if (
                Mathf.Abs(
                    threat.Impact.y -
                    pos.y
                )
                >
                GRENADE_DAMAGE_RADIUS +
                1f
            )
            {
                continue;
            }


            if (
                FlatDistance(
                    pos,
                    threat.Impact
                )
                <
                dangerRadius
            )
            {
                return true;
            }
        }


        return false;
    }


    // =========================================================
    // ESPACEMENT DES DRONES ROUGES
    // =========================================================

    const float RESERVATION_LIFETIME =
        6f;


    static readonly Dictionary<GameObject, Vector3>
        s_Reservations =
            new Dictionary<GameObject, Vector3>();


    static readonly Dictionary<GameObject, float>
        s_ReservationTimes =
            new Dictionary<GameObject, float>();


    public static void ReserveSlot(
        GameObject droneId,
        Vector3 slot)
    {
        s_Reservations[droneId] =
            slot;


        s_ReservationTimes[droneId] =
            Time.time;
    }


    public static void ReleaseSlot(
        GameObject droneId)
    {
        s_Reservations.Remove(
            droneId
        );


        s_ReservationTimes.Remove(
            droneId
        );
    }


    public static float AllyCrowdPenalty(
        Vector3 pos,
        GameObject myId,
        string myTag,
        float spacing)
    {
        float penalty =
            0f;


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (
                !(unit is Drone) ||
                !unit.CompareTag(myTag)
            )
            {
                continue;
            }


            if (unit.gameObject == myId)
                continue;


            float distance =
                FlatDistance(
                    pos,
                    unit.transform.position
                );


            if (distance < spacing)
            {
                penalty +=
                    1f -
                    distance /
                    spacing;
            }
        }


        foreach (
            KeyValuePair<GameObject, Vector3> kv
            in s_Reservations)
        {
            if (
                kv.Key == null ||
                kv.Key == myId
            )
            {
                continue;
            }


            float reservationTime;


            if (
                !s_ReservationTimes.TryGetValue(
                    kv.Key,
                    out reservationTime
                )
                ||
                Time.time -
                reservationTime >
                RESERVATION_LIFETIME
            )
            {
                continue;
            }


            float distance =
                FlatDistance(
                    pos,
                    kv.Value
                );


            if (distance < spacing)
            {
                penalty +=
                    1f -
                    distance /
                    spacing;
            }
        }


        return penalty;
    }


    public static int EnemyDronesNear(
        Vector3 pos,
        string myTag,
        float radius,
        Transform ignore)
    {
        int count = 0;


        foreach (
            ArmyElement unit
            in AllUnits())
        {
            if (
                !(unit is Drone) ||
                unit.CompareTag(myTag) ||
                unit.transform == ignore
            )
            {
                continue;
            }


            if (
                FlatDistance(
                    pos,
                    unit.transform.position
                )
                <
                radius
            )
            {
                count++;
            }
        }


        return count;
    }


    // =========================================================
    // CHOIX D'UNE POSITION DE TIR
    // =========================================================

    static readonly float[]
        s_RadiusFactors =
        {
            1f,
            0.75f,
            1.3f
        };


    const int SLOT_ANGLES =
        12;


    public static bool FindFiringSlot(
        Transform self,
        GameObject myId,
        string myTag,
        Transform target,
        float preferredRange,
        float allySpacing,
        float pivotHeight,
        List<Vector3> rejectedSlots,
        out Vector3 bestSlot)
    {
        bestSlot =
            Vector3.zero;


        float bestScore =
            float.MinValue;


        Vector3 myPos =
            self.position;


        Vector3 targetPos =
            target.position;


        Vector3 baseDir =
            Flat(
                myPos -
                targetPos
            );


        baseDir =
            baseDir.sqrMagnitude > 0.01f
                ?
                baseDir.normalized
                :
                Flat(
                    self.forward
                ).normalized;


        foreach (
            float factor
            in s_RadiusFactors)
        {
            float radius =
                Mathf.Max(
                    2.5f,
                    preferredRange *
                    factor
                );


            for (
                int k = 0;
                k < SLOT_ANGLES;
                k++)
            {
                float angle =
                    (
                        k % 2 == 0
                            ? 1f
                            : -1f
                    )
                    *
                    (
                        (k + 1) /
                        2
                    )
                    *
                    (
                        360f /
                        SLOT_ANGLES
                    );


                Vector3 dir =
                    Quaternion.Euler(
                        0f,
                        angle,
                        0f
                    )
                    *
                    baseDir;


                Vector3 candidate =
                    targetPos
                    +
                    dir *
                    radius;


                NavMeshHit hit;


                if (
                    !NavMesh.SamplePosition(
                        candidate,
                        out hit,
                        2f,
                        NavMesh.AllAreas
                    )
                )
                {
                    continue;
                }


                Vector3 point =
                    hit.position;


                if (
                    IsRejected(
                        point,
                        rejectedSlots
                    )
                )
                {
                    continue;
                }


                float score =
                    0f;


                // Déplacement court.
                score -=
                    FlatDistance(
                        myPos,
                        point
                    );


                // Espacement RED.
                score -=
                    35f *
                    AllyCrowdPenalty(
                        point,
                        myId,
                        myTag,
                        allySpacing
                    );


                // Éviter les groupes GREEN.
                score -=
                    8f *
                    EnemyDronesNear(
                        point,
                        myTag,
                        6f,
                        target
                    );


                // Éviter les grenades prévues.
                if (
                    IsInGrenadeDanger(
                        point,
                        myTag,
                        GRENADE_DAMAGE_RADIUS +
                        2f,
                        10f
                    )
                )
                {
                    score -= 100f;
                }


                // Ligne de tir.
                if (
                    !HasLineOfFire(
                        point +
                        Vector3.up *
                        pivotHeight,
                        target,
                        myTag
                    )
                )
                {
                    score -= 40f;
                }


                if (score > bestScore)
                {
                    bestScore =
                        score;


                    bestSlot =
                        point;
                }
            }
        }


        return
            bestScore >
            float.MinValue;
    }


    // =========================================================
    // STRAFE
    // =========================================================

    public static bool FindStrafePoint(
        Transform self,
        GameObject myId,
        string myTag,
        Transform target,
        float minRange,
        float maxRange,
        float pivotHeight,
        float preferredSide,
        out Vector3 bestPoint,
        out float chosenSide)
    {
        bestPoint =
            self.position;


        chosenSide =
            preferredSide;


        Vector3 myPos =
            self.position;


        Vector3 toTarget =
            target != null
                ?
                Flat(
                    target.position -
                    myPos
                )
                :
                Flat(
                    self.forward
                );


        toTarget =
            toTarget.sqrMagnitude > 0.01f
                ?
                toTarget.normalized
                :
                Vector3.forward;


        Vector3 right =
            Vector3.Cross(
                Vector3.up,
                toTarget
            );


        float bestScore =
            float.MinValue;


        float[] distances =
        {
            4f,
            6.5f
        };


        foreach (
            float distance
            in distances)
        {
            for (
                int k = 0;
                k < 12;
                k++)
            {
                Vector3 dir =
                    Quaternion.Euler(
                        0f,
                        k * 30f,
                        0f
                    )
                    *
                    toTarget;


                NavMeshHit hit;


                if (
                    !NavMesh.SamplePosition(
                        myPos +
                        dir *
                        distance,
                        out hit,
                        2f,
                        NavMesh.AllAreas
                    )
                )
                {
                    continue;
                }


                Vector3 point =
                    hit.position;


                float score =
                    0f;


                score +=
                    6f *
                    (
                        1f -
                        Mathf.Abs(
                            Vector3.Dot(
                                dir,
                                toTarget
                            )
                        )
                    );


                float side =
                    Vector3.Dot(
                        dir,
                        right
                    ) >= 0f
                    ?
                    1f
                    :
                    -1f;


                if (side == preferredSide)
                {
                    score += 3f;
                }


                if (target != null)
                {
                    float targetDistance =
                        FlatDistance(
                            point,
                            target.position
                        );


                    if (targetDistance < minRange)
                    {
                        score -=
                            3f *
                            (
                                minRange -
                                targetDistance
                            );
                    }


                    if (targetDistance > maxRange)
                    {
                        score -=
                            3f *
                            (
                                targetDistance -
                                maxRange
                            );
                    }


                    if (
                        !HasLineOfFire(
                            point +
                            Vector3.up *
                            pivotHeight,
                            target,
                            myTag
                        )
                    )
                    {
                        score -= 20f;
                    }
                }


                score -=
                    30f *
                    AllyCrowdPenalty(
                        point,
                        myId,
                        myTag,
                        6f
                    );


                score -=
                    6f *
                    EnemyDronesNear(
                        point,
                        myTag,
                        6f,
                        target
                    );


                if (
                    IsInGrenadeDanger(
                        point,
                        myTag,
                        GRENADE_DAMAGE_RADIUS +
                        2f,
                        10f
                    )
                )
                {
                    score -= 100f;
                }


                if (score > bestScore)
                {
                    bestScore =
                        score;


                    bestPoint =
                        point;


                    chosenSide =
                        side;
                }
            }
        }


        return
            bestScore >
            float.MinValue;
    }


    static bool IsRejected(
        Vector3 point,
        List<Vector3> rejected)
    {
        if (rejected == null)
            return false;


        foreach (
            Vector3 rejectedPoint
            in rejected)
        {
            if (
                FlatDistance(
                    point,
                    rejectedPoint
                )
                <
                1.5f
            )
            {
                return true;
            }
        }


        return false;
    }


    // =========================================================
    // GRENADE ESCAPE
    // =========================================================

    public static bool FindEscapePoint(
        Transform self,
        GameObject myId,
        string myTag,
        Transform currentTarget,
        float dangerRadius,
        out Vector3 escapePoint)
    {
        escapePoint =
            self.position;


        Vector3 myPos =
            self.position;


        List<GrenadeThreat> threats =
            GetEnemyGrenadeThreats(
                myTag
            );


        Vector3 away =
            Vector3.zero;


        foreach (
            GrenadeThreat threat
            in threats)
        {
            Vector3 direction =
                Flat(
                    myPos -
                    threat.Impact
                );


            float distance =
                direction.magnitude;


            if (
                distance >
                dangerRadius +
                3f
            )
            {
                continue;
            }


            float urgency =
                1f /
                Mathf.Max(
                    0.2f,
                    threat.TimeToImpact
                );


            away +=
                (
                    distance > 0.01f
                        ?
                        direction /
                        distance
                        :
                        Flat(
                            self.right
                        )
                )
                *
                urgency;
        }


        if (away.sqrMagnitude < 0.0001f)
        {
            away =
                Flat(
                    self.right
                );
        }


        away.Normalize();


        float bestScore =
            float.MinValue;


        float[] distances =
        {
            dangerRadius + 1.5f,
            dangerRadius + 4f
        };


        foreach (
            float distance
            in distances)
        {
            for (
                int k = 0;
                k < 16;
                k++)
            {
                Vector3 dir =
                    Quaternion.Euler(
                        0f,
                        k * 22.5f,
                        0f
                    )
                    *
                    away;


                NavMeshHit hit;


                if (
                    !NavMesh.SamplePosition(
                        myPos +
                        dir *
                        distance,
                        out hit,
                        2f,
                        NavMesh.AllAreas
                    )
                )
                {
                    continue;
                }


                Vector3 point =
                    hit.position;


                float score =
                    0f;


                if (
                    IsInGrenadeDanger(
                        point,
                        myTag,
                        dangerRadius,
                        10f
                    )
                )
                {
                    score -= 200f;
                }


                score +=
                    10f *
                    Vector3.Dot(
                        dir,
                        away
                    );


                score -=
                    0.5f *
                    FlatDistance(
                        myPos,
                        point
                    );


                score -=
                    25f *
                    AllyCrowdPenalty(
                        point,
                        myId,
                        myTag,
                        6f
                    );


                if (currentTarget != null)
                {
                    float targetDistance =
                        FlatDistance(
                            point,
                            currentTarget.position
                        );


                    if (targetDistance > 25f)
                    {
                        score -=
                            targetDistance -
                            25f;
                    }
                }


                if (score > bestScore)
                {
                    bestScore =
                        score;


                    escapePoint =
                        point;
                }
            }
        }


        return
            bestScore >
            float.MinValue;
    }
}