using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;

[UpdateAfter(typeof(Diffusion))]
public class Advection : JobComponentSystem
{
    [BurstCompile]
    struct AdvectionJob : IJobForEach<HeightComponent, SpeedComponent>
    {
        public float deltaT;
        public float volumeToAddPerCell;

        public void Execute(ref HeightComponent height, [ReadOnly] ref SpeedComponent speed)
        {
            height.height = System.Math.Max(height.height + volumeToAddPerCell + speed.speed * deltaT, 0.0f);            
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        // Reading simulation config
        EntityQuery m_Group = GetEntityQuery(typeof(SimulationComponent));
        var simValues = m_Group.ToComponentDataArray<SimulationComponent>(Allocator.TempJob);
        AdvectionJob advectionJob;

        if (simValues.Length > 0)
            advectionJob = new AdvectionJob { deltaT = simValues[0].deltaT, volumeToAddPerCell = simValues[0].volumeToAddPerCell };
        else
            advectionJob = new AdvectionJob { deltaT = 0.0f, volumeToAddPerCell = 0.0f };

        simValues.Dispose();

        return advectionJob.Schedule(this, inputDeps);
    }
}

