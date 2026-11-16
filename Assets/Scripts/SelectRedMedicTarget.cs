using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine.AI;

[TaskCategory("MyTasks")]
[TaskDescription("Selects a hurt ally for the RED Medic to heal.")]
public class SelectRedMedicTarget : Action
{
    IArmyElement m_ArmyElement;
    NavMeshAgent m_Agent;

    public SharedTransform healTarget;

    public override void OnAwake()
    {
        // 【修改点 1】不能直接 GetComponent<IArmyElement>()，改为获取 Medic 组件再转换
        m_ArmyElement = GetComponent<Medic>() as IArmyElement;
        m_Agent = GetComponent<NavMeshAgent>();
    }

    public override TaskStatus OnUpdate()
    {
        if (m_ArmyElement == null || m_ArmyElement.ArmyManager == null) 
            return TaskStatus.Running;

        ArmyManagerRed manager = m_ArmyElement.ArmyManager as ArmyManagerRed;
        if (manager == null) return TaskStatus.Failure;

        if (IsCurrentTargetStillValid())
        {
            if (m_Agent != null)
            {
                m_Agent.SetDestination(healTarget.Value.position);
            }
            return TaskStatus.Running;
        }

        GameObject selected = manager.RequestTarget(
            ArmyManagerRed.RequesterType.Medic,
            transform.position,
            0f,   
            100f  
        );

        if (selected == null)
        {
            healTarget.Value = null;
            return TaskStatus.Failure; 
        }

        healTarget.Value = selected.transform;
        return TaskStatus.Success;
    }

    bool IsCurrentTargetStillValid()
    {
        if (healTarget.Value == null) return false;
        GameObject go = healTarget.Value.gameObject;
        if (go == null || !go.activeInHierarchy) return false;
        
        // 【修改点 2】获取接口同样不能直接 GetComponent<IArmyElement>()
        IArmyElement element = go.GetComponent<Drone>() as IArmyElement;
        
        // 如果目标不是无人机（比如可能是炮塔），那也要能识别。保险起见，我们直接用你项目里的 ArmyElement 父类来找。
        if (element == null)
        {
            element = go.GetComponent<ArmyElement>() as IArmyElement;
        }

        return element != null && element.Health > 0f && element.Health < 50f;
    }
}