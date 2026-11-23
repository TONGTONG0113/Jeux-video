using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.AI;
using UnityEngine.Events;

public abstract class ArmyManager : MonoBehaviour
{
    [SerializeField] string m_ArmyTag;
    [SerializeField] Color m_ArmyColor;
    protected List<IArmyElement> m_ArmyElements = new List<IArmyElement>();

    [SerializeField] TMP_Text m_NDronesText;
    [SerializeField] TMP_Text m_NTurretsText;
    [SerializeField] TMP_Text m_HealthText;

    [SerializeField] UnityEvent m_OnArmyIsDead;

    protected List<T> GetAllEnemiesOfType<T>(bool sortRandom) where T : ArmyElement
    {
        var enemies = GameObject.FindObjectsOfType<T>().Where(element => element != null && !element.CompareTag(m_ArmyTag)).ToList();
        if (sortRandom) enemies.Sort((a, b) => Random.value.CompareTo(.5f));
        return enemies;
    }

    public GameObject GetRandomEnemy<T>(Vector3 centerPos, float minRadius, float maxRadius) where T : ArmyElement
    {
        var enemies = GetAllEnemiesOfType<T>(true).Where(
            item=> item != null && Vector3.Distance(centerPos,item.transform.position)>minRadius
                    && Vector3.Distance(centerPos, item.transform.position) < maxRadius);

        return enemies.FirstOrDefault()?.gameObject;
    }

    protected void ComputeStatistics(ref int nDrones, ref int nTurrets, ref int cumulatedHealth)
    {
        int safeDrones = 0;
        int safeTurrets = 0;
        int safeHealth = 0;

        for (int i = m_ArmyElements.Count - 1; i >= 0; i--)
        {
            var item = m_ArmyElements[i];
            if (item == null || (item is MonoBehaviour mono && mono == null)) 
            {
                m_ArmyElements.RemoveAt(i);
                continue;
            }

            if (item is Drone)
            {
                safeDrones++;
                safeHealth += (int)item.Health;
            }
            else if (item is Turret)
            {
                safeTurrets++;
                safeHealth += (int)item.Health;
            }
        }

        nDrones = safeDrones;
        nTurrets = safeTurrets;
        cumulatedHealth = safeHealth;
    }

    public virtual IEnumerator Start()
    {
        yield return null; 

        GameObject[] allArmiesElements = GameObject.FindGameObjectsWithTag(m_ArmyTag);
        foreach (var item in allArmiesElements)
        {
            ArmyElement armyElement = item.GetComponent<ArmyElement>();
            if (armyElement == null) continue; 

            armyElement.ArmyManager = this;
            m_ArmyElements.Add(armyElement);
        }

        RefreshHudDisplay();
        yield break;
    }

    protected void RefreshHudDisplay()
    {
        int nDrones = 0, nTurrets = 0, health = 0;
        ComputeStatistics(ref nDrones, ref nTurrets, ref health);

        if (m_NDronesText != null) m_NDronesText.text = nDrones.ToString();
        if (m_NTurretsText != null) m_NTurretsText.text = nTurrets.ToString();
        if (m_HealthText != null) m_HealthText.text = health.ToString();
    }

    public virtual void ArmyElementHasBeenKilled(GameObject go)
    {
        if (go == null) return;
        
        IArmyElement element = go.GetComponent<IArmyElement>();
        if (element != null) m_ArmyElements.Remove(element);

        RefreshHudDisplay();

        if (m_ArmyElements.Count == 0 && m_OnArmyIsDead != null) 
            m_OnArmyIsDead.Invoke();
    }
}