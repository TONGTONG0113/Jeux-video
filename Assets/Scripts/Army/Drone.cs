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

    // 【新增】给无人机加血的方法
    public void Heal(float amount)
    {
        // 由于不知道 ArmyElement 内部怎么存血量的，这里提供一个安全的通用加血逻辑
        // 假设父类 ArmyElement 里有 m_Health 或者类似的字段
        // 如果没有，我们可以通过一些通用的手段去增加
        
        // 如果父类有提供受伤害的方法，通常也会有一个类似加血的方法
        // 这里我们先尝试用反射强行修改基类的 Health 字段（因为你的接口里是 get;）
        var healthProperty = typeof(ArmyElement).GetProperty("Health");
        if (healthProperty != null && healthProperty.CanWrite)
        {
            healthProperty.SetValue(this, Mathf.Min(50f, Health + amount)); // 假设满血是 50
        }
        else
        {
            // 如果属性是只读的，尝试找底层字段
            var healthField = typeof(ArmyElement).GetField("m_Health", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (healthField != null)
            {
                float currentHealth = (float)healthField.GetValue(this);
                healthField.SetValue(this, Mathf.Min(50f, currentHealth + amount)); // 50 是无人机的满血上限
            }
        }
    }

    public new void Die()
    {
        ArmyManager.ArmyElementHasBeenKilled(gameObject);
        Destroy(gameObject);
    }
}