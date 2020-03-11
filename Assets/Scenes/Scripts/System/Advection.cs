using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

[UpdateAfter(typeof(Diffusion))]
public class Advection : ComponentSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref Translation translation, ref SpeedComponent speed) => {
            translation.Value.y += speed.speed;
        });
    }
}
