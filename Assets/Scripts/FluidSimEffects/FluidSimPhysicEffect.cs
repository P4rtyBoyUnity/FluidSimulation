using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidSimEffect : MonoBehaviour
{
    public virtual  bool OnFluidSimContact(FluidSimulation sim, FluidSimulation.PhysicObject newObject)
    {
        return true;
    }

    public virtual void UpdateBehavior(FluidSimulation sim, FluidSimulation.PhysicObject physicObject)
    {

    }
}

public class FluidSimPhysicEffect : FluidSimEffect
{
    public override bool OnFluidSimContact(FluidSimulation sim, FluidSimulation.PhysicObject newObject)
    {
        return true;
    }

    public override void UpdateBehavior(FluidSimulation sim, FluidSimulation.PhysicObject physicObject)
    {
        // PARAMS FOR OBJECTS
        // How much they are in the water
        // How fast they rotate
        float y;
        Vector3 normal = sim.GetYNormal(physicObject.gao.transform.position.x, physicObject.gao.transform.position.z, out y);

        var rigidBody = physicObject.gao.GetComponent<Rigidbody>();
        if (rigidBody)
        {
            /*
            rigidBody.isKinematic = true;                
            physicObjects[i].transform.position = new Vector3(physicObjects[i].transform.position.x, y, physicObjects[i].transform.position.z);
            physicObjects[i].transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            */


            if (y > 0.0f)
            {
                float mv = physicObject.volume / rigidBody.mass;

                float height = 0.0f;
                var objCollider = physicObject.gao.GetComponent<Collider>();
                if (objCollider)
                    height = objCollider.bounds.size.y;

                //Debug.Log("Mass=" + rigidBody.mass + ", volume=" + physicObjects[i].volume + ", mv=" + mv + ", height = " + height);

                float PercentageInFluid = 1.0f - (physicObject.gao.transform.position.y - y + (height / 2.0f)) / height;
                float Ratio = 1.0f - physicObject.gao.transform.position.y / y;

                //Debug.Log("pos = " + physicObjects[i].gao.transform.position.y + ", y=" + y + ", Ratio = " + Ratio + ", Height=" + PercentageInFluid);

                // compute mass in fluid 
                float massInsideFluid = PercentageInFluid * physicObject.volume;  //1.0 = liquid vm
                float massOutsideFluid = (1.0f - PercentageInFluid) * rigidBody.mass;
                float archimedRatio = massInsideFluid / (massInsideFluid + massOutsideFluid);

                // Update submerged volume
                float newSubmergedVolume = PercentageInFluid * physicObject.volume;
                float deltaSubmergedVolume = newSubmergedVolume - physicObject.submergedVolume;
                physicObject.submergedVolume = newSubmergedVolume;

                //Debug.Log("Mass in fluid=" + massInsideFluid + ", Mass outside fluid= " + massOutsideFluid + ", ArchiRatio=" + archimedRatio + ", Volume=" + physicObjects[i].volume);

                // submerge volume effect
                int index = sim.GetArrayIndexFromPos(physicObject.gao.transform.position);
                /*
                if (index >= 0)
                    speedY[index] += deltaSubmergedVolume * 2.0f;
                */

                /* physic effect temporarily removed 
                if(index >= 0)
                    simulation.SetSpeed(index, simulation.GetSpeed(index) + deltaSubmergedVolume * 2.0f);

                rigidBody.AddForce((0.2f * archimedRatio * mv * normal * Physics.gravity.magnitude), ForceMode.Force);
                if (rigidBody.velocity.y < 0.0f)
                    rigidBody.velocity = rigidBody.velocity * 0.98f;
                    */

                /*
                if (Ratio > 0.0f)
                {
                    rigidBody.AddForce(mv * normal * Physics.gravity.magnitude * (0.25f + Ratio * 0.25f), ForceMode.Force);
                    if (rigidBody.velocity.y < 0.0f)
                        rigidBody.velocity = rigidBody.velocity * 0.98f;
                }
                */
            }

            /*
            objectsSpeed[i] = (objectsSpeed[i] + normal) * 0.95f;
            Vector3 Dest = floatingObjects[i].transform.position + objectsSpeed[i] * Time.deltaTime;
            Dest.y = y;
            floatingObjects[i].transform.position = Vector3.Lerp(floatingObjects[i].transform.position, Dest, 0.3f);
            floatingObjects[i].transform.rotation = Quaternion.RotateTowards(floatingObjects[i].transform.rotation, Quaternion.FromToRotation(Vector3.up, normal), 0.3f);
            */

        }
        else
        {
            physicObject.gao.transform.position = new Vector3(physicObject.gao.transform.position.x, y, physicObject.gao.transform.position.z);
            physicObject.gao.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        }
    }
}
