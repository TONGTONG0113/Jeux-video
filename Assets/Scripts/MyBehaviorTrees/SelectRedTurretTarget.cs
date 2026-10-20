using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("MyTasks")]
[TaskDescription(
    "Selects the best tactical target for a RED artillery turret."
)]
public class SelectRedTurretTarget : Action
{
    IArmyElement m_ArmyElement;

    public SharedTransform target;
    public SharedFloat minRadius;
    public SharedFloat maxRadius;


    public override void OnAwake()
    {
        m_ArmyElement =
            (IArmyElement)GetComponent(
                typeof(IArmyElement)
            );
    }


    public override TaskStatus OnUpdate()
    {
        // ArmyManager has not been injected yet.
        if (m_ArmyElement.ArmyManager == null)
        {
            return TaskStatus.Running;
        }


        ArmyManagerRed armyManagerRed =
            m_ArmyElement.ArmyManager as ArmyManagerRed;


        if (armyManagerRed == null)
        {
            return TaskStatus.Failure;
        }


        GameObject selectedTarget =
            armyManagerRed.GetBestTargetForRedTurret(
                gameObject,
                transform.position,
                minRadius.Value,
                maxRadius.Value
            );


        if (selectedTarget == null)
        {
            target.Value = null;
            return TaskStatus.Failure;
        }


        target.Value =
            selectedTarget.transform;

        return TaskStatus.Success;
    }
}