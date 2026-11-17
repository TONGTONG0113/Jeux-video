using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Condition : le drone se trouve-t-il dans la zone d'impact prévue d'une grenade
/// ennemie qui va tomber bientôt ?
///
/// Utilisée avec un abort "Lower Priority" : elle est réévaluée à chaque frame
/// pendant que la branche d'attaque s'exécute, et l'interrompt dès qu'une menace
/// apparaît (y compris pendant le Wait de fin de boucle).
/// </summary>
[TaskCategory("MyTasks/Red Drone")]
[TaskDescription("RED drone: success if an enemy grenade is predicted to land within dangerRadius before reactionTime.")]
public class IsGrenadeThreatened : Conditional
{
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Rayon de dégâts (5 m) + rayon du drone + marge")]
    public SharedFloat dangerRadius = 7f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("On ne réagit qu'aux grenades qui tombent dans moins de X secondes (vol total = 4 s)")]
    public SharedFloat reactionTime = 2.5f;

    public override TaskStatus OnUpdate()
    {
        return RedDroneTactics.IsInGrenadeDanger(
                   transform.position, gameObject.tag, dangerRadius.Value, reactionTime.Value)
            ? TaskStatus.Success
            : TaskStatus.Failure;
    }

    public override void OnReset()
    {
        dangerRadius = 7f;
        reactionTime = 2.5f;
    }
}
