using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;

public class Diffusion : ComponentSystem
{
    [BurstCompile]
    protected override void OnUpdate()
    {
        // Reading simulation config
        EntityQuery m_Group = GetEntityQuery(typeof(SimulationComponent));
        var simValues = m_Group.ToComponentDataArray<SimulationComponent>(Allocator.TempJob);

        if (simValues.Length > 0)
        {
            // TODO: height & neighbor are read-only
            Entities.ForEach((ref HeightComponent height, ref SpeedComponent speed, ref NeighborComponent neighbor) =>
            {
                float transferRate = simValues[0].diffusionSpeed * simValues[0].deltaT;
                float prevX = EntityManager.GetComponentData<HeightComponent>(neighbor.entityPrevX).height;
                float nextX = EntityManager.GetComponentData<HeightComponent>(neighbor.entityNextX).height;
                float prevZ = EntityManager.GetComponentData<HeightComponent>(neighbor.entityPrevZ).height;
                float nextZ = EntityManager.GetComponentData<HeightComponent>(neighbor.entityNextZ).height;
                speed.speed += ((prevX + nextX + prevZ + nextZ) / 4.0f - height.height) * transferRate;
                speed.speed *= simValues[0].viscosity;
            });
        }
    }
}

/*
using Unity.Jobs;
using System.ComponentModel;

public class Diffusion : JobComponentSystem
{
    private struct DiffusionJob : IJobForEach<HeightComponent, SpeedComponent, NeighborComponent> 
    {
        public float deltaT;
        public float diffusionSpeed;

        public void Execute([ReadOnly] ref HeightComponent height, ref SpeedComponent speed, [ReadOnly] ref NeighborComponent neighbor)
        {
            float transferRate = diffusionSpeed * deltaT;
            float prevX = EntityManager.GetComponentData<HeightComponent>(neighbor.entityPrevX).height;
            float nextX = EntityManager.GetComponentData<HeightComponent>(neighbor.entityNextX).height;
            float prevZ = EntityManager.GetComponentData<HeightComponent>(neighbor.entityPrevZ).height;
            float nextZ = EntityManager.GetComponentData<HeightComponent>(neighbor.entityNextZ).height;
            speed.speed += ((prevX + nextX + prevZ + nextZ) / 4.0f - height.height) * transferRate;
            speed.speed *= 0.998f;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        Job job = new Job();
        return job.Schedule(this, inputDeps);
    }

}
*/
