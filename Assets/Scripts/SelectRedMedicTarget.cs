using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("MyTasks")]
[TaskDescription("Publishes the ally selected by the standalone RED Medic.")]
public class SelectRedMedicTarget : Action
{
    public SharedTransform healTarget = new SharedTransform();
    Medic m_Medic;

    public override void OnAwake()
    {
        m_Medic = GetComponent<Medic>();
    }

    public override TaskStatus OnUpdate()
    {
        if (healTarget == null)
            return TaskStatus.Failure;

        // The Medic owns navigation and healing even when a tree observes its target.
        ArmyElement target = m_Medic != null ? m_Medic.CurrentTarget : null;
        healTarget.Value = target != null ? target.transform : null;
        return target != null ? TaskStatus.Success : TaskStatus.Failure;
    }
}
