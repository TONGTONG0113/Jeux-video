using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rocket : MonoBehaviour
{
    [SerializeField]
    float m_MaxLifeDuration;

    [SerializeField]
    float m_DamageRadius;

    [SerializeField]
    float m_DamagePoints;


    Rigidbody m_Rigidbody;

    Transform m_Transform;


    private void Awake()
    {
        m_Rigidbody =
            GetComponent<Rigidbody>();

        m_Transform =
            transform;

        Destroy(
            gameObject,
            m_MaxLifeDuration
        );
    }


    public void Shoot(
        Vector3 targetPos,
        float travelDuration)
    {
        StartCoroutine(
            BallisticMoveCoroutine(
                travelDuration,
                m_Transform.position,
                targetPos
            )
        );


        Destroy(
            gameObject,
            travelDuration +
            Time.fixedDeltaTime
        );
    }


    IEnumerator BallisticMoveCoroutine(
        float travelTime,
        Vector3 startPos,
        Vector3 endPos)
    {
        float sqrTravelTime =
            travelTime *
            travelTime;


        float elapsedTime = 0;


        Vector3 startVelocity =
            (
                endPos -
                startPos -
                Physics.gravity *
                sqrTravelTime *
                .5f
            )
            /
            travelTime;


        m_Rigidbody.MoveRotation(
            Quaternion.LookRotation(
                startVelocity.normalized
            )
        );


        while (elapsedTime < travelTime)
        {
            yield return
                new WaitForFixedUpdate();


            Vector3 newVelocity =
                startVelocity +
                Physics.gravity *
                elapsedTime;


            m_Rigidbody.AddForce(
                newVelocity -
                m_Rigidbody.linearVelocity,
                ForceMode.VelocityChange
            );


            if (
                m_Rigidbody
                    .linearVelocity
                    .sqrMagnitude > 0
            )
            {
                m_Rigidbody.MoveRotation(
                    Quaternion.LookRotation(
                        m_Rigidbody
                            .linearVelocity
                            .normalized
                    )
                );
            }


            elapsedTime +=
                Time.fixedDeltaTime;
        }


        Destroy(gameObject);


        ExplosionManager.Instance
            .SpawnExplosionOnObject(
                m_Transform.position,
                m_Transform.forward,
                TerrainManager
                    .Instance
                    .TerrainGO,
                ExplosionSize.big
            );


        // =====================================================
        // DAMAGE + HIT STATISTICS
        // =====================================================

        Collider[] hitColliders =
            Physics.OverlapSphere(
                endPos,
                m_DamageRadius
            );


        /*
         * One unit can theoretically have several colliders.
         *
         * We use a HashSet only for STATISTICS so that the
         * same unit isn't counted several times.
         *
         * Damage behaviour itself remains the same as
         * the original project.
         */
        HashSet<Health> uniqueUnitsHit =
            new HashSet<Health>();


        foreach (
            var item in hitColliders
        )
        {
            if (
                !item.gameObject
                    .CompareTag(
                        gameObject.tag
                    )
            )
            {
                Health health =
                    item.GetComponentInChildren<
                        Health
                    >();


                if (health != null)
                {
                    // Statistics only.
                    uniqueUnitsHit.Add(
                        health
                    );


                    // Original damage behaviour.
                    health.InflictDamage(
                        m_DamagePoints
                    );
                }
            }
        }


        // Tell the statistics system whether
        // this artillery shell actually hit something.
        TurretAccuracyStats.Instance?
            .RegisterExplosion(
                gameObject.tag,
                uniqueUnitsHit.Count
            );
    }
}