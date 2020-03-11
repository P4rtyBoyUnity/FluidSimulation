using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct NeighborComponent : IComponentData
{
    public Entity entityPrevX;
    public Entity entityNextX;
    public Entity entityPrevZ;
    public Entity entityNextZ;
}
