using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public unsafe struct HeightSharedComponent : ISharedComponentData
{
    public fixed float Height[400];
}
