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
    private struct Force
    {
        public int index;
        public float acceleration;
    };

    private NativeArray<Entity>     entityArray;
    private EntityManager           entityManager;
    private NativeArray<Vector3>    vertices;
    private List<Force>             forcesToApply = new List<Force>();
    private Entity                  simParams;    

    public EcsSimulation(Neighbor[] neighbor, ref NativeArray<Vector3> vertices, float startingTotalHeight)
    {
        int simSize = vertices.Length;
        this.vertices = vertices;        

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        EntityArchetype simulationArchetype = entityManager.CreateArchetype(typeof(SimulationComponent));
        simParams = entityManager.CreateEntity(simulationArchetype);
        SimulationComponent component;
        component.deltaT = 0.0f;
        component.diffusionSpeed = 0.0f;
        component.viscosity = 0.0f;
        component.volumeToAddPerCell = 0.0f;
        entityManager.SetComponentData<SimulationComponent>(simParams, component);

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
            entityManager.SetComponentData(entity, new NeighborComponent {  /*prevX = neighbor[i].prevX, nextX = neighbor[i].nextX, prevZ = neighbor[i].prevZ, nextZ = neighbor[i].nextZ,*/
                                                                            entityPrevX = prevX, entityNextX = nextX, entityPrevZ = prevZ, entityNextZ = nextZ });
        }
    }

    public void Dispose()
    {
        entityArray.Dispose();
    }

    public void ApplyForce(int indexToApply, float force, float mass)
    {
        forcesToApply.Add(new Force { index = indexToApply, acceleration = force / mass });
    }

    // Phase 1
    public void ApplyForcesToSimulation(float deltaT)
    {
        foreach (Force force in forcesToApply)
        {
            // TODO: Apply to neighbor too
            var component = entityManager.GetComponentData<SpeedComponent>(entityArray[force.index]);
            component.speed += force.acceleration * deltaT;
            entityManager.SetComponentData<SpeedComponent>(entityArray[force.index], component);
        }

        forcesToApply.Clear();
    }

    public void Simulate(float targetTotalheight, float diffusionSpeed, float viscosity, float deltaT)
    {
        // Copy height & compute volume becasue ECS simulation is running AFTER
        float totalHeight = 0.0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            float height = entityManager.GetComponentData<HeightComponent>(entityArray[i]).height;
            vertices[i] = new Vector3(vertices[i].x, height, vertices[i].z);
            totalHeight += height;
        }

        SimulationComponent component;
        component.deltaT = deltaT;
        component.diffusionSpeed = diffusionSpeed;
        component.viscosity = viscosity;
        component.volumeToAddPerCell = (targetTotalheight - totalHeight) / (float)vertices.Length;

        entityManager.SetComponentData<SimulationComponent>(simParams, component);

        UnityEngine.Profiling.Profiler.BeginSample("ApplyForcesToSimulation");
        ApplyForcesToSimulation(deltaT);
        UnityEngine.Profiling.Profiler.EndSample();
    }
        
    public void WaitForVerticeUpdate()
    {
    }
}
