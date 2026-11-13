using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class BattleStatsManager : MonoBehaviour
{
    public static BattleStatsManager Instance { get; private set; }


    // =========================================================
    // UI
    // =========================================================

    [Header("Battle Result UI")]

    [SerializeField]
    GameObject battleResultPanel;

    [SerializeField]
    TMP_Text resultTitleText;

    [SerializeField]
    TMP_Text resultSummaryText;

    [SerializeField]
    TMP_Text redStatsText;

    [SerializeField]
    TMP_Text greenStatsText;


    // =========================================================
    // INTERNAL STATISTICS
    // =========================================================

    class ArmyStats
    {
        public int droneShots;
        public int droneHits;

        public int turretShots;
        public int turretHitShots;
        public int turretUnitsHit;
    }


    readonly Dictionary<string, ArmyStats> m_Stats =
        new Dictionary<string, ArmyStats>();


    bool m_BattleEnded = false;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Instance = this;

        // Hide the result popup when the battle starts.
        if (battleResultPanel != null)
        {
            battleResultPanel.SetActive(false);
        }

        Debug.Log("BattleStatsManager READY");
    }


    // =========================================================
    // GET ARMY STATS
    // =========================================================

    ArmyStats GetStats(string armyTag)
    {
        if (!m_Stats.ContainsKey(armyTag))
        {
            m_Stats[armyTag] =
                new ArmyStats();
        }

        return m_Stats[armyTag];
    }


    // =========================================================
    // DRONE STATISTICS
    // =========================================================

    public void RegisterDroneShots(
        string armyTag,
        int missileCount)
    {
        GetStats(armyTag)
            .droneShots += missileCount;
    }


    public void RegisterDroneHit(
        string armyTag)
    {
        GetStats(armyTag)
            .droneHits++;
    }


    // =========================================================
    // TURRET STATISTICS
    // =========================================================

    public void RegisterTurretShot(
        string armyTag)
    {
        GetStats(armyTag)
            .turretShots++;
    }


    public void RegisterTurretExplosion(
        string armyTag,
        int unitsHit)
    {
        ArmyStats stats =
            GetStats(armyTag);


        if (unitsHit > 0)
        {
            stats.turretHitShots++;
        }


        stats.turretUnitsHit +=
            unitsHit;
    }


    // =========================================================
    // BATTLE END
    // =========================================================

    public void PrintBattleResult(
        string winnerTag)
    {
        // Prevent the result from appearing twice.
        if (m_BattleEnded)
        {
            return;
        }

        m_BattleEnded = true;


        float duration =
            Timer.Value;


        string winner =
            GetArmyName(
                winnerTag
            );


        // =====================================================
        // TITLE
        // =====================================================

        if (resultTitleText != null)
        {
            resultTitleText.text =
                "BATTLE RESULT";
        }


        // =====================================================
        // SUMMARY
        // =====================================================

        if (resultSummaryText != null)
        {
            resultSummaryText.text =
                "Winner: " +
                winner +
                "\n" +

                "Duration: " +
                duration.ToString("F1") +
                " s";
        }


        // =====================================================
        // RED
        // =====================================================

        string redReport =
            BuildArmyReport("1");


        if (redStatsText != null)
        {
            redStatsText.text =
                redReport;
        }


        // =====================================================
        // GREEN
        // =====================================================

        string greenReport =
            BuildArmyReport("2");


        if (greenStatsText != null)
        {
            greenStatsText.text =
                greenReport;
        }


        // =====================================================
        // SHOW POPUP
        // =====================================================

        if (battleResultPanel != null)
        {
            battleResultPanel.SetActive(
                true
            );
        }


        // =====================================================
        // CONSOLE BACKUP
        // =====================================================

        Debug.Log(
            "================ BATTLE RESULT ================\n" +

            "Winner: " +
            winner +
            "\n" +

            "Duration: " +
            duration.ToString("F1") +
            " s\n\n" +

            redReport +
            "\n\n" +

            greenReport +

            "\n================================================"
        );
    }


    // =========================================================
    // BUILD ONE ARMY REPORT
    // =========================================================

    string BuildArmyReport(
        string armyTag)
    {
        ArmyStats stats =
            GetStats(armyTag);


        // -----------------------------------------------------
        // LIVING DRONES
        // -----------------------------------------------------

        List<Drone> drones =
            FindObjectsByType<Drone>(
                FindObjectsSortMode.None
            )
            .Where(
                drone =>
                    drone.CompareTag(
                        armyTag
                    )
                    &&
                    drone.Health > 0f
            )
            .ToList();


        // -----------------------------------------------------
        // LIVING TURRETS
        // -----------------------------------------------------

        List<Turret> turrets =
            FindObjectsByType<Turret>(
                FindObjectsSortMode.None
            )
            .Where(
                turret =>
                    turret.CompareTag(
                        armyTag
                    )
                    &&
                    turret.Health > 0f
            )
            .ToList();


        // -----------------------------------------------------
        // REMAINING HEALTH
        // -----------------------------------------------------

        float remainingHealth =
            0f;


        foreach (Drone drone in drones)
        {
            remainingHealth +=
                drone.Health;
        }


        foreach (Turret turret in turrets)
        {
            remainingHealth +=
                turret.Health;
        }


        // -----------------------------------------------------
        // DRONE ACCURACY
        // -----------------------------------------------------

        float droneAccuracy =
            stats.droneShots > 0
                ? 100f *
                  stats.droneHits /
                  stats.droneShots
                : 0f;


        // -----------------------------------------------------
        // ARTILLERY ACCURACY
        // -----------------------------------------------------

        float turretAccuracy =
            stats.turretShots > 0
                ? 100f *
                  stats.turretHitShots /
                  stats.turretShots
                : 0f;


        // -----------------------------------------------------
        // AOE EFFECTIVENESS
        // -----------------------------------------------------

        float averageUnitsPerShot =
            stats.turretShots > 0
                ? (float)
                  stats.turretUnitsHit /
                  stats.turretShots
                : 0f;


        // -----------------------------------------------------
        // REPORT
        // -----------------------------------------------------

        return
            GetArmyName(armyTag) +
            "\n\n" +

            "Units remaining\n" +
            "Drones: " +
            drones.Count +
            "\n" +

            "Turrets: " +
            turrets.Count +
            "\n" +

            "Health: " +
            remainingHealth.ToString("F0") +
            "\n\n" +


            "DRONE MISSILES\n" +

            "Shots: " +
            stats.droneShots +
            "\n" +

            "Hits: " +
            stats.droneHits +
            "\n" +

            "Accuracy: " +
            droneAccuracy.ToString("F1") +
            "%\n\n" +


            "ARTILLERY\n" +

            "Shots: " +
            stats.turretShots +
            "\n" +

            "Hit shots: " +
            stats.turretHitShots +
            "\n" +

            "Accuracy: " +
            turretAccuracy.ToString("F1") +
            "%\n" +

            "Units hit: " +
            stats.turretUnitsHit +
            "\n" +

            "Average units / shot: " +
            averageUnitsPerShot.ToString("F2");
    }


    // =========================================================
    // TAG -> ARMY NAME
    // =========================================================

    string GetArmyName(
        string armyTag)
    {
        if (armyTag == "1")
        {
            return "RED";
        }


        if (armyTag == "2")
        {
            return "GREEN";
        }


        return
            "Army " +
            armyTag;
    }
}