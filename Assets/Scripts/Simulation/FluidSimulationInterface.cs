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
    void Dispose();

    // Phase 1 
    void Simulate(float targetVolume, float diffusionSpeed, float viscosity, float deltaT);

    // Phase 2 
    // Return vertice volume?
    void WaitForVerticeUpdate();

    // Apply a force to a specific simulation point
    void ApplyForce(int index, float force, float mass);
}
