using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Drone : ArmyElement, IShoot
{
    [SerializeField] GameObject m_MissilePrefab;
    [SerializeField] Transform[] m_MissileSpawnPos;
    NavMeshAgent m_NavMeshAgent;

    Transform m_Transform;

    private void Awake()
    {
        m_Transform = transform;
        m_NavMeshAgent = GetComponent<NavMeshAgent>();
    }

    public void Shoot()
    {
        BattleStatsManager.Instance?
            .RegisterDroneShots(
                gameObject.tag,
                m_MissileSpawnPos.Length
            );

        for (int i = 0; i < m_MissileSpawnPos.Length; i++)
        {
            Transform missileSpawnPos = m_MissileSpawnPos[i];
            GameObject newMissileGO = Instantiate(m_MissilePrefab, missileSpawnPos.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized));
            newMissileGO.tag = gameObject.tag;
            Missile missile = newMissileGO.GetComponent<Missile>();
            missile.SetStartSpeed(m_NavMeshAgent.speed);
        }
    }

    // =========================================================
    // [RED - Ruofan] Tir convergent
    // Shoot() (utilisé par l'armée verte) n'est PAS modifié.
    // =========================================================

    public int MissileCount => m_MissileSpawnPos != null ? m_MissileSpawnPos.Length : 0;

    /// <summary>
    /// Comme Shoot(), mais chaque missile est orienté depuis son propre point de
    /// lancement vers aimPoint (missiles convergents au lieu de parallèles).
    /// La trajectoire reste rectiligne et la correction est bornée à
    /// maxCorrectionAngle degrés autour de l'axe du drone : le drone doit
    /// toujours s'orienter vers sa cible avant de tirer.
    /// </summary>
    public void ShootAt(Vector3 aimPoint, float maxCorrectionAngle)
    {
        BattleStatsManager.Instance?
            .RegisterDroneShots(
                gameObject.tag,
                m_MissileSpawnPos.Length
            );

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float maxRad = Mathf.Max(0f, maxCorrectionAngle) * Mathf.Deg2Rad;

        for (int i = 0; i < m_MissileSpawnPos.Length; i++)
        {
            Transform missileSpawnPos = m_MissileSpawnPos[i];

            Vector3 dir = Vector3.ProjectOnPlane(aimPoint - missileSpawnPos.position, Vector3.up);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : forward;
            dir = Vector3.RotateTowards(forward, dir, maxRad, 0f);

            GameObject newMissileGO = Instantiate(m_MissilePrefab, missileSpawnPos.position, Quaternion.LookRotation(dir));
            newMissileGO.tag = gameObject.tag;
            Missile missile = newMissileGO.GetComponent<Missile>();
            missile.SetStartSpeed(m_NavMeshAgent.speed);
        }
    }

    public void Heal(float amount)
    {
        UnitHealth.Heal(amount);
    }

    public new void Die()
    {
        ArmyManager.ArmyElementHasBeenKilled(gameObject);
        Destroy(gameObject);
    }
}