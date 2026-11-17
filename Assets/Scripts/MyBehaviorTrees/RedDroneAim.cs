using UnityEngine;
using UnityEngine.AI;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Remplace "My Slerp Rotate Around Y" pour les drones rouges.
/// Même rotation (Slerp Coef 4, Arrival Angle 5° - Annexe A), mais le drone
/// s'oriente vers le POINT D'INTERCEPTION anticipé de la cible et non vers
/// sa position actuelle.
/// </summary>
[TaskCategory("MyTasks/Red Drone")]
[TaskDescription("RED drone: rotates (slerp) towards the predicted interception point of the target.")]
public class RedDroneAim : Action
{
    public SharedTransform target;

    [BehaviorDesigner.Runtime.Tasks.Tooltip("Annexe A : Slerp Coef (non modifié)")]
    public SharedFloat slerpCoef = 4f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Annexe A : Arrival Angle (non modifié)")]
    public SharedFloat arrivalAngle = 5f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Sécurité : abandon si l'orientation n'aboutit pas")]
    public SharedFloat timeout = 2.5f;

    NavMeshAgent m_Agent;
    float m_StartTime;

    public override void OnAwake()
    {
        m_Agent = GetComponent<NavMeshAgent>();
    }

    public override void OnStart()
    {
        m_StartTime = Time.time;
    }

    public override TaskStatus OnUpdate()
    {
        if (!RedDroneTactics.IsAlive(target.Value)) return TaskStatus.Failure;

        float missileStartSpeed = m_Agent != null ? m_Agent.speed : 10f;
        Vector3 aimPoint = RedDroneTactics.PredictAimPoint(transform.position, missileStartSpeed, target.Value);

        Vector3 dir = RedDroneTactics.Flat(aimPoint - transform.position);
        if (dir.sqrMagnitude < 0.0001f) return TaskStatus.Success;

        Quaternion endOrientation = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, endOrientation, Time.deltaTime * slerpCoef.Value);

        Debug.DrawLine(transform.position, aimPoint, Color.red);

        float angle = Quaternion.Angle(transform.rotation, endOrientation);
        if (angle < arrivalAngle.Value) return TaskStatus.Success;

        if (Time.time - m_StartTime > timeout.Value)
            return angle < 3f * arrivalAngle.Value ? TaskStatus.Success : TaskStatus.Failure;

        return TaskStatus.Running;
    }

    public override void OnReset()
    {
        target = null;
        slerpCoef = 4f;
        arrivalAngle = 5f;
        timeout = 2.5f;
    }
}
