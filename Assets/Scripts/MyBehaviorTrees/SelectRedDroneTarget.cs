using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("MyTasks")]
[TaskDescription(
    "Selects the best tactical target for a RED drone."
)]
public class SelectRedDroneTarget : Action
{
    IArmyElement m_ArmyElement;

    public SharedTransform target;
    public SharedFloat minRadius = 5f;
    public SharedFloat maxRadius = 50f;

    // [ADDED] Same cooldown idea as the turret version.
    public SharedFloat retargetCooldown = 2.0f;

    float m_NextRetargetTime = 0f;


    public override void OnAwake()
    {
        m_ArmyElement =
            (IArmyElement)GetComponent(typeof(IArmyElement));
    }


    public override TaskStatus OnUpdate()
    {
        if (m_ArmyElement.ArmyManager == null)
        {
            return TaskStatus.Running;
        }

        ArmyManagerRed manager =
            m_ArmyElement.ArmyManager as ArmyManagerRed;

        if (manager == null)
        {
            return TaskStatus.Failure;
        }

        if (IsCurrentTargetStillValid() &&
            Time.time < m_NextRetargetTime)
        {
            return TaskStatus.Success;
        }

        GameObject selected =
            manager.RequestTarget(
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
        if (go == null) return false;
        if (!go.activeInHierarchy) return false;

        IArmyElement element = go.GetComponent<IArmyElement>();
        if (element == null) return false;

        return element.Health > 0f;
    }
}