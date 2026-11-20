using UnityEngine;
using UnityEngine.AI;

internal static class MedicNavigation
{
    const float AttachDistance = 2f;
    const float ApproachSampleDistance = 1.5f;
    static readonly float[] ApproachAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

    public static bool TryAttach(NavMeshAgent agent)
    {
        return agent.isOnNavMesh ||
            (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, AttachDistance, Filter(agent)) &&
             agent.Warp(hit.position));
    }

    public static bool TryFindApproach(NavMeshAgent agent, Vector3 target, float range, NavMeshPath path)
    {
        // Turrets can sit off the mesh. Seek a stand-off point on the Medic's side first,
        // then try other angles and the ground below the target.
        Vector3 towardAgent = Vector3.ProjectOnPlane(agent.nextPosition - target, Vector3.up).normalized;
        Vector3 offset = towardAgent * (range * 0.65f);
        NavMeshQueryFilter filter = Filter(agent);
        float approachRange = range * 0.9f;
        foreach (float angle in ApproachAngles)
        {
            Vector3 sample = target + Quaternion.AngleAxis(angle, Vector3.up) * offset;
            if (TryPath(agent, sample, target, approachRange, filter, path))
                return true;
        }

        return TryPath(agent, target, target, approachRange, filter, path);
    }

    static bool TryPath(
        NavMeshAgent agent, Vector3 sample, Vector3 target, float range, NavMeshQueryFilter filter, NavMeshPath path)
    {
        return NavMesh.SamplePosition(sample, out NavMeshHit hit, ApproachSampleDistance, filter) &&
            (hit.position - target).sqrMagnitude <= range * range &&
            agent.CalculatePath(hit.position, path) &&
            path.status == NavMeshPathStatus.PathComplete;
    }

    static NavMeshQueryFilter Filter(NavMeshAgent agent)
    {
        return new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
    }
}
