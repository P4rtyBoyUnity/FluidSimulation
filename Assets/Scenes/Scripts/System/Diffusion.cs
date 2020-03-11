using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

public class Diffusion : ComponentSystem
{
    const float DIFFUSION_SPEED = 0.05f;

    protected override void OnUpdate()
    {
        Entities.ForEach((ref Translation translation, ref SpeedComponent speed, ref NeighborComponent neighbor) => 
        {
            float transferRate = DIFFUSION_SPEED * Time.DeltaTime;
            Translation prevX = EntityManager.GetComponentData<Translation>(neighbor.entityPrevX);
            Translation nextX = EntityManager.GetComponentData<Translation>(neighbor.entityNextX);
            Translation prevZ = EntityManager.GetComponentData<Translation>(neighbor.entityPrevZ);
            Translation nextZ = EntityManager.GetComponentData<Translation>(neighbor.entityNextZ);
            speed.speed += ((prevX.Value.y + nextX.Value.y + prevZ.Value.y + nextZ.Value.y) / 4.0f - translation.Value.y) * transferRate;
            speed.speed *= 0.999f;
            
        });
    }
}
