using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class Turret : ArmyElement
{
    public Transform m_TurretHead;

    public float m_RotationSpeed;

    [SerializeField]
    float m_RocketTravelDuration;

    public GameObject m_RocketPrefab;

    public Transform[] m_SpawnPoints;

    int m_CurrSpawnIndex = 0;


    Transform NextSpawnPoint
    {
        get
        {
            return m_SpawnPoints[
                (m_CurrSpawnIndex++) %
                m_SpawnPoints.Length
            ];
        }
    }


    IEnumerator m_RotationCoroutine = null;


    IEnumerator Start()
    {
        yield break;
    }


    public void Shoot(Vector3 targetPos)
    {
        /*
         * Statistics:
         * register one artillery shot for this army.
         *
         * We use the turret's existing Unity tag,
         * so this works for both RED and GREEN
         * without hard-coding army names.
         */
        TurretAccuracyStats.Instance?
            .RegisterShot(gameObject.tag);


        Transform spawnPos =
            m_SpawnPoints[
                m_CurrSpawnIndex %
                m_SpawnPoints.Length
            ];


        GameObject newRocketGO =
            Instantiate(
                m_RocketPrefab,
                spawnPos.position,
                Quaternion.LookRotation(
                    spawnPos.forward
                )
            );


        Rocket rocket =
            newRocketGO.GetComponent<Rocket>();


        rocket.Shoot(
            targetPos,
            m_RocketTravelDuration
        );


        // Preserve the original army identification.
        newRocketGO.tag = gameObject.tag;
    }


    public void RotateTowards(
        Vector3 targetPos,
        Action onRotationOver = null)
    {
        Transform spawnPoint =
            NextSpawnPoint;


        Vector3 startVelocity =
            (
                targetPos -
                spawnPoint.position -
                .5f *
                Physics.gravity *
                m_RocketTravelDuration *
                m_RocketTravelDuration
            )
            /
            m_RocketTravelDuration;


        if (m_RotationCoroutine != null)
        {
            StopCoroutine(
                m_RotationCoroutine
            );

            m_RotationCoroutine = null;
        }


        m_RotationCoroutine =
            RotateCoroutine(
                m_TurretHead.rotation,
                Quaternion.LookRotation(
                    startVelocity.normalized
                ),
                m_RotationSpeed,
                onRotationOver
            );


        StartCoroutine(
            m_RotationCoroutine
        );
    }


    IEnumerator RotateCoroutine(
        Quaternion startOrient,
        Quaternion endOrient,
        float rotationSpeed,
        Action onRotationOver = null)
    {
        float duration =
            Quaternion.Angle(
                startOrient,
                endOrient
            )
            /
            rotationSpeed;


        float elapsedTime = 0;


        while (elapsedTime < duration)
        {
            float k =
                elapsedTime /
                duration;


            m_TurretHead.rotation =
                Quaternion.Slerp(
                    startOrient,
                    endOrient,
                    k
                );


            elapsedTime +=
                Time.deltaTime;


            yield return null;
        }


        m_TurretHead.rotation =
            endOrient;


        if (onRotationOver != null)
        {
            onRotationOver();
        }
    }
}