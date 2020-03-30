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

    // Phase 1: Apply Forces (forces has impact on speeds)
    void ApplyForcesToSimulation(float deltaT);
    // Phase 2: Diffusion (height has impact on speeds)
    void Diffusion(float deltaT);
    // Phase 3: Advection (speed has impact on heights)
    void Advection(float volumeToAddPerCell, float deltaT);
    // Phase 4: Copy height to vertices
    void ApplyToMesh();

    // Change the simulatin viscosity
    void SetViscosity(float viscosity);
    // Change the simulation viscosity
    void SetDiffusionSpeed(float diffusionSpeed);
    // Apply a force to a specific simulation point
    void ApplyForce(int index, float force, float mass);
}
