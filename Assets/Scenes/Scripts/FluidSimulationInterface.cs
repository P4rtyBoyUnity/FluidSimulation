using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Neighbor
{
    public int prevX;
    public int nextX;
    public int prevZ;
    public int nextZ;
}

public interface FluidSimulationInterface
{
    //void InitSimulation(Neighbor[] neighbor, ref Vector3[] vertices);
    void ApplyToMesh();
    void Advection(float volumeToAddPerCell);
    void Diffusion();

    float GetSpeed(int index);
    void SetSpeed(int index, float height);
}
