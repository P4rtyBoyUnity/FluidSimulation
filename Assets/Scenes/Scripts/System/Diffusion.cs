using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;


public class Diffusion : ComponentSystem
{
    const float DIFFUSION_SPEED = 20.0f;

    protected override void OnUpdate()
    {
        // TODO: height & neighbor are read-only
        Entities.ForEach((ref HeightComponent height, ref SpeedComponent speed, ref NeighborComponent neighbor) => 
        {
            float transferRate = DIFFUSION_SPEED * Time.DeltaTime;
            float prevX = EntityManager.GetComponentData<HeightComponent>(neighbor.entityPrevX).height;
            float nextX = EntityManager.GetComponentData<HeightComponent>(neighbor.entityNextX).height;
            float prevZ = EntityManager.GetComponentData<HeightComponent>(neighbor.entityPrevZ).height;
            float nextZ = EntityManager.GetComponentData<HeightComponent>(neighbor.entityNextZ).height;
            speed.speed += ((prevX + nextX + prevZ + nextZ) / 4.0f - height.height) * transferRate;
            speed.speed *= 0.998f;            
        });
    }
}

/*
using Unity.Jobs;

public class Diffusion : JobComponentSystem
{
    const float DIFFUSION_SPEED = 20.0f;

    private struct Job : IJobProcessComponentData<HeightComponent, SpeedComponent, NeighborComponent> // can take up to three components <T0, T1, T2>
    {
        public void Execute(ref HeightComponent height, ref SpeedComponent speed, ref NeighborComponent neighbor)
        {
            float transferRate = DIFFUSION_SPEED * Time.DeltaTime;
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