using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;

public unsafe struct SimulationComponent : IComponentData
{
    public float                    diffusionSpeed;
    public float                    viscosity;
    public float                    volumeToAddPerCell;
    public float                    deltaT;
}
