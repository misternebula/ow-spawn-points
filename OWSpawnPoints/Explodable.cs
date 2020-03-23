using OWML.ModHelper.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OWSpawnPoints
{
    class Explodable : MonoBehaviour
    {
        GameObject _toBeDestroyed;

        void OnCollisionEnter(Collision col)
        {
            if (col.collider.isTrigger)
            {
                return;
            }

            var probe = Locator.GetProbe();
            var explosion = Instantiate(GameObject.Find("Explosion_ModelRocket (1)"), probe.transform);

            explosion.GetComponent<ParticleSystem>().Play();
            explosion.transform.localScale *= 10;

            explosion.AddComponent<AudioSource>();
            var audio = explosion.AddComponent<OWAudioSource>();

            audio.PlayOneShot(AudioType.TH_ModelShipCrash, 1f);

            _toBeDestroyed = col.gameObject;
            Invoke(nameof(RetrieveProbe), 0.1f);
            Invoke(nameof(DestroyObject), 0.2f);
        }

        void RetrieveProbe()
        {
            Locator.GetPlayerTransform().GetComponentInChildren<ProbeLauncher>().Invoke("RetrieveProbe", false, false);
        }

        void DestroyObject()
        {
            if (_toBeDestroyed != null)
            {
                Locator.GetProbe().transform.parent = null;
                Destroy(_toBeDestroyed);
            }
        }

        void LateUpdate()
        {
            if (Locator.GetProbe() == null)
            {
                return;
            }

            transform.position = Locator.GetProbe().transform.position;
            transform.rotation = Locator.GetProbe().transform.rotation;
        }
    }
}
