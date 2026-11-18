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

    [SerializeField] float m_DroneFinishOffHealthRatio = 0.5f;
    [SerializeField] float m_DroneFinishOffAbsHealth = 5f;

    // =========================================================
    // PENDING DAMAGE (在途伤害追踪)
    // =========================================================
    class PendingShot
    {
        public IArmyElement Target;
        public float Damage;
        public float ExpireTime;
    }
    readonly List<PendingShot> m_PendingShots = new List<PendingShot>();

    public void RegisterPendingArtilleryShot(GameObject target)
    {
        RegisterPendingShot(target, ARTILLERY_DAMAGE, PENDING_SHOT_LIFETIME);
    }

    public void RegisterPendingMissileShot(GameObject target, int missileCount = 2)
    {
        if (target == null) return;
        int count = Mathf.Max(1, missileCount);
        for (int i = 0; i < count; i++)
            RegisterPendingShot(target, MISSILE_DAMAGE, MISSILE_LIFETIME);
    }

    void RegisterPendingShot(GameObject target, float damage, float lifetime)
    {
        if (target == null) return;
        IArmyElement element = target.GetComponent<IArmyElement>();
        if (element == null) return;

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
        m_PendingShots.RemoveAll(shot => shot.Target == null || Time.time >= shot.ExpireTime);
    }

    float GetPendingDamage(IArmyElement target)
    {
        CleanupPendingShots();
        return m_PendingShots.Where(shot => shot.Target == target).Sum(shot => shot.Damage);
    }

    float GetExpectedRemainingHealth(IArmyElement target)
    {
        return target.Health - GetPendingDamage(target);
    }

    // =========================================================
    // UNIFIED TARGET REQUEST
    // =========================================================
    public enum RequesterType { Drone, Turret, Medic }

    public GameObject RequestTarget(
        RequesterType requesterType,
        Vector3 requesterPos,
        float minRadius,
        float maxRadius)
    {
        switch (requesterType)
        {
            case RequesterType.Turret:
                return GetBestTargetForRedTurret(null, requesterPos, minRadius, maxRadius);
            case RequesterType.Drone:
                return GetBestTargetForRedDrone(requesterPos, minRadius, maxRadius);
            case RequesterType.Medic:
                return GetBestHealTarget(requesterPos, minRadius, maxRadius);
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

        // 优先级1：敌方炮塔
        List<Turret> enemyTurretsInRange = GetAllEnemiesOfType<Turret>(false)
            .Where(turret =>
            {
                float distance = Vector3.Distance(turretPosition, turret.transform.position);
                return distance > minRadius && distance < maxRadius;
            }).ToList();

        Turret bestEnemyTurret = enemyTurretsInRange
            .Where(turret => GetExpectedRemainingHealth(turret) > 0f)
            .OrderBy(turret => GetExpectedRemainingHealth(turret))
            .ThenBy(turret => Vector3.Distance(turretPosition, turret.transform.position))
            .FirstOrDefault();

        if (bestEnemyTurret != null) return bestEnemyTurret.gameObject;

        // 优先级2：敌方无人机
        List<Drone> enemyDronesInRange = GetAllEnemiesOfType<Drone>(false)
            .Where(drone =>
            {
                float distance = Vector3.Distance(turretPosition, drone.transform.position);
                return distance > minRadius && distance < maxRadius;
            }).ToList();

        // 优先级2A：慢速无人机
        List<Drone> slowDrones = enemyDronesInRange
            .Where(drone =>
            {
                NavMeshAgent agent = drone.GetComponent<NavMeshAgent>();
                if (agent == null) return false;
                return agent.velocity.magnitude <= 1.0f;
            }).ToList();

        if (slowDrones.Count > 0)
        {
            Drone nearestSlowDrone = slowDrones
                .OrderBy(drone => Vector3.Distance(turretPosition, drone.transform.position))
                .FirstOrDefault();
            if (nearestSlowDrone != null) return nearestSlowDrone.gameObject;
        }

        // 优先级3：最近的无人机
        Drone fallbackDrone = enemyDronesInRange
            .OrderBy(drone => Vector3.Distance(turretPosition, drone.transform.position))
            .FirstOrDefault();

        if (fallbackDrone != null) return fallbackDrone.gameObject;

        return null;
    }

    // =========================================================
    // RED DRONE TARGET SELECTION
    // =========================================================
    GameObject GetBestTargetForRedDrone(Vector3 dronePos, float minRadius, float maxRadius)
    {
        CleanupPendingShots();

        // 优先级1：收尾残血炮塔
        Turret finishableTurret = GetAllEnemiesOfType<Turret>(false)
            .Where(turret =>
            {
                float distance = Vector3.Distance(dronePos, turret.transform.position);
                return distance > minRadius && distance < maxRadius;
            })
            .Where(turret =>
            {
                float expected = GetExpectedRemainingHealth(turret);
                return expected > 0f &&
                       (expected <= turret.Health * m_DroneFinishOffHealthRatio ||
                        expected <= m_DroneFinishOffAbsHealth);
            })
            .OrderBy(turret => GetExpectedRemainingHealth(turret))
            .ThenBy(turret => Vector3.Distance(dronePos, turret.transform.position))
            .FirstOrDefault();

        if (finishableTurret != null) return finishableTurret.gameObject;

        // 优先级2：返回最近的敌机（后续 Ruofan 的 RedDroneTactics 会通过队友计数防扎堆）
        var enemyDrones = GetAllEnemiesOfType<ArmyElement>(false)
            .Where(d => d.Health > 0 && !d.CompareTag(gameObject.tag))
            .OrderBy(d => Vector3.Distance(dronePos, d.transform.position))
            .FirstOrDefault();

        if (enemyDrones != null) return enemyDrones.gameObject;

        return null;
    }

    // =========================================================
    // MEDIC TARGET SELECTION
    // =========================================================
    GameObject GetBestHealTarget(Vector3 medicPos, float minRadius, float maxRadius)
    {
        IArmyElement best = null;
        float bestHealth = float.MaxValue;

        foreach (IArmyElement ally in m_ArmyElements)
        {
            if (ally == null) continue;
            if (ally.Health <= 0f) continue; 
            
            Component allyComponent = ally as Component;
            if (allyComponent == null) continue;
            if (allyComponent.gameObject == gameObject) continue;

            // 获取最大血量（假设都是50），如果低于最大血量，才需要治疗
            Health healthComp = allyComponent.GetComponent<Health>();
            if (healthComp != null && ally.Health >= healthComp.MaxValue) continue;

            float distance = Vector3.Distance(medicPos, allyComponent.transform.position);
            if (distance <= minRadius || distance >= maxRadius) continue;

            if (ally.Health < bestHealth)
            {
                bestHealth = ally.Health;
                best = ally;
            }
        }

        if (best == null) return null;

        Component bestComponent = best as Component;
        return bestComponent != null ? bestComponent.gameObject : null;
    }

    // =========================================================
    // HELPER: GET ALLIES
    // =========================================================
    public List<T> GetAllAlliesOfType<T>(bool includeDead = false) where T : Component
    {
        List<T> allies = new List<T>();
        foreach (var element in m_ArmyElements)
        {
            if (element == null) continue;
            if (!includeDead && element.Health <= 0f) continue;
            if (element is T comp) allies.Add(comp);
        }
        return allies;
    }

    // =========================================================
    // BATTLE END
    // =========================================================
    public override void ArmyElementHasBeenKilled(GameObject go)
    {
        base.ArmyElementHasBeenKilled(go);

        if (m_ArmyElements.Count == 0)
        {
            GUIUtility.systemCopyBuffer = "0\t" + ((int)Timer.Value).ToString() + "\t0\t0\t0";
            BattleStatsManager.Instance?.PrintBattleResult("2");
        }
    }

    public void GreenArmyIsDead(string deadArmyTag)
    {
        int nDrones = 0, nTurrets = 0, health = 0;
        ComputeStatistics(ref nDrones, ref nTurrets, ref health);

        GUIUtility.systemCopyBuffer = "1\t" + ((int)Timer.Value).ToString() + "\t" +
                                      nDrones.ToString() + "\t" + nTurrets.ToString() + "\t" + health.ToString();

        RefreshHudDisplay();
        BattleStatsManager.Instance?.PrintBattleResult("1");
    }
}