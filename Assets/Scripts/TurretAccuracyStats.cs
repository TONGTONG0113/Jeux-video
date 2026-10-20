using System.Collections.Generic;
using UnityEngine;

public class TurretAccuracyStats : MonoBehaviour
{
    public static TurretAccuracyStats Instance;

    class TeamStats
    {
        public int Shots = 0;
        public int HitShots = 0;
        public int UnitsHit = 0;
    }

    readonly Dictionary<string, TeamStats> m_Stats =
        new Dictionary<string, TeamStats>();


    private void Awake()
    {
        Instance = this;
    }


    TeamStats GetStats(string teamTag)
    {
        if (!m_Stats.ContainsKey(teamTag))
        {
            m_Stats.Add(teamTag, new TeamStats());
        }

        return m_Stats[teamTag];
    }


    string GetTeamName(string teamTag)
    {
        if (teamTag == "1")
        {
            return "RED ARTILLERY";
        }

        if (teamTag == "2")
        {
            return "GREEN ARTILLERY";
        }

        return "ARTILLERY - " + teamTag;
    }


    public void RegisterShot(string teamTag)
    {
        TeamStats stats =
            GetStats(teamTag);

        stats.Shots++;
    }


    public void RegisterExplosion(
        string teamTag,
        int unitsHit)
    {
        TeamStats stats =
            GetStats(teamTag);

        // A shell counts as a hit
        // if at least one enemy unit is damaged.
        if (unitsHit > 0)
        {
            stats.HitShots++;
        }

        stats.UnitsHit += unitsHit;
    }


    private void OnGUI()
    {
        int panelIndex = 0;

        foreach (var entry in m_Stats)
        {
            string teamTag =
                entry.Key;

            TeamStats stats =
                entry.Value;


            float accuracy =
                stats.Shots > 0
                    ? (float)stats.HitShots /
                      stats.Shots * 100f
                    : 0f;


            float averageUnitsHit =
                stats.Shots > 0
                    ? (float)stats.UnitsHit /
                      stats.Shots
                    : 0f;


            float x = 10f;

            float y =
                10f +
                panelIndex * 175f;


            // =================================================
            // PANEL TITLE
            // =================================================

            GUI.Box(
                new Rect(
                    x,
                    y,
                    310,
                    165
                ),
                GetTeamName(teamTag)
            );


            // =================================================
            // STATISTICS
            // =================================================

            GUI.Label(
                new Rect(
                    x + 15,
                    y + 30,
                    280,
                    20
                ),
                "Shots: " +
                stats.Shots
            );


            GUI.Label(
                new Rect(
                    x + 15,
                    y + 50,
                    280,
                    20
                ),
                "Hit Shots: " +
                stats.HitShots
            );


            GUI.Label(
                new Rect(
                    x + 15,
                    y + 70,
                    280,
                    20
                ),
                "Accuracy: " +
                accuracy.ToString("F1") +
                "%"
            );


            GUI.Label(
                new Rect(
                    x + 15,
                    y + 90,
                    280,
                    20
                ),
                "Units Hit: " +
                stats.UnitsHit
            );


            GUI.Label(
                new Rect(
                    x + 15,
                    y + 110,
                    280,
                    20
                ),
                "Average Units / Shot: " +
                averageUnitsHit.ToString("F2")
            );


            // =================================================
            // ACCURACY BAR
            // =================================================

            GUI.Box(
                new Rect(
                    x + 15,
                    y + 137,
                    270,
                    15
                ),
                ""
            );


            float barWidth =
                266f *
                Mathf.Clamp01(
                    accuracy / 100f
                );


            GUI.Box(
                new Rect(
                    x + 17,
                    y + 139,
                    barWidth,
                    11
                ),
                ""
            );


            panelIndex++;
        }
    }
}