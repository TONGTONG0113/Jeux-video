using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Sélection de cible des drones rouges : TIR CONCENTRÉ (focus fire).
///
/// Constat en simulation : avec la répartition "anti-regroupement", les dégâts
/// rouges étaient étalés sur trop de cibles (490 dégâts de missiles pour 3 drones
/// verts détruits). Or chaque drone vert détruit, c'est 2 missiles de moins
/// tirés sur nous à chaque cycle (loi de Lanchester).
///
///  1. On garde sa cible jusqu'à sa destruction (pas de changement toutes les 2 s),
///     sauf si elle est déjà condamnée par les missiles en vol ou trop loin.
///  2. Tant qu'il reste plus de 2 drones verts : uniquement des drones.
///     Ensuite, ArmyManagerRed est prioritaire pour les tourelles presque détruites.
///  3. Sinon : drone vert le plus "rentable" (proche, déjà attaqué par d'autres
///     drones rouges, déjà blessé), puis tourelle verte la plus faible.
///
/// Corrections précédentes conservées : portée illimitée (plus de drones inactifs
/// faute de cible à moins de 50 m), suppression de la retraite vers (0,0,0).
/// </summary>
[TaskCategory("MyTasks/Red Drone")]
[TaskDescription("RED drone: focus-fire target selection (keeps its target until it dies, avoids overkill).")]
public class SelectRedDroneTarget : Action
{
    public SharedTransform target;

    [BehaviorDesigner.Runtime.Tasks.Tooltip("Au-delà de cette distance, on abandonne la cible actuelle")]
    public SharedFloat maxKeepDistance = 35f;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Nombre max d'alliés comptés dans le bonus de concentration")]
    public SharedInt maxAttackersBonus = 3;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Les drones rouges n'attaquent les tourelles que s'il reste au plus N drones verts")]
    public SharedInt attackTurretsWhenEnemyDronesAtMost = 2;

    [BehaviorDesigner.Runtime.Tasks.Tooltip("STRATÉGIE : true = assaut des tourelles vertes d'abord (dans leur angle mort < 5 m), false = drones verts d'abord")]
    public SharedBool turretsFirst = false;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Mode tourelles d'abord : nombre max de drones rouges par tourelle verte")]
    public SharedInt maxDronesPerTurret = 2;
    [BehaviorDesigner.Runtime.Tasks.Tooltip("STRATÉGIE MIXTE : nombre de drones \"raiders\" envoyés sur les tourelles vertes, les autres attaquent les drones (0 = drones d'abord)")]
    public SharedInt raidersCount = 4;

    IArmyElement m_ArmyElement;

    public override void OnAwake()
    {
        m_ArmyElement = (IArmyElement)GetComponent(typeof(IArmyElement));
    }

    public override TaskStatus OnUpdate()
    {
        // La référence à l'armée n'est injectée qu'après la première frame.
        if (m_ArmyElement == null || m_ArmyElement.ArmyManager == null) return TaskStatus.Running;

        // 1. Garder la cible actuelle tant qu'elle vaut le coup
        // Tant que les drones verts sont nombreux, ils sont la seule priorité :
        // ce sont eux qui détruisent nos drones (≈ 600 dégâts de missiles par bataille).
        // Constat en simulation : environ la moitié des dégâts de nos missiles partait
        // dans des tourelles vertes "presque détruites", pendant que les drones verts
        // survivaient. Les tourelles vertes sont laissées à notre artillerie.
        bool dronesOnly = RedDroneTactics.CountEnemyDrones(gameObject.tag) > attackTurretsWhenEnemyDronesAtMost.Value;

        // ---------------------------------------------------------
        // STRATÉGIE "TOURELLES D'ABORD"
        // Les tourelles vertes (9 contre 6) détruisent notre artillerie en ~20 s
        // et infligent ~700 dégâts par bataille. Elles ne peuvent pas viser
        // une unité à moins de 5 m (Min Radius) et ciblent en priorité nos tourelles :
        // un drone rouge collé à une tourelle verte est donc peu exposé à l'artillerie.
        // ---------------------------------------------------------
        bool raider = turretsFirst.Value ||
                      RedDroneTactics.IsRaider(gameObject, gameObject.tag, raidersCount.Value);

        if (raider && RedDroneTactics.CountEnemyTurrets(gameObject.tag) > 0)
        {
            Transform cur = target.Value;
            if (cur != null && cur.GetComponent<Turret>() != null &&
                RedDroneTactics.IsAlive(cur) && RedDroneTactics.GetExpectedHealth(cur) > 0f)
            {
                RedDroneTactics.SetDroneTarget(gameObject, cur);
                return TaskStatus.Success;
            }

            Transform turret = RedDroneTactics.SelectTurretRushTarget(transform, gameObject.tag, maxDronesPerTurret.Value);
            if (turret != null)
            {
                target.Value = turret;
                RedDroneTactics.SetDroneTarget(gameObject, turret);
                return TaskStatus.Success;
            }
            // Toutes les tourelles déjà condamnées par les missiles en vol : on passe aux drones.
        }

        Transform current = target.Value;
        if (dronesOnly && current != null && current.GetComponent<Turret>() != null) current = null;

        if (RedDroneTactics.IsAlive(current) &&
            RedDroneTactics.GetExpectedHealth(current) > 0f &&
            RedDroneTactics.FlatDistance(transform.position, current.position) <= maxKeepDistance.Value)
        {
            RedDroneTactics.SetDroneTarget(gameObject, current);
            return TaskStatus.Success;
        }

        // 2. Tourelle verte presque détruite signalée par le chef d'état-major
        Transform selected = null;
        ArmyManagerRed manager = m_ArmyElement.ArmyManager as ArmyManagerRed;
        if (manager != null && !dronesOnly)
        {
            GameObject fromManager = manager.RequestTarget(
                ArmyManagerRed.RequesterType.Drone, transform.position, 0f, 1000f);

            if (fromManager != null && fromManager.GetComponent<Turret>() != null)
                selected = fromManager.transform;
        }

        // 3. Tir concentré
        if (selected == null)
            selected = RedDroneTactics.SelectFocusTarget(transform, gameObject.tag, maxAttackersBonus.Value);

        // Mode "drones uniquement" : si tous les drones verts sont déjà condamnés
        // par des missiles en vol, on garde sa cible actuelle plutôt qu'une tourelle.
        if (dronesOnly && selected != null && selected.GetComponent<Turret>() != null)
            selected = RedDroneTactics.IsAlive(target.Value) ? target.Value : null;

        // 4. Secours
        if (selected == null)
        {
            GameObject nearest = RedDroneTactics.FindNearestEnemy(transform.position, gameObject.tag);
            if (nearest != null) selected = nearest.transform;
        }

        target.Value = selected;
        RedDroneTactics.SetDroneTarget(gameObject, selected);
        return selected != null ? TaskStatus.Success : TaskStatus.Failure;
    }

    public override void OnBehaviorComplete()
    {
        RedDroneTactics.SetDroneTarget(gameObject, null);
    }

    public override void OnReset()
    {
        target = null;
        maxKeepDistance = 35f;
        maxAttackersBonus = 3;
        attackTurretsWhenEnemyDronesAtMost = 2;
        turretsFirst = false;
        maxDronesPerTurret = 2;
        raidersCount = 4;
    }
}
