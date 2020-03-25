using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public unsafe struct HeightSharedComponent : ISharedComponentData
{
    float diffusionSpeed;
    float viscosity;
}
