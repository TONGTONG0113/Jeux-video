using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("MyTasks")]
[TaskDescription(
    "RED artillery fires and registers its incoming damage."
)]
public class RedTurretShoot : Action
{
    public SharedTransform target;

    Turret m_Turret;
    IArmyElement m_ArmyElement;


    public override void OnAwake()
    {
        m_Turret =
            GetComponent<Turret>();


        m_ArmyElement =
            (IArmyElement)GetComponent(
                typeof(IArmyElement)
            );
    }


    public override TaskStatus OnUpdate()
    {
        if (m_Turret == null)
        {
            return TaskStatus.Failure;
        }

        // [ADDED] Re-check target validity: between SelectRedTurretTarget
        // and RedTurretShoot, the target may have been destroyed.
        if (target.Value == null)
        {
            return TaskStatus.Failure;
        }

        IArmyElement targetElement =
            target.Value.GetComponent<IArmyElement>();

        if (targetElement == null || targetElement.Health <= 0f)
        {
            return TaskStatus.Failure;
        }


        // Register the shell only when it is actually about to be fired.
        if (
            m_ArmyElement != null &&
            m_ArmyElement.ArmyManager != null
        )
        {
            ArmyManagerRed manager =
                m_ArmyElement.ArmyManager
                    as ArmyManagerRed;


            if (manager != null)
            {
                manager
                    .RegisterPendingArtilleryShot(
                        target.Value.gameObject
                    );
            }
        }


        // Same shooting behaviour as the original TurretShoot task.
        m_Turret.Shoot(
            target.Value.position
        );


        return TaskStatus.Success;
    }
}