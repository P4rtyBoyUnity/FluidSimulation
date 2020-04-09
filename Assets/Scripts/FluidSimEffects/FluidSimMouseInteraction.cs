using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidSimMouseInteraction : MonoBehaviour
{
    public FluidSimulation fluidSim = null;
    public float strength           = 30.0f;

    // Update is called once per frame
    void Update()
    {
        if(fluidSim) 
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (fluidSim.simCollider)
                {
                    //fluidSim.simCollider.enabled = true;
                    if (fluidSim.simCollider.Raycast(ray, out hit, 100))
                        if(Input.GetMouseButton(0))
                            fluidSim.PushVolume(hit.point, Vector3.down * strength);
                        else
                            fluidSim.PushVolume(hit.point, Vector3.up * strength);
                    //fluidSim.simCollider.enabled = false;
                }
            }
        }
    }
}
