using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidSimRainEffect : MonoBehaviour
{
    public FluidSimulation  fluidSim = null;
    public float            countPerM2PerSec = 1;
    public float            strength = 1.0f;

    // Update is called once per frame
    void Update()
    {
        if(fluidSim)
        {
            float surface = fluidSim.simCollider.size.x * fluidSim.simCollider.size.x;
            uint count = (uint)(surface * countPerM2PerSec * Time.deltaTime);

            for(uint i = 0; i < count; i++)
            {
                float px = Random.Range(fluidSim.simCollider.bounds.min.x, fluidSim.simCollider.bounds.max.x);
                float pz = Random.Range(fluidSim.simCollider.bounds.min.z, fluidSim.simCollider.bounds.max.z);
                fluidSim.DisplaceVolume(new Vector3(px, fluidSim.simCollider.bounds.max.y, pz), -strength);
            }
        }
    }
}
