using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using System;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;

[System.Serializable]
public class ReferenceFluidSimulation : FluidSimulationInterface
{
    private struct Force
    {
        public int index;
        public float acceleration;
    };

    private NativeArray<Neighbor> neighbor;
    private NativeArray<float> speedY;
    private NativeArray<Vector3> vertices;
    private List<Force> forcesToApply = new List<Force>();

    public ReferenceFluidSimulation(Neighbor[] neighbor, ref NativeArray<Vector3> vertices)
    {
        this.vertices = vertices;
        this.neighbor = new NativeArray<Neighbor>(neighbor.Length, Allocator.Persistent);
        this.neighbor.CopyFrom(neighbor);
        this.speedY = new NativeArray<float>(vertices.Length, Allocator.Persistent);
    }

    public void Dispose()
    {
        neighbor.Dispose();
        speedY.Dispose();
    }

    public void ApplyForce(int indexToApply, float force, float mass)
    {
        forcesToApply.Add(new Force { index = indexToApply, acceleration = force / mass });
    }

    // Phase 1
    [BurstCompile]
    public void ApplyForcesToSimulation(float deltaT)
    {
        foreach (Force force in forcesToApply)
        {
            speedY[force.index] += force.acceleration * deltaT;
            /*
            speedY[neighbor[force.index].prevX] -= force.acceleration * deltaT / 4.0f;
            speedY[neighbor[force.index].nextX] -= force.acceleration * deltaT / 4.0f;
            speedY[neighbor[force.index].prevZ] -= force.acceleration * deltaT / 4.0f;
            speedY[neighbor[force.index].nextZ] -= force.acceleration * deltaT / 4.0f;
            */
        }
        forcesToApply.Clear();
    }

    // Phase 2
    [BurstCompile]
    public float Diffusion(float diffusionSpeed, float viscosity, float deltaT)
    {
        // Diffusion Phase
        float currentVolume = 0.0f;
        for (int i = 0; i < speedY.Length; i++)
        {
            float transferRate = diffusionSpeed * deltaT;
            speedY[i] += ((vertices[neighbor[i].prevX].y + vertices[neighbor[i].nextX].y + vertices[neighbor[i].prevZ].y + vertices[neighbor[i].nextZ].y) / 4.0f - vertices[i].y) * transferRate;
            speedY[i] *= viscosity;
            currentVolume += vertices[i].y;
        }

        return currentVolume;
    }

    // Phase 3
    [BurstCompile]
    public void Advection(float volumeToAddPerCell, float deltaT)
    {
        // Advection Phase
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = new Vector3(vertices[i].x, Math.Max(vertices[i].y + volumeToAddPerCell + speedY[i] * deltaT, 0.0f), vertices[i].z);
    }

    public void Simulate(float targetTotalheight, float diffusionSpeed, float viscosity, float deltaT)
    {
        ApplyForcesToSimulation(deltaT);
        float currentVolume = Diffusion(diffusionSpeed, viscosity, deltaT);
        float volumeToAddPerCell = (targetTotalheight - currentVolume) / (float)vertices.Length;
        Advection(volumeToAddPerCell, deltaT);
    }

    public void WaitForVerticeUpdate()
    {
        // Nothing to do, since we are aleady writting directly to vertices
    }
}