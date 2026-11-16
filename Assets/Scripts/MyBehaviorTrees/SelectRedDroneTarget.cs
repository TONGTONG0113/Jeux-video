using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine.AI;

[TaskCategory("MyTasks")]
[TaskDescription("Selects the best tactical target for a RED drone. Also handles retreat logic.")]
public class SelectRedDroneTarget : Action
{
    IArmyElement m_ArmyElement;
    NavMeshAgent m_Agent;

    public SharedTransform target;
    public SharedFloat minRadius = 5f;
    public SharedFloat maxRadius = 50f;
    public SharedFloat retargetCooldown = 2.0f;

    // 【新增】撤退坐标，在行为树面板里把红方大后方的一个坐标填进来
    public SharedVector3 retreatPosition; 

    float m_NextRetargetTime = 0f;
    Vector3 m_LastPosition;
    float m_LastCheckTime;
    float m_StuckThreshold = 0.5f;

    public override void OnAwake()
    {
        m_ArmyElement = (IArmyElement)GetComponent(typeof(IArmyElement));
        m_Agent = GetComponent<NavMeshAgent>();
        m_LastPosition = transform.position;
        m_LastCheckTime = Time.time;
    }

    public override TaskStatus OnUpdate()
    {
        if (m_ArmyElement.ArmyManager == null) return TaskStatus.Running;

        ArmyManagerRed manager = m_ArmyElement.ArmyManager as ArmyManagerRed;
        if (manager == null) return TaskStatus.Failure;

        // 【逻辑】残血撤退
        // 假设无人机满血50，低于 10 血开始撤退
        if (m_ArmyElement.Health < 10f && retreatPosition != null && m_Agent != null)
        {
            target.Value = null; // 清空目标，不打了
            m_Agent.SetDestination(retreatPosition.Value);
            
            // 如果已经退到了撤退点，就不管了，让行为树其他节点处理（比如去回血）
            if (Vector3.Distance(transform.position, retreatPosition.Value) < 3f)
            {
                return TaskStatus.Failure; 
            }
            return TaskStatus.Running;
        }

        // 卡住检测
        if (Time.time - m_LastCheckTime > 1.0f)
        {
            float movedDistance = Vector3.Distance(transform.position, m_LastPosition);
            if (movedDistance < m_StuckThreshold && target.Value != null)
            {
                target.Value = null;
                m_NextRetargetTime = 0f;
            }
            m_LastPosition = transform.position;
            m_LastCheckTime = Time.time;
        }

        if (IsCurrentTargetStillValid() && Time.time < m_NextRetargetTime)
        {
            return TaskStatus.Success;
        }

        GameObject selected = manager.RequestTarget(
            ArmyManagerRed.RequesterType.Drone,
            transform.position,
            minRadius.Value,
            maxRadius.Value);

        if (selected == null)
        {
            target.Value = null;
            return TaskStatus.Failure;
        }

        target.Value = selected.transform;
        m_NextRetargetTime = Time.time + retargetCooldown.Value;
        return TaskStatus.Success;
    }

    bool IsCurrentTargetStillValid()
    {
        if (target.Value == null) return false;
        GameObject go = target.Value.gameObject;
        if (go == null || !go.activeInHierarchy) return false;
        IArmyElement element = go.GetComponent<IArmyElement>();
        return element != null && element.Health > 0f;
    }
}