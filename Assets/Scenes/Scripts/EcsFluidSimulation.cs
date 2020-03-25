using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;

[System.Serializable]
public class EcsSimulation : FluidSimulationInterface
{
    private NativeArray<Entity> entityArray;
    private EntityManager entityManager;
    private Vector3[] vertices = null;

    public EcsSimulation(Neighbor[] neighbor, ref Vector3[] vertices, float diffusionSpeed, float viscosity)
    {
        int simSize = neighbor.Length;
        this.vertices = vertices;

        /*EntityManager*/
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype entityArchetype = entityManager.CreateArchetype(typeof(SpeedComponent),
                                                                        typeof(HeightComponent),
                                                                        typeof(NeighborComponent));

        entityArray = new NativeArray<Entity>(simSize, Allocator.Persistent);
        entityManager.CreateEntity(entityArchetype, entityArray);

        for (int i = 0; i < simSize; i++)
        {
            Entity entity = entityArray[i];

            Entity prevX = entityArray[neighbor[i].prevX];
            Entity nextX = entityArray[neighbor[i].nextX];
            Entity prevZ = entityArray[neighbor[i].prevZ];
            Entity nextZ = entityArray[neighbor[i].nextZ];

            // Init Speed @ 0
            entityManager.SetComponentData(entity, new SpeedComponent { speed = 0.0f });
            // Init height
            entityManager.SetComponentData(entity, new HeightComponent { height = vertices[i].y });
            // Init neighbors
            entityManager.SetComponentData(entity, new NeighborComponent { entityPrevX = prevX, entityNextX = nextX, entityPrevZ = prevZ, entityNextZ = nextZ });
            // Set material
            //entityManager.SetSharedComponentData(entity, new RenderMesh { mesh = meshToDeform, material = material, });
        }
    }

    ~EcsSimulation()
    {
        entityArray.Dispose();
    }

    public void Advection(float volumeToAddPerCell)
    {
        // All advection is done by ECS

        // Only thing left to do; add volume lost/gained
        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y += volumeToAddPerCell;
    }

    public void Diffusion()
    {
        // Nothing to do, done in ECS
    }

    public void ApplyToMesh()
    {
        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y = entityManager.GetComponentData<HeightComponent>(entityArray[i]).height;
    }

    public float GetSpeed(int index)
    {
        //TODO
        return 0.0f;
    }

    public void SetSpeed(int index, float speed)
    {
        //TODO
    }
}
