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
        Entities.ForEach((ref HeightComponent height, ref SpeedComponent speed) => {
            height.height = Mathf.Max(height.height + speed.speed * Time.DeltaTime, 0.0f);            
        });
    }
}
