using UnityEngine;
using UnityEngine.AI;

public class Medic : ArmyElement
{
    [Header("Medic Settings")]
    public float HealAmount = 5f;        // 每秒治疗量
    public float HealRadius = 5f;        // 治疗半径
    public float MaxHealth = 100f;       // 医疗兵自身血量

    private float m_CurrentHealth;
    private NavMeshAgent m_Agent;

    public new float Health => m_CurrentHealth;

    // 【修改】去掉 override，直接写 Awake
    private void Awake()
    {
        m_Agent = GetComponent<NavMeshAgent>();
        m_CurrentHealth = MaxHealth;
    }

    private void Update()
    {
        if (m_CurrentHealth <= 0) return;

        // 扫描周围 5 米内的友军
        Collider[] colliders = Physics.OverlapSphere(transform.position, HealRadius);
        foreach (var col in colliders)
        {
            IArmyElement ally = col.GetComponent<IArmyElement>();
            
            // 确保它是红方单位，没死，且缺血（血量低于 50）
            if (ally != null && ally.ArmyManager is ArmyManagerRed && ally.Health > 0f && ally.Health < 50f)
            {
                Drone drone = col.GetComponent<Drone>();
                if (drone != null)
                {
                    drone.Heal(HealAmount * Time.deltaTime);
                }
            }
        }
    }

    public void TakeDamage(float amount)
    {
        m_CurrentHealth -= amount;
        if (m_CurrentHealth <= 0)
        {
            m_CurrentHealth = 0;
            Die();
        }
    }

    // 【修改】加上 new 关键字，消除警告
    public new void Die()
    {
        ArmyManager?.ArmyElementHasBeenKilled(gameObject);
        Destroy(gameObject);
    }
}