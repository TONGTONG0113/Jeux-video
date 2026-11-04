using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("MyTasks")]
[TaskDescription(
    "RED drone fires and registers its incoming missile damage."
)]
public class RedDroneShoot : Action
{
    public SharedTransform target;

    Drone m_Drone;
    IArmyElement m_ArmyElement;
    int m_MissileCount = 2;


    public override void OnAwake()
    {
        m_Drone = GetComponent<Drone>();
        m_ArmyElement =
            (IArmyElement)GetComponent(typeof(IArmyElement));

        // [ADDED] Read missile count from the Drone's spawn points.
        // If Ruofan prefers, he can hard-code 2 instead.
        // Since m_MissileSpawnPos is private, we use reflection-free
        // fallback: count children of the Drone that are named "MissileSpawn"
        // OR just hard-code 2 based on the project spec.
        m_MissileCount = 2;
    }


    public override TaskStatus OnUpdate()
    {
        if (m_Drone == null || target.Value == null)
        {
            return TaskStatus.Failure;
        }

        IArmyElement targetElement =
            target.Value.GetComponent<IArmyElement>();

        if (targetElement == null || targetElement.Health <= 0f)
        {
            return TaskStatus.Failure;
        }

        // [ADDED] Register the missiles BEFORE firing, so that
        // overkill estimation is up to date for the next frame.
        if (m_ArmyElement != null &&
            m_ArmyElement.ArmyManager is ArmyManagerRed manager)
        {
            manager.RegisterPendingMissileShot(
                target.Value.gameObject,
                m_MissileCount);
        }

        // Fire. Drone.Shoot() fires m_MissileSpawnPos.Length missiles.
        m_Drone.Shoot();

        return TaskStatus.Success;
    }
}