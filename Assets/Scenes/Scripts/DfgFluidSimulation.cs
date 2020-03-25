using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Unity.DataFlowGraph;

[System.Serializable]
public class DfgSimulation : FluidSimulationInterface
{
    private Vector3[] vertices = null;

    public DfgSimulation(Neighbor[] neighbor, ref Vector3[] vertices, float diffusionSpeed, float viscosity)
    {
        this.vertices = vertices;

        // Create DFG Nodes

    }

    ~DfgSimulation()
    {
    }

    public void Advection(float volumeToAddPerCell)
    {
        // All advection is done by ECS

        // Only thing left to do; add volume lost/gained
        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y += volumeToAddPerCell;
    }

    public void Diffusion()
    {
        // Nothing to do, done in ECS
    }

    public void ApplyToMesh()
    {
    }

    public float GetSpeed(int index)
    {
        //TODO
        return 0.0f;
    }

    public void SetSpeed(int index, float speed)
    {
        //TODO
    }
}
