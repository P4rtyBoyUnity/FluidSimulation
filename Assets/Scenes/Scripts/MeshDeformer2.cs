using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Unity.Mathematics;

[RequireComponent(typeof(MeshFilter))]
public class MeshDeformer2 : MonoBehaviour
{
    // Start is called before the first frame update
    Mesh            deformingMesh;
    Vector3[]       displacedVertices;

    void Start()
    {
        deformingMesh = GetComponent<MeshFilter>().mesh;
        Vector3[] originalVertices = deformingMesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++)
            displacedVertices[i] = originalVertices[i];
    }

    // Update is called once per frame
    void Update()
    {      
        for (int i = 0; i < displacedVertices.Length; i++)
        {
            
        }
        deformingMesh.vertices = displacedVertices;
        deformingMesh.RecalculateNormals();
    }
}
