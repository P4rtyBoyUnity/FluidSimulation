using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[RequireComponent(typeof(MeshFilter))]
public class MeshDeformer : MonoBehaviour
{
    // Start is called before the first frame update
    Mesh                deformingMesh;
    Vector3[]           displacedVertices;
    //public TestWaves    testWaves;

    void Start()
    {
        Debug.Log("MeshDeformer::Start");
        deformingMesh = GetComponent<MeshFilter>().mesh;
        Vector3[] originalVertices = deformingMesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
        
        for (int i = 0; i < originalVertices.Length; i++)        
            displacedVertices[i] = originalVertices[i];      
        

    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log("MeshDeformer::Update");
        /*
        var m_Group = testWaves.manager.GetEntityQuery(typeof(SpeedComponent), ComponentType.ReadOnly<SpeedComponent>());
        testWaves.manager.ForEach((ref SpeedComponent i) => {
            i.speed += 1.0f;
        });
            
        */

            for (int i = 0; i < displacedVertices.Length; i++)
        {
            UpdateVertex(i);
        }
        displacedVertices[0].y = 0.5f;
        deformingMesh.vertices = displacedVertices;        
        deformingMesh.RecalculateNormals();
    }

    void UpdateVertex(int i)
    {
        /*
        Vector3 velocity = vertexVelocities[i];
        displacedVertices[i] += velocity * Time.deltaTime;
        */
    }
}
