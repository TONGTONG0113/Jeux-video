using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField] float m_StartHealth;
    float m_Health;
    
    // 提供接口给 ArmyManager 统计和医疗兵
    public float Value => m_Health;
    public float MaxValue => m_StartHealth;
    public bool CanHeal => m_Health > 0f && m_Health < MaxValue;

    [SerializeField] Slider m_HealthBar;
    [SerializeField] UnityEvent m_OnDieEvent = new UnityEvent();
    public UnityEvent DeathEvent => m_OnDieEvent;

    private void Start()
    {
        m_Health = m_StartHealth;
        RefreshHealthDisplay();
    }

    void RefreshHealthDisplay()
    {
        if (m_HealthBar != null) 
            m_HealthBar.value = m_Health / m_StartHealth;
    }

    public void InflictDamage(float damage)
    {
        m_Health = Mathf.Max(m_Health - damage, 0);
        RefreshHealthDisplay();

        if (m_Health == 0 && m_OnDieEvent != null) m_OnDieEvent.Invoke();
    }

    // 【新增】给医疗兵用
    public void Heal(float amount)
    {
        if (!CanHeal || !(amount > 0f)) return;

        m_Health = Mathf.Min(m_Health + amount, MaxValue);
        RefreshHealthDisplay();
    }
}
