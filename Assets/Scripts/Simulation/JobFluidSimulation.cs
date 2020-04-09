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
public class JobFluidSimulation : FluidSimulationInterface
{
    private struct Force
    {
        public int index;
        public float acceleration;
    };

    const int NbJobsToUse = 16;

    private NativeArray<Neighbor> neighbor;
    private NativeArray<float> speedY;
    private NativeArray<Vector3> vertices;
    private List<Force> forcesToApply = new List<Force>();    
    
    // job related
    private DiffusionJob[] diffusionJobs = null;
    private NativeArray<JobHandle> diffusionHandles;
    private ComputeVolumeJob volumeJob;
    private AdvectionJob advectionJob;

    public JobFluidSimulation(Neighbor[] neighbor, ref NativeArray<Vector3> vertices)
    {
        this.vertices = vertices;
        this.neighbor = new NativeArray<Neighbor>(neighbor.Length, Allocator.Persistent);
        this.neighbor.CopyFrom(neighbor);
        this.speedY = new NativeArray<float>(vertices.Length, Allocator.Persistent);

        // Init advection job
        advectionJob.speedY = this.speedY;
        advectionJob.vertices = this.vertices;
        advectionJob.volumeToAddPerCell = new NativeArray<float>(1, Allocator.Persistent); ;

        // Init volume job
        volumeJob.nbCell = (uint)vertices.Length;
        volumeJob.localHeightArray = new NativeArray<float>(NbJobsToUse, Allocator.Persistent);
        volumeJob.heightToAddPerCell = advectionJob.volumeToAddPerCell;

        // Init diffusions job
        diffusionJobs = new DiffusionJob[NbJobsToUse];
        diffusionHandles = new NativeArray<JobHandle>(NbJobsToUse, Allocator.Persistent);

        int chunkSize = (vertices.Length + NbJobsToUse - 1) / NbJobsToUse;
        for (int i = 0; i < NbJobsToUse; i++)
        {
            diffusionJobs[i].neighbor = this.neighbor;
            diffusionJobs[i].vertices = this.vertices;
            diffusionJobs[i].startIndex = i * chunkSize;
            diffusionJobs[i].count = Math.Min(chunkSize, speedY.Length - diffusionJobs[i].startIndex);
            diffusionJobs[i].speedY = speedY.Slice(diffusionJobs[i].startIndex, diffusionJobs[i].count);
            diffusionJobs[i].volume = volumeJob.localHeightArray.Slice(i, 1);
        }       

    }

    public void Dispose()
    {
        diffusionHandles.Dispose();
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
            // TODO: Reduce speed around
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

    public void Simulate(float targetTotalheight, float diffusionSpeed, float viscosity, float deltaT)
    {
        // Phase 1 : Apply forces
        ApplyForcesToSimulation(deltaT);

        // Phase 2 : Diffusion
        for (int i = 0; i < NbJobsToUse; i++)
        {
            diffusionJobs[i].transferRate = diffusionSpeed * deltaT;
            diffusionJobs[i].viscosity = viscosity;
            diffusionJobs[i].volume[0] = 0;
            diffusionHandles[i] = diffusionJobs[i].Schedule();
        }

        // Wait for completion of all jobs
        JobHandle waitAllHandle = JobHandle.CombineDependencies(diffusionHandles);

        // Phase 3 : Compute volume to add Per cell
        volumeJob.targetTotalheight = targetTotalheight;
        JobHandle volumeJobHandle = volumeJob.Schedule(waitAllHandle);
        volumeJobHandle.Complete();

        // Phase 4 : Advection
        advectionJob.volumeToAddPerCell = volumeJob.heightToAddPerCell;
        advectionJob.deltaT = deltaT;
        JobHandle advectionJobHandle = advectionJob.Schedule(speedY.Length, 1, volumeJobHandle);

        // Wait for the job to complete
        advectionJobHandle.Complete();
    }

    public void WaitForVerticeUpdate()
    {
        // Nothing to do, since we are aleady writting directly to vertices
    }

    /// <summary>
    /// Job section
    /// </summary>
    [BurstCompile]
    struct DiffusionJob : IJob
    {
        [ReadOnly]
        public NativeArray<Neighbor> neighbor;
        [ReadOnly]
        public NativeArray<Vector3> vertices;
        [ReadOnly]
        public float transferRate;
        [ReadOnly]
        public float viscosity;
        [ReadOnly]
        public int startIndex;
        [ReadOnly]
        public int count;

        // outputs
        [NativeDisableContainerSafetyRestriction]
        public NativeSlice<float> volume;
        [NativeDisableContainerSafetyRestriction]
        public NativeSlice<float> speedY;

        public void Execute()
        {
            float localHeightSum = 0;
            for (int i = startIndex; i < startIndex + count; i++)
            {
                int speedIndex = i - startIndex;
                speedY[speedIndex] += ((vertices[neighbor[i].prevX].y + vertices[neighbor[i].nextX].y + vertices[neighbor[i].prevZ].y + vertices[neighbor[i].nextZ].y) / 4.0f - vertices[i].y) * transferRate;
                speedY[speedIndex] *= viscosity;
                localHeightSum += vertices[i].y;
            }

            volume[0] = localHeightSum;
        }
    }

    [BurstCompile]
    struct AdvectionJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float> speedY;
        [ReadOnly]
        public NativeArray<float> volumeToAddPerCell;
        [ReadOnly]
        public float deltaT;
        public NativeArray<Vector3> vertices;

        public void Execute(int i)
        {
            vertices[i] = new Vector3(vertices[i].x, Math.Max(vertices[i].y + volumeToAddPerCell[0] + speedY[i] * deltaT, 0.0f), vertices[i].z);
        }
    }

    [BurstCompile]
    struct ComputeVolumeJob : IJob
    {
        [ReadOnly]
        public uint nbCell;
        [ReadOnly]
        public float targetTotalheight;
        [ReadOnly]
        public NativeArray<float> localHeightArray;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> heightToAddPerCell;

        public void Execute()
        {
            // Aum up all previous jobs volumes
            float computedTotalHeight = 0.0f;
            for (int i = 0; i < localHeightArray.Length; i++)
                computedTotalHeight += localHeightArray[i];

            // Compute height to add (subtract) per cell
            heightToAddPerCell[0] = (targetTotalheight - computedTotalHeight) / (float)nbCell;
        }
    }
}
