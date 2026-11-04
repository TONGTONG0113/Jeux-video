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

    // [ADDED] Cooldown to prevent the turret from switching target
    // every frame. The turret keeps its current target until this
    // cooldown expires, unless the target becomes invalid.
    public SharedFloat retargetCooldown = 1.0f;

    float m_NextRetargetTime = 0f;


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


        // [ADDED] If we already have a valid target and the cooldown
        // has not expired yet, keep it. This prevents the turret from
        // oscillating between several targets every frame.
        if (IsCurrentTargetStillValid() &&
            Time.time < m_NextRetargetTime)
        {
            return TaskStatus.Success;
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

        // [ADDED] Schedule next retarget.
        m_NextRetargetTime =
            Time.time + retargetCooldown.Value;

        return TaskStatus.Success;
    }


    // [ADDED] A target is valid if it still exists, is active,
    // and has not been destroyed.
    bool IsCurrentTargetStillValid()
    {
        if (target.Value == null)
        {
            return false;
        }

        GameObject go = target.Value.gameObject;

        if (go == null)
        {
            return false;
        }

        if (!go.activeInHierarchy)
        {
            return false;
        }

        IArmyElement element = go.GetComponent<IArmyElement>();

        if (element == null)
        {
            return false;
        }

        return element.Health > 0f;
    }
}