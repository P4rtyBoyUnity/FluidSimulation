using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoatPiloting : MonoBehaviour
{
    public FluidSimulation  fluidSim = null;
    public Vector3          wakePos = Vector3.right * 5.0f + Vector3.up;
    public float            wakeForce = 20.0f;
    public Vector3          frontWavePos = Vector3.left * 7.0f + Vector3.up + Vector3.forward;
    public float            frontWaveForce = 20.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //if(Input.GetKeyDown(KeyCode.UpArrow))
        {
            var rigidBody = GetComponent<Rigidbody>();
            if(rigidBody)
            {
                rigidBody.AddRelativeForce(Vector3.forward * 10.0f);
            }
        }

        if (fluidSim)
        {
            Vector3 wake = transform.position + wakePos;
            Vector3 frontWave = transform.position +frontWavePos;

            /*
            fluidSim.PushVolume(wake + Vector3.forward, Vector3.down * wakeForce);
            fluidSim.PushVolume(wake + Vector3.back, Vector3.down * wakeForce);

            fluidSim.PushVolume(frontWave + Vector3.forward, Vector3.up * frontWaveForce);            
            fluidSim.PushVolume(frontWave + Vector3.back, Vector3.up * frontWaveForce);
            */

            fluidSim.PushVolume(wake, Vector3.down * wakeForce);
            //fluidSim.PushVolume(frontWave, Vector3.up * frontWaveForce);
            fluidSim.DisplaceVolume(frontWave, frontWaveForce / 50.0f);
        }
    }
}
