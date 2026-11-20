using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class RedMedicTestTools
{
    const string InjureMenu = "Tools/RED AI/Test/Injure Selected RED Units";
    const string DefeatMenu = "Tools/RED AI/Test/Defeat Selected RED Units";

    [MenuItem(InjureMenu)]
    static void InjureSelection()
    {
        ApplyDamageToSelection(0.4f);
    }

    [MenuItem(DefeatMenu)]
    static void DefeatSelection()
    {
        ApplyDamageToSelection(1f);
    }

    [MenuItem(InjureMenu, true)]
    [MenuItem(DefeatMenu, true)]
    static bool CanDamageSelection()
    {
        return EditorApplication.isPlaying && Selection.gameObjects.Length > 0;
    }

    static void ApplyDamageToSelection(float fraction)
    {
        var affected = new HashSet<Health>();
        foreach (GameObject selected in Selection.gameObjects)
        {
            ArmyElement unit = selected.GetComponentInParent<ArmyElement>();
            if (unit == null || unit.ArmyManager is not ArmyManagerRed || !unit.CompareTag(Medic.RedTeamTag))
                continue;

            Health health = unit.UnitHealth;
            if (health != null && health.Value > 0f && affected.Add(health))
                health.InflictDamage(health.Value * fraction);
        }

        if (affected.Count == 0)
            Debug.LogWarning("Select a live, registered RED unit in the Hierarchy.");
    }
}
