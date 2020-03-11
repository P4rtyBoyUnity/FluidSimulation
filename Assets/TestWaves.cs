using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Unity.Mathematics;

public class TestWaves : MonoBehaviour, IConvertGameObjectToEntity
{   
    public int   arraySize = 20;

    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    [SerializeField] private Material obstacleMaterial;

    [SerializeField] private Mesh meshToDeform;

    NativeArray<Entity> entityArray;
    public EntityManager manager;

    // Return the entity located @ (x, z)
    // If the coordinates are outside the area, return the defaultEntity (yourself)
    private Entity GetEntity(int x, int z, Entity defaultEntity)
    {
        if ((x < 0) || (x >= arraySize))
            return defaultEntity;
        if ((z < 0) || (z >= arraySize))
            return defaultEntity;

        /*
        if((x >= 10) && (x <= 11) && (z >= 9) && (z <= 12))
            return defaultEntity;
        */
        return entityArray[x + z * arraySize];
    }

    public void Convert(Entity inputEntity, EntityManager entityManager, GameObjectConversionSystem conversionSystem)
    {
        Debug.Log("TestWaves::Convert");
        float CUBE_SIZE = 10.0f / arraySize;

        EntityArchetype entityArchetype = entityManager.CreateArchetype(typeof(SpeedComponent),
                                                                        typeof(NeighborComponent),
                                                                        typeof(Translation),
                                                                        typeof(Scale),
                                                                        typeof(Transform),
                                                                        typeof(RenderMesh),
                                                                        typeof(LocalToWorld));

        entityArray = new NativeArray<Entity>(arraySize * arraySize, Allocator.Temp);
        entityManager.CreateEntity(entityArchetype, entityArray);

        for (int i = 0; i < arraySize; i++)
        {
            for (int j = 0; j < arraySize; j++)
            {                
                Entity entity = entityArray[i + j * arraySize];

                Entity prevX = GetEntity(i - 1, j, entity);
                Entity nextX = GetEntity(i + 1, j, entity);
                Entity prevZ = GetEntity(i, j - 1, entity);
                Entity nextZ = GetEntity(i, j + 1, entity);

                // Init Speed @ 0
                entityManager.SetComponentData(entity, new SpeedComponent { speed = 0.0f });
                // Init neighbors
                entityManager.SetComponentData(entity, new NeighborComponent { entityPrevX = prevX, entityNextX = nextX, entityPrevZ = prevZ, entityNextZ = nextZ });
                // Set material
                entityManager.SetSharedComponentData(entity, new RenderMesh { mesh = meshToDeform, material = material, });

                /*
                if ((i >= 10) && (i <= 11) && (j >= 9) && (j <= 12))
                    entityManager.SetSharedComponentData(entity, new RenderMesh { mesh = meshToDeform, material = obstacleMaterial, });
                else
                    entityManager.SetSharedComponentData(entity, new RenderMesh { mesh = meshToDeform, material = material, });
                */
                entityManager.SetComponentData(entity, new Scale { Value = CUBE_SIZE });                

                // Initial wave
                //if ((i == arraySize / 2) && (j == arraySize / 2))
                if ((i == 5) && ((j == arraySize / 2)))
                    entityManager.SetComponentData(entity, new Translation { Value = new float3(i * CUBE_SIZE, 10.0f, j * CUBE_SIZE) });
                else
                    entityManager.SetComponentData(entity, new Translation { Value = new float3(i * CUBE_SIZE, 5.0f, j * CUBE_SIZE) });                    
            }
        }

        //entityArray.Dispose();        
    }
}
