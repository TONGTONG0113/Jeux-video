using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Outils tactiques partagés par les tâches Behavior Designer des drones ROUGES.
/// (Ruofan - DroneRedBehavior)
///
///  - Tir anticipé (lead) : prédiction du point d'interception d'un missile
///    à partir de la vitesse de la cible et du profil de vitesse du missile.
///  - Ligne de tir : vérification qu'aucun obstacle ne bloque le missile.
///  - Menaces d'artillerie : prédiction du point d'impact des grenades ennemies
///    en vol (le chef d'état-major "voit" les trajectoires, cf. sujet).
///  - Positions de tir : choix d'une position autour de la cible qui garde
///    l'espacement entre drones rouges (anti dégâts de zone).
///
/// Aucune caractéristique de l'Annexe A n'est modifiée : les constantes
/// ci-dessous sont uniquement LUES pour faire les prédictions.
/// </summary>
public static class RedDroneTactics
{
    // =========================================================
    // ANNEXE A (lecture seule)
    // =========================================================

    public const float MISSILE_ACCELERATION = 40f;   // m/s²
    public const float MISSILE_MAX_SPEED = 20f;      // m/s
    public const float MISSILE_LIFE_DURATION = 3f;   // s
    public const float GRENADE_DAMAGE_RADIUS = 5f;   // m
    public const float DRONE_COLLIDER_RADIUS = 0.4f; // m

    // Hauteur du pivot d'un drone au-dessus du sol (NavMeshAgent.baseOffset).
    const float DRONE_PIVOT_HEIGHT = 1f;

    // Le lead est plafonné : les drones verts changent souvent de direction.
    const float MAX_LEAD_DISTANCE = 6f;

    // =========================================================
    // OUTILS GÉNÉRAUX
    // =========================================================

    public static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static bool IsAlive(Transform t)
    {
        if (t == null) return false;
        IArmyElement element = t.GetComponent<IArmyElement>();
        return element != null && element.Health > 0f;
    }

    /// <summary>Centre du collider de la cible (meilleur point de visée que le pivot).</summary>
    public static Vector3 GetAimCenter(Transform target)
    {
        Collider col = target.GetComponent<Collider>();
        if (col == null) col = target.GetComponentInChildren<Collider>();
        return col != null ? col.bounds.center : target.position;
    }

    // =========================================================
    // CACHE DES UNITÉS (recalculé une fois par frame)
    // =========================================================

    static int s_UnitsFrame = -1;
    static readonly List<ArmyElement> s_AllUnits = new List<ArmyElement>();

    static List<ArmyElement> AllUnits()
    {
        if (s_UnitsFrame != Time.frameCount)
        {
            s_UnitsFrame = Time.frameCount;
            s_AllUnits.Clear();
            ArmyElement[] units = Object.FindObjectsByType<ArmyElement>(FindObjectsSortMode.None);
            foreach (ArmyElement u in units)
            {
                if (u != null && u.Health > 0f) s_AllUnits.Add(u);
            }
        }
        return s_AllUnits;
    }

    /// <summary>Vrai si la position est proche d'une tourelle de notre camp.</summary>
    public static bool IsNearAllyTurret(Vector3 pos, string myTag, float radius)
    {
        foreach (ArmyElement u in AllUnits())
        {
            if (u is Turret && u.CompareTag(myTag) && FlatDistance(pos, u.transform.position) <= radius)
                return true;
        }
        return false;
    }

    /// <summary>Nombre de drones ennemis encore en vie.</summary>
    public static int CountEnemyDrones(string myTag)
    {
        int count = 0;
        foreach (ArmyElement u in AllUnits())
        {
            if (u is Drone && !u.CompareTag(myTag)) count++;
        }
        return count;
    }

    /// <summary>Ennemi vivant le plus proche, sans limite de distance (secours).</summary>
    public static GameObject FindNearestEnemy(Vector3 pos, string myTag)
    {
        ArmyElement best = null;
        float bestDist = float.MaxValue;

        foreach (ArmyElement u in AllUnits())
        {
            if (u.CompareTag(myTag)) continue;
            float d = FlatDistance(pos, u.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = u;
            }
        }
        return best != null ? best.gameObject : null;
    }

    // =========================================================
    // TIR CONCENTRÉ (FOCUS FIRE)
    // Constat en simulation : les dégâts rouges étaient répartis sur trop de
    // cibles (490 dégâts de missiles pour seulement 3 drones verts détruits).
    // On concentre donc les drones rouges sur les mêmes cibles, sans overkill.
    // =========================================================

    class PendingMissile
    {
        public Transform Target;
        public float Damage;
        public float ExpireTime;
    }

    static readonly Dictionary<GameObject, Transform> s_DroneTargets = new Dictionary<GameObject, Transform>();
    static readonly List<PendingMissile> s_PendingMissiles = new List<PendingMissile>();
    static readonly List<GameObject> s_DeadKeys = new List<GameObject>();

    public static void SetDroneTarget(GameObject drone, Transform target)
    {
        if (drone == null) return;
        if (target == null) s_DroneTargets.Remove(drone);
        else s_DroneTargets[drone] = target;
    }

    /// <summary>Nombre d'autres drones rouges qui attaquent déjà cette cible.</summary>
    public static int CountAttackers(Transform target, GameObject except)
    {
        int count = 0;
        s_DeadKeys.Clear();
        foreach (KeyValuePair<GameObject, Transform> kv in s_DroneTargets)
        {
            if (kv.Key == null) { s_DeadKeys.Add(kv.Key); continue; }
            if (kv.Key == except) continue;
            if (kv.Value == target) count++;
        }
        foreach (GameObject k in s_DeadKeys) s_DroneTargets.Remove(k);
        return count;
    }

    /// <summary>Durée de vie ≈ temps de vol d'un missile à 25 m (≈ 1,4 s) + marge.</summary>
    public static void RegisterPendingDamage(Transform target, float damage, float lifetime)
    {
        if (target == null) return;
        s_PendingMissiles.Add(new PendingMissile { Target = target, Damage = damage, ExpireTime = Time.time + lifetime });
    }

    public static float GetPendingDamage(Transform target)
    {
        s_PendingMissiles.RemoveAll(p => p.Target == null || Time.time >= p.ExpireTime);
        float sum = 0f;
        foreach (PendingMissile p in s_PendingMissiles)
        {
            if (p.Target == target) sum += p.Damage;
        }
        return sum;
    }

    public static float GetExpectedHealth(Transform target)
    {
        IArmyElement element = target != null ? target.GetComponent<IArmyElement>() : null;
        if (element == null) return 0f;
        return element.Health - GetPendingDamage(target);
    }

    /// <summary>
    /// Choix de cible "focus fire" pour un drone rouge :
    ///  - drones verts d'abord (ce sont eux qui tuent nos drones),
    ///  - bonus si d'autres drones rouges l'attaquent déjà (concentration),
    ///  - bonus si la cible est déjà endommagée (on finit les blessés),
    ///  - exclusion des cibles déjà condamnées par les missiles en vol (anti overkill),
    ///  - sinon, tourelle verte la plus faible.
    /// </summary>
    public static Transform SelectFocusTarget(Transform self, string myTag, int maxAttackersBonus)
    {
        Vector3 myPos = self.position;
        Transform bestDrone = null;
        float bestScore = float.MinValue;
        Transform bestTurret = null;
        float bestTurretScore = float.MinValue;

        foreach (ArmyElement u in AllUnits())
        {
            if (u.CompareTag(myTag)) continue;
            Transform t = u.transform;

            float expected = GetExpectedHealth(t);
            if (expected <= 0f) continue;

            float dist = FlatDistance(myPos, t.position);

            if (u is Drone)
            {
                int attackers = System.Math.Min(CountAttackers(t, self.gameObject), maxAttackersBonus);
                float score = -dist
                              + 15f * attackers
                              + 0.8f * (50f - expected);

                // Protéger notre artillerie (100 % de précision en simulation) :
                // priorité aux drones verts qui attaquent nos tourelles.
                if (IsNearAllyTurret(t.position, myTag, 12f)) score += 20f;

                // Drone vert immobile (en train de viser / tirer) : cible facile.
                if (GetTargetVelocity(t).sqrMagnitude < 1f) score += 8f;
                if (score > bestScore) { bestScore = score; bestDrone = t; }
            }
            else if (u is Turret)
            {
                float score = -dist - 0.5f * expected + 10f * System.Math.Min(CountAttackers(t, self.gameObject), maxAttackersBonus);
                if (score > bestTurretScore) { bestTurretScore = score; bestTurret = t; }
            }
        }

        return bestDrone != null ? bestDrone : bestTurret;
    }

    /// <summary>
    /// Mode "assaut des tourelles" : chaque drone choisit une tourelle verte
    /// (la plus faible / la plus proche), au maximum ~4 drones par tourelle
    /// pour répartir l'assaut et éviter un regroupement autour d'une seule.
    /// </summary>
    public static Transform SelectTurretRushTarget(Transform self, string myTag, int maxAttackersPerTurret)
    {
        Vector3 myPos = self.position;
        Transform best = null;
        float bestScore = float.MinValue;

        foreach (ArmyElement u in AllUnits())
        {
            if (!(u is Turret) || u.CompareTag(myTag)) continue;
            Transform t = u.transform;

            float expected = GetExpectedHealth(t);
            if (expected <= 0f) continue;

            int attackers = CountAttackers(t, self.gameObject);
            float score = -FlatDistance(myPos, t.position)
                          - 0.3f * expected
                          + 12f * System.Math.Min(attackers, maxAttackersPerTurret - 1)
                          - 40f * System.Math.Max(0, attackers - (maxAttackersPerTurret - 1));

            if (score > bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    // ---------------------------------------------------------
    // RÔLES : "raiders" (commando anti-tourelles)
    // Au début de chaque bataille, les N drones rouges les plus proches des
    // tourelles vertes sont désignés raiders. Les rôles ne sont pas réattribués
    // quand un raider meurt (on ne vide pas le groupe principal).
    // ---------------------------------------------------------

    static readonly HashSet<GameObject> s_Raiders = new HashSet<GameObject>();
    static float s_RaidersAssignedAt = -1f;
    static int s_RaidersRequested = -1;

    public static bool IsRaider(GameObject drone, string myTag, int raidersCount)
    {
        if (raidersCount <= 0) return false;

        // Nouvelle bataille (rechargement de scène) ou nombre modifié : réattribution
        bool newBattle = s_RaidersAssignedAt < 0f || Time.timeSinceLevelLoad < s_RaidersAssignedAt;
        if (newBattle || s_RaidersRequested != raidersCount)
        {
            AssignRaiders(myTag, raidersCount);
        }
        return s_Raiders.Contains(drone);
    }

    static void AssignRaiders(string myTag, int raidersCount)
    {
        s_Raiders.Clear();
        s_RaidersAssignedAt = Time.timeSinceLevelLoad;
        s_RaidersRequested = raidersCount;

        List<ArmyElement> myDrones = new List<ArmyElement>();
        List<Vector3> enemyTurrets = new List<Vector3>();
        foreach (ArmyElement u in AllUnits())
        {
            if (u is Drone && u.CompareTag(myTag)) myDrones.Add(u);
            else if (u is Turret && !u.CompareTag(myTag)) enemyTurrets.Add(u.transform.position);
        }
        if (enemyTurrets.Count == 0) return;

        myDrones.Sort((a, b) =>
            DistanceToClosest(a.transform.position, enemyTurrets)
                .CompareTo(DistanceToClosest(b.transform.position, enemyTurrets)));

        for (int i = 0; i < myDrones.Count && i < raidersCount; i++)
            s_Raiders.Add(myDrones[i].gameObject);
    }

    static float DistanceToClosest(Vector3 pos, List<Vector3> points)
    {
        float best = float.MaxValue;
        foreach (Vector3 p in points) best = Mathf.Min(best, FlatDistance(pos, p));
        return best;
    }

    public static int CountEnemyTurrets(string myTag)
    {
        int count = 0;
        foreach (ArmyElement u in AllUnits())
        {
            if (u is Turret && !u.CompareTag(myTag)) count++;
        }
        return count;
    }

    // =========================================================
    // TIR ANTICIPÉ (LEAD)
    // =========================================================

    /// <summary>Distance parcourue par un missile après t secondes.</summary>
    public static float MissileDistanceAt(float t, float startSpeed)
    {
        float tAcc = Mathf.Max(0f, (MISSILE_MAX_SPEED - startSpeed) / MISSILE_ACCELERATION);
        if (t <= tAcc) return startSpeed * t + 0.5f * MISSILE_ACCELERATION * t * t;

        float dAcc = startSpeed * tAcc + 0.5f * MISSILE_ACCELERATION * tAcc * tAcc;
        return dAcc + MISSILE_MAX_SPEED * (t - tAcc);
    }

    /// <summary>Temps nécessaire à un missile pour parcourir la distance d.</summary>
    public static float MissileTimeToDistance(float d, float startSpeed)
    {
        float tAcc = Mathf.Max(0f, (MISSILE_MAX_SPEED - startSpeed) / MISSILE_ACCELERATION);
        float dAcc = startSpeed * tAcc + 0.5f * MISSILE_ACCELERATION * tAcc * tAcc;

        if (d <= dAcc)
        {
            // 0.5·a·t² + v0·t - d = 0
            float a = 0.5f * MISSILE_ACCELERATION;
            float disc = startSpeed * startSpeed + 4f * a * d;
            return (-startSpeed + Mathf.Sqrt(disc)) / (2f * a);
        }
        return tAcc + (d - dAcc) / MISSILE_MAX_SPEED;
    }

    public static Vector3 GetTargetVelocity(Transform target)
    {
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled) return agent.velocity;
        return Vector3.zero; // tourelle : statique
    }

    /// <summary>
    /// Point de visée anticipé : position de la cible au moment où le missile l'atteint.
    /// Résolution itérative (4 itérations suffisent largement).
    /// </summary>
    public static Vector3 PredictAimPoint(Vector3 shooterPos, float missileStartSpeed, Transform target)
    {
        Vector3 targetPos = GetAimCenter(target);
        Vector3 velocity = Flat(GetTargetVelocity(target));
        if (velocity.sqrMagnitude < 0.04f) return targetPos;

        Vector3 aim = targetPos;
        for (int i = 0; i < 4; i++)
        {
            float d = FlatDistance(shooterPos, aim);
            float t = Mathf.Min(MissileTimeToDistance(d, missileStartSpeed), MISSILE_LIFE_DURATION);
            aim = targetPos + Vector3.ClampMagnitude(velocity * t, MAX_LEAD_DISTANCE);
        }
        return aim;
    }

    // =========================================================
    // LIGNE DE TIR
    // =========================================================

    static readonly RaycastHit[] s_Hits = new RaycastHit[32];

    class HitDistanceComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
    }
    static readonly HitDistanceComparer s_HitComparer = new HitDistanceComparer();

    /// <summary>
    /// Vrai si un missile tiré depuis "from" peut atteindre la cible :
    /// les unités alliées sont traversées (les missiles ignorent leur propre camp),
    /// toucher un autre ennemi est acceptable, un obstacle / le terrain bloque.
    /// </summary>
    public static bool HasLineOfFire(Vector3 from, Transform target, string myTag)
    {
        Vector3 to = GetAimCenter(target);
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.05f) return true;

        int n = Physics.RaycastNonAlloc(from, dir / dist, s_Hits, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(s_Hits, 0, n, s_HitComparer);

        for (int i = 0; i < n; i++)
        {
            Transform hitT = s_Hits[i].collider.transform;
            if (hitT == target || hitT.IsChildOf(target)) return true;

            IArmyElement element = s_Hits[i].collider.GetComponentInParent<IArmyElement>();
            if (element != null)
            {
                Component c = element as Component;
                if (c != null && c.CompareTag(myTag)) continue; // allié : le missile le traverse
                return true;                                     // autre ennemi : il sera touché
            }

            if (s_Hits[i].collider.CompareTag(myTag)) continue;  // missile allié, etc.
            return false;                                        // obstacle ou terrain
        }
        return true;
    }

    // =========================================================
    // MENACES : GRENADES ENNEMIES EN VOL
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

    static int s_ThreatFrame = -1;
    static string s_ThreatTag;
    static readonly List<GrenadeThreat> s_Threats = new List<GrenadeThreat>();
    static readonly Dictionary<Rocket, RocketSample> s_RocketSamples = new Dictionary<Rocket, RocketSample>();
    static readonly List<Rocket> s_ToRemove = new List<Rocket>();

    /// <summary>Liste des impacts prévus des grenades ennemies (mise à jour 1 fois par frame).</summary>
    public static List<GrenadeThreat> GetEnemyGrenadeThreats(string myTag)
    {
        if (s_ThreatFrame == Time.frameCount && s_ThreatTag == myTag) return s_Threats;

        s_ThreatFrame = Time.frameCount;
        s_ThreatTag = myTag;
        s_Threats.Clear();

        Rocket[] rockets = Object.FindObjectsByType<Rocket>(FindObjectsSortMode.None);
        foreach (Rocket rocket in rockets)
        {
            if (rocket == null || rocket.CompareTag(myTag)) continue;

            Rocket id = rocket;
            Vector3 pos = rocket.transform.position;
            Vector3 velocity = Vector3.zero;
            bool hasVelocity = false;

            // 1) vitesse physique du Rigidbody
            Rigidbody rb = rocket.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic && rb.linearVelocity.sqrMagnitude > 1f)
            {
                velocity = rb.linearVelocity;
                hasVelocity = true;
            }

            // 2) secours : différence finie entre deux observations
            RocketSample sample;
            if (s_RocketSamples.TryGetValue(id, out sample))
            {
                float dt = Time.time - sample.Time;
                if (!hasVelocity && dt > 0.0001f)
                {
                    velocity = (pos - sample.Position) / dt;
                    hasVelocity = velocity.sqrMagnitude > 1f;
                }
            }
            else
            {
                sample = new RocketSample();
                s_RocketSamples[id] = sample;
            }
            sample.Position = pos;
            sample.Time = Time.time;
            sample.Frame = Time.frameCount;

            if (!hasVelocity) continue;

            Vector3 impact;
            float t;
            if (PredictGrenadeImpact(pos, velocity, out impact, out t))
            {
                s_Threats.Add(new GrenadeThreat { Impact = impact, TimeToImpact = t });
            }
        }

        // Nettoyage des grenades disparues
        s_ToRemove.Clear();
        foreach (KeyValuePair<Rocket, RocketSample> kv in s_RocketSamples)
        {
            if (kv.Key == null || Time.frameCount - kv.Value.Frame > 30) s_ToRemove.Add(kv.Key);
        }
        foreach (Rocket r in s_ToRemove) s_RocketSamples.Remove(r);

        return s_Threats;
    }

    /// <summary>
    /// Résout p.y + v.y·t + ½·g·t² = hauteur du sol (+ pivot drone) au point d'arrivée.
    /// 3 itérations pour tenir compte des terrasses.
    /// </summary>
    public static bool PredictGrenadeImpact(Vector3 p, Vector3 v, out Vector3 impact, out float time)
    {
        impact = p;
        time = 0f;

        float g = Physics.gravity.y;
        if (g > -0.01f) return false;

        float groundY = GroundHeight(p, p.y - 10f);

        for (int i = 0; i < 3; i++)
        {
            float yEnd = groundY + DRONE_PIVOT_HEIGHT;
            float a = 0.5f * g;
            float b = v.y;
            float c = p.y - yEnd;
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;

            float t = (-b - Mathf.Sqrt(disc)) / (2f * a); // racine positive (a < 0)
            if (t < 0f) return false;

            time = t;
            impact = new Vector3(p.x + v.x * t, yEnd, p.z + v.z * t);
            groundY = GroundHeight(impact, groundY);
        }
        return true;
    }

    static float GroundHeight(Vector3 pos, float fallback)
    {
        if (TerrainManager.Instance == null) return fallback;
        Vector3 terrainPos = Vector3.zero;
        Vector3 normal = Vector3.zero;
        if (TerrainManager.Instance.GetVerticallyAlignedPositionOnTerrain(pos, ref terrainPos, ref normal))
            return terrainPos.y;
        return fallback;
    }

    /// <summary>Vrai si "pos" est dans la zone d'explosion d'une grenade qui tombe dans moins de maxTime secondes.</summary>
    public static bool IsInGrenadeDanger(Vector3 pos, string myTag, float dangerRadius, float maxTime)
    {
        foreach (GrenadeThreat threat in GetEnemyGrenadeThreats(myTag))
        {
            if (threat.TimeToImpact > maxTime) continue;
            if (Mathf.Abs(threat.Impact.y - pos.y) > GRENADE_DAMAGE_RADIUS + 1f) continue;
            if (FlatDistance(pos, threat.Impact) < dangerRadius) return true;
        }
        return false;
    }

    // =========================================================
    // ESPACEMENT DES DRONES ROUGES (anti dégâts de zone)
    // =========================================================

    const float RESERVATION_LIFETIME = 6f;

    static readonly Dictionary<GameObject, Vector3> s_Reservations = new Dictionary<GameObject, Vector3>();
    static readonly Dictionary<GameObject, float> s_ReservationTimes = new Dictionary<GameObject, float>();

    public static void ReserveSlot(GameObject droneId, Vector3 slot)
    {
        s_Reservations[droneId] = slot;
        s_ReservationTimes[droneId] = Time.time;
    }

    public static void ReleaseSlot(GameObject droneId)
    {
        s_Reservations.Remove(droneId);
        s_ReservationTimes.Remove(droneId);
    }

    /// <summary>
    /// Pénalité de proximité avec les autres drones rouges
    /// (positions actuelles ET destinations réservées).
    /// </summary>
    public static float AllyCrowdPenalty(Vector3 pos, GameObject myId, string myTag, float spacing)
    {
        float penalty = 0f;

        foreach (ArmyElement u in AllUnits())
        {
            if (!(u is Drone) || !u.CompareTag(myTag)) continue;
            if (u.gameObject == myId) continue;

            float d = FlatDistance(pos, u.transform.position);
            if (d < spacing) penalty += 1f - d / spacing;
        }

        foreach (KeyValuePair<GameObject, Vector3> kv in s_Reservations)
        {
            if (kv.Key == null || kv.Key == myId) continue;
            float time;
            if (!s_ReservationTimes.TryGetValue(kv.Key, out time) || Time.time - time > RESERVATION_LIFETIME) continue;

            float d = FlatDistance(pos, kv.Value);
            if (d < spacing) penalty += 1f - d / spacing;
        }
        return penalty;
    }

    /// <summary>Nombre de drones ennemis proches d'une position (hors cible).</summary>
    public static int EnemyDronesNear(Vector3 pos, string myTag, float radius, Transform ignore)
    {
        int count = 0;
        foreach (ArmyElement u in AllUnits())
        {
            if (!(u is Drone) || u.CompareTag(myTag) || u.transform == ignore) continue;
            if (FlatDistance(pos, u.transform.position) < radius) count++;
        }
        return count;
    }

    // =========================================================
    // CHOIX D'UNE POSITION DE TIR
    // =========================================================

    static readonly float[] s_RadiusFactors = { 1f, 0.75f, 1.3f };
    const int SLOT_ANGLES = 12;

    /// <summary>
    /// Cherche, sur des anneaux autour de la cible, la meilleure position de tir :
    /// proche du drone, espacée des autres drones rouges, hors zone d'impact,
    /// avec ligne de tir dégagée et loin des groupes de drones verts.
    /// </summary>
    public static bool FindFiringSlot(
        Transform self, GameObject myId, string myTag, Transform target,
        float preferredRange, float allySpacing, float pivotHeight,
        List<Vector3> rejectedSlots, out Vector3 bestSlot)
    {
        bestSlot = Vector3.zero;
        float bestScore = float.MinValue;

        Vector3 myPos = self.position;
        Vector3 targetPos = target.position;

        Vector3 baseDir = Flat(myPos - targetPos);
        baseDir = baseDir.sqrMagnitude > 0.01f ? baseDir.normalized : Flat(self.forward).normalized;

        foreach (float factor in s_RadiusFactors)
        {
            float radius = Mathf.Max(2.5f, preferredRange * factor);

            for (int k = 0; k < SLOT_ANGLES; k++)
            {
                float angle = (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * (360f / SLOT_ANGLES);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                Vector3 candidate = targetPos + dir * radius;

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;

                if (IsRejected(p, rejectedSlots)) continue;

                float score = 0f;

                // 1. Coût du déplacement
                score -= FlatDistance(myPos, p);

                // 2. Espacement avec les autres drones rouges
                score -= 35f * AllyCrowdPenalty(p, myId, myTag, allySpacing);

                // 3. Éviter les groupes de drones verts
                score -= 8f * EnemyDronesNear(p, myTag, 6f, target);

                // 4. Éviter les impacts de grenades prévus
                if (IsInGrenadeDanger(p, myTag, GRENADE_DAMAGE_RADIUS + 2f, 10f)) score -= 100f;

                // 5. Ligne de tir dégagée depuis cette position
                if (!HasLineOfFire(p + Vector3.up * pivotHeight, target, myTag)) score -= 40f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = p;
                }
            }
        }

        return bestScore > float.MinValue;
    }

    /// <summary>
    /// Point de "strafe" pendant le temps de recharge : déplacement latéral
    /// (perpendiculaire à la ligne de tir) pour ne pas offrir une cible immobile
    /// aux missiles verts, qui visent la position actuelle sans anticipation.
    /// </summary>
    public static bool FindStrafePoint(
        Transform self, GameObject myId, string myTag, Transform target,
        float minRange, float maxRange, float pivotHeight, float preferredSide,
        out Vector3 bestPoint, out float chosenSide)
    {
        bestPoint = self.position;
        chosenSide = preferredSide;
        Vector3 myPos = self.position;

        Vector3 toTarget = target != null ? Flat(target.position - myPos) : Flat(self.forward);
        toTarget = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, toTarget);

        float bestScore = float.MinValue;
        float[] distances = { 4f, 6.5f };

        foreach (float dist in distances)
        {
            for (int k = 0; k < 12; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 30f, 0f) * toTarget;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(myPos + dir * dist, out hit, 2f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;

                float score = 0f;

                // Latéral plutôt qu'avant / arrière
                score += 6f * (1f - Mathf.Abs(Vector3.Dot(dir, toTarget)));

                // Alterner les côtés (trajectoire imprévisible)
                float side = Vector3.Dot(dir, right) >= 0f ? 1f : -1f;
                if (side == preferredSide) score += 3f;

                if (target != null)
                {
                    float dTarget = FlatDistance(p, target.position);
                    if (dTarget < minRange) score -= 3f * (minRange - dTarget);
                    if (dTarget > maxRange) score -= 3f * (dTarget - maxRange);
                    if (!HasLineOfFire(p + Vector3.up * pivotHeight, target, myTag)) score -= 20f;
                }

                score -= 30f * AllyCrowdPenalty(p, myId, myTag, 6f);
                score -= 6f * EnemyDronesNear(p, myTag, 6f, target);
                if (IsInGrenadeDanger(p, myTag, GRENADE_DAMAGE_RADIUS + 2f, 10f)) score -= 100f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = p;
                    chosenSide = side;
                }
            }
        }
        return bestScore > float.MinValue;
    }

    static bool IsRejected(Vector3 p, List<Vector3> rejected)
    {
        if (rejected == null) return false;
        foreach (Vector3 r in rejected)
        {
            if (FlatDistance(p, r) < 1.5f) return true;
        }
        return false;
    }

    /// <summary>
    /// Point de fuite hors de toutes les zones d'impact prévues,
    /// en restant si possible à distance de tir de la cible.
    /// </summary>
    public static bool FindEscapePoint(
        Transform self, GameObject myId, string myTag, Transform currentTarget,
        float dangerRadius, out Vector3 escapePoint)
    {
        escapePoint = self.position;
        Vector3 myPos = self.position;
        List<GrenadeThreat> threats = GetEnemyGrenadeThreats(myTag);

        // Direction de fuite moyenne (pondérée par l'urgence)
        Vector3 away = Vector3.zero;
        foreach (GrenadeThreat threat in threats)
        {
            Vector3 d = Flat(myPos - threat.Impact);
            float dist = d.magnitude;
            if (dist > dangerRadius + 3f) continue;
            float urgency = 1f / Mathf.Max(0.2f, threat.TimeToImpact);
            away += (dist > 0.01f ? d / dist : Flat(self.right)) * urgency;
        }
        if (away.sqrMagnitude < 0.0001f) away = Flat(self.right);
        away.Normalize();

        float bestScore = float.MinValue;
        float[] distances = { dangerRadius + 1.5f, dangerRadius + 4f };

        foreach (float dist in distances)
        {
            for (int k = 0; k < 16; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 22.5f, 0f) * away;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(myPos + dir * dist, out hit, 2f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;

                float score = 0f;
                if (IsInGrenadeDanger(p, myTag, dangerRadius, 10f)) score -= 200f;
                score += 10f * Vector3.Dot(dir, away);
                score -= 0.5f * FlatDistance(myPos, p);
                score -= 25f * AllyCrowdPenalty(p, myId, myTag, 6f);

                if (currentTarget != null)
                {
                    float dTarget = FlatDistance(p, currentTarget.position);
                    if (dTarget > 25f) score -= (dTarget - 25f);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    escapePoint = p;
                }
            }
        }
        return bestScore > float.MinValue;
    }
}
