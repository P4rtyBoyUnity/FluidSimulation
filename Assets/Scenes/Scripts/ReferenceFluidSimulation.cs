using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ReferenceFluidSimulation : FluidSimulationInterface
{
    private struct Force
    {
        public int index;
        public float acceleration;
    };

    private Neighbor[] neighbor = null;
    private Vector3[] vertices = null;
    private float[] speedY = null;
    private float diffusionSpeed;
    private float viscosity;
    private List<Force> forcesToApply = new List<Force>();

    public ReferenceFluidSimulation(Neighbor[] neighbor, ref Vector3[] vertices, float diffusionSpeed, float viscosity)
    {
        this.neighbor = neighbor;
        this.vertices = vertices;
        this.speedY = new float[vertices.Length];
        this.diffusionSpeed = diffusionSpeed;
        this.viscosity = viscosity;
    }
    
    public void SetViscosity(float viscosity)
    {
        this.viscosity = viscosity;
    }

    public void SetDiffusionSpeed(float diffusionSpeed)
    {
        this.diffusionSpeed = diffusionSpeed;
    }

    public void ApplyForce(int indexToApply, float force, float mass)
    {
        forcesToApply.Add(new Force { index = indexToApply, acceleration = force / mass } );
    }

    public void Dispose()
    {
    }

    // Phase 1
    public void ApplyForcesToSimulation(float deltaT)
    {
        foreach(Force force in forcesToApply)
        {
            // TODO: Reduce speed around
            speedY[force.index] += force.acceleration * deltaT;
            speedY[neighbor[force.index].prevX] -= force.acceleration * deltaT / 4.0f;
            speedY[neighbor[force.index].nextX] -= force.acceleration * deltaT / 4.0f;
            speedY[neighbor[force.index].prevZ] -= force.acceleration * deltaT / 4.0f;
            speedY[neighbor[force.index].nextZ] -= force.acceleration * deltaT / 4.0f;
        }
        forcesToApply.Clear();
    }

    // Phase 2
    public void Diffusion(float deltaT)
    {
        // Diffusion Phase
        for (int i = 0; i < speedY.Length; i++)
        {
            float transferRate = diffusionSpeed * deltaT;
            speedY[i] += ((vertices[neighbor[i].prevX].y + vertices[neighbor[i].nextX].y + vertices[neighbor[i].prevZ].y + vertices[neighbor[i].nextZ].y) / 4.0f - vertices[i].y) * transferRate;
            speedY[i] *= viscosity;
        }
    }

    // Phase 3
    public void Advection(float volumeToAddPerCell, float deltaT)
    {
        // Advection Phase
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].y += volumeToAddPerCell + speedY[i] * deltaT;
            // check for bottom
            if (vertices[i].y < 0.0f)
                vertices[i].y = 0.0f;
        }
    }

    // Phase 4
    public void ApplyToMesh()
    {
        // Nothing to do, since we are aleady writting directly to vertices
    }
}
