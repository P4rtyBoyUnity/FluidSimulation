using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ReferenceFluidSimulation : FluidSimulationInterface
{
    private Neighbor[] neighbor = null;
    private Vector3[] vertices = null;
    private float[] speedY = null;
    private float diffusionSpeed;
    private float viscosity;

    public ReferenceFluidSimulation(Neighbor[] neighbor, ref Vector3[] vertices, float diffusionSpeed, float viscosity)
    {
        this.neighbor = neighbor;
        this.vertices = vertices;
        this.speedY = new float[vertices.Length];
        this.diffusionSpeed = diffusionSpeed;
        this.viscosity = viscosity;
    }

    public void Dispose()
    {

    }

    public void Advection(float volumeToAddPerCell)
    {
        // Advection Phase
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].y += volumeToAddPerCell + speedY[i] * Time.deltaTime;
            // check for bottom
            if (vertices[i].y < 0.0f)
                vertices[i].y = 0.0f;
        }
    }

    public void Diffusion()
    {
        // Diffusion Phase
        for (int i = 0; i < speedY.Length; i++)
        {
            float transferRate = diffusionSpeed * Time.deltaTime;
            speedY[i] += ((vertices[neighbor[i].prevX].y + vertices[neighbor[i].nextX].y + vertices[neighbor[i].prevZ].y + vertices[neighbor[i].nextZ].y) / 4.0f - vertices[i].y) * transferRate;
            speedY[i] *= viscosity;
        }
    }

    public void ApplyToMesh()
    {
        // Nothing to do, since we are aleady writting directly to vertices
    }

    public float GetSpeed(int index)
    {
        return speedY[index];
    }

    public void SetSpeed(int index, float speed)
    {
        speedY[index] = speed;
    }

}
