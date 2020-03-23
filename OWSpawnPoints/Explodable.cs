using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OWSpawnPoints
{
    class Explodable : MonoBehaviour
    {
        void OnCollisionEnter(Collision collision)
        {


            var probe = Locator.GetProbe();
            var explosion = Instantiate(GameObject.Find("Explosion_ModelRocket (1)"), probe.transform);

            explosion.GetComponent<ParticleSystem>().Play();
            explosion.transform.localScale *= 50;

            explosion.AddComponent<AudioSource>();
            var audio = explosion.AddComponent<OWAudioSource>();

            audio.PlayOneShot(AudioType.TH_ModelShipCrash, 1f);

            Invoke(nameof(RetrieveProbe), 0.1f);
            Invoke(nameof(DestroySelf), 0.2f);
        }

        void RetrieveProbe()
        {
            Locator.GetProbe().ExternalRetrieve();
        }

        void DestroySelf()
        {
            Destroy(gameObject);
        }
    }
}
