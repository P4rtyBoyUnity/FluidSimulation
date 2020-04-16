using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;



// This class contains the simulation data for a surface composed of quads
public class SimDataQuad
{    
    public struct StripData
    {
        public int localOffset;         // Nb of sim points not in simulation
        public int globalOffset;        // Straing sim point index
        public int count;               // Nb of sim points in the strip
    }

    public StripData[] stripToVerticeData { get; private set; } = new StripData[0];
    public Neighbor[] neighbor { get; private set; } = new Neighbor[0];
    public int maxStripSize { get; private set; } = 0;
    public int simSize { get; private set; } = 0;

    private float   gridResolution;
    private Vector3 pos;
    private Vector3 boundingBoxMin;
    private Vector3 boundingBoxMax;

    public SimDataQuad(Vector3 pos, float gridResolution, Vector3 boundingBoxMin, Vector3 boundingBoxMax, SurfaceDelimitation[] surfaceLimits)
    {
        this.pos = pos;        
        this.gridResolution = gridResolution;
        this.boundingBoxMin = boundingBoxMin;
        this.boundingBoxMax = boundingBoxMax;

        // Only pos used is pos.z
        // only boundingBoxMin used is .z

        float halfGridResolution = gridResolution / 2.0f;
        int simStripCount = surfaceLimits.Length;

        // Allocate all strips data
        stripToVerticeData = new StripData[simStripCount];

        // Compute reference offset
        int referenceOfs = Mathf.CeilToInt((pos.z - boundingBoxMin.z - halfGridResolution) / gridResolution);

        // Evaluate each strip
        simSize = 0;
        maxStripSize = 0;
        for (int i = 0; i < simStripCount; i++)
        {
            float backZ = surfaceLimits[i].back;
            float frontZ = surfaceLimits[i].front;

            if (i > 0)
            {
                backZ = Mathf.Min(backZ, surfaceLimits[i - 1].back);
                frontZ = Mathf.Max(frontZ, surfaceLimits[i - 1].front);
            }
            if (i < simStripCount - 1)
            {
                backZ = Mathf.Min(backZ, surfaceLimits[i + 1].back);
                frontZ = Mathf.Max(frontZ, surfaceLimits[i + 1].front);
            }

            int nbBackPoints = Mathf.CeilToInt((pos.z - backZ - halfGridResolution) / gridResolution);
            int nbFrontPoints = Mathf.CeilToInt((frontZ - pos.z - halfGridResolution) / gridResolution);

            stripToVerticeData[i].localOffset = referenceOfs - nbBackPoints;
            stripToVerticeData[i].globalOffset = simSize;
            stripToVerticeData[i].count = nbBackPoints + nbFrontPoints + 2;
            simSize += stripToVerticeData[i].count;

            maxStripSize = Mathf.Max(maxStripSize, stripToVerticeData[i].localOffset + stripToVerticeData[i].count);
        }

        // Allocate for the whole sim
        // Array Length == Nb Vertex/Values Total
        //speedY = new float[simSize];
        neighbor = new Neighbor[simSize];

        // Init sim
        for (int x = 0; x < simStripCount; x++)
        {
            for (int z = 0; z < maxStripSize; z++)
            {
                int index = GetVerticeIndex(x, z);
                if (index >= 0)
                {
                    neighbor[index].prevZ = GetVerticeIndex(x, z - 1, index);
                    neighbor[index].nextZ = GetVerticeIndex(x, z + 1, index);
                    neighbor[index].prevX = GetVerticeIndex(x - 1, z, index);
                    neighbor[index].nextX = GetVerticeIndex(x + 1, z, index);
                    //Debug.Log("index " + index + " = " + prevIndexZ[index] + ", " + nextIndexZ[index] + ", " + prevIndexX[index] + ", " + nextIndexX[index]);
                }
            }
        }

        for (int x = 0; x < simStripCount; x++)
        {
            for (int z = 0; z < maxStripSize; z++)
            {
                int index = GetVerticeIndex(x, z);
                if (index >= 0)
                {
                    if ((neighbor[index].prevZ != index) && (neighbor[index].prevZ != neighbor[index].nextZ))
                        neighbor[index].prevZ = GetVerticeIndex(x, z - 1, index);
                    neighbor[index].nextZ = GetVerticeIndex(x, z + 1, index);
                    neighbor[index].prevX = GetVerticeIndex(x - 1, z, index);
                    neighbor[index].nextX = GetVerticeIndex(x + 1, z, index);
                }
            }
        }
    }

    // Return an index, based on a local int position
    // return defaultVal if (x, z) is outside bound or doesn't have simulation point
    public int GetVerticeIndex(int x, int z, int defaultVal = -1)
    {
        if ((x < 0) || (x >= stripToVerticeData.Length))
            return defaultVal;
        if ((z < stripToVerticeData[x].localOffset) || (z >= (stripToVerticeData[x].localOffset + stripToVerticeData[x].count)))
            return defaultVal;
        return stripToVerticeData[x].globalOffset + z - stripToVerticeData[x].localOffset;
    }

    public Mesh CreateSurface([ReadOnly] SurfaceDelimitation[] surfaceLimits, out NativeArray<Vector3> simVertices, bool debugSurface = false)
    {
        // Only pos used = pos.y
        simVertices = new NativeArray<Vector3>(simSize, Allocator.Persistent);
        int[] triangles = new int[simSize * 6];

        int triangleIndex = 0;
        Vector3 halfBoundingBoxSize = boundingBoxMin;

        for (int x = 0; x < stripToVerticeData.Length; x++)
        {
            for (int z = 0; z < maxStripSize; z++)
            {
                int index = GetVerticeIndex(x, z);
                if (index >= 0)
                {
                    // Set Vertex
                    Vector3 vector = GetVertexLocalPos(x, z);
                    // Adjust tight contour
                    vector.z = Mathf.Clamp(vector.z, surfaceLimits[x].back - boundingBoxMin.z, surfaceLimits[x].front - boundingBoxMin.z);

                    simVertices[index] = vector;

                    //////////// DEBUG
                    if (debugSurface)
                    {
                        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        Vector3 spherePos = vector;
                        spherePos.y = pos.y;
                        sphere.transform.position = spherePos;
                        sphere.transform.localScale = sphere.transform.localScale * 0.1f;
                    }
                    ///////////////

                    // Set Triangles
                    int nextXindex = GetVerticeIndex(x + 1, z);
                    int nextZindex = GetVerticeIndex(x, z + 1);
                    int nextXZindex = GetVerticeIndex(x + 1, z + 1);

                    if ((nextXindex != -1) && (nextZindex != -1) && (nextXZindex != -1))
                    {
                        triangles[triangleIndex++] = index;
                        triangles[triangleIndex++] = nextZindex;
                        triangles[triangleIndex++] = nextXZindex;

                        triangles[triangleIndex++] = index;
                        triangles[triangleIndex++] = nextXZindex;
                        triangles[triangleIndex++] = nextXindex;
                    }
                }
            }            
        }

        int[] triangles2 = new int[triangleIndex];
        for (int i = 0; i < triangleIndex / 3; i ++)
        {
            triangles2[i*3+0] = triangles[i * 3 + 0];
            triangles2[i*3+1] = triangles[i * 3 + 1];
            triangles2[i*3+2] = triangles[i * 3 + 2];
        }

        // Create the mesh!
        Mesh deformingMesh = new Mesh();
        deformingMesh.vertices = simVertices.ToArray();
        //deformingMesh.uv = uvs;
        deformingMesh.triangles = triangles2;
        deformingMesh.RecalculateNormals();

        ///////////// DEBUG
        /*
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = (boundingBoxMin + boundingBoxMax) / 2.0f;
        cube.transform.localScale = boundingBoxMax - boundingBoxMin;

        Debug.Log("Debug box = " + boundingBoxMin + " - " + boundingBoxMax);
        Debug.Log("Mesh Filter = " + meshFilter.transform.position);
        Debug.Log("Transform   = " + transform.position);
        Debug.Log("Cube        = Pos:" + cube.transform.position + ", Scale:" + cube.transform.localScale);
        */
        //////////
        ///
        return deformingMesh;
    }

    public int[] GetContourIndex([ReadOnly] SurfaceDelimitation[] surfaceLimits)
    {
        int lastStripIndex = stripToVerticeData.Length - 1;
        int[] result = new int[stripToVerticeData.Length * 2 + stripToVerticeData[0].count + stripToVerticeData[lastStripIndex].count];
        
        int index = 0;
        for (int i = 0; i < stripToVerticeData.Length; i++)
            result[index++] = stripToVerticeData[0].globalOffset;
        for (int i = 0; i < stripToVerticeData[lastStripIndex].count; i++)
            result[index++] = stripToVerticeData[lastStripIndex].globalOffset + i;
        for (int i = 0; i < stripToVerticeData.Length; i++)
            result[index++] = stripToVerticeData[lastStripIndex - i].globalOffset;
        for (int i = 0; i < stripToVerticeData[0].count; i++)
            result[index++] = stripToVerticeData[0].globalOffset + stripToVerticeData[0].globalOffset - 1 - i;

        return result;
    }

    public enum TextureTilingType
    {
        Stretch,
        Tile,
        Random
    };

    public Vector2[] ComputeUVs(NativeArray<Vector3> simVertices, TextureTilingType tiling = TextureTilingType.Tile)
    {
        Vector2[] uvs = new Vector2[simSize];

        for (int x = 0; x < stripToVerticeData.Length; x++)
        {
            for (int z = 0; z < maxStripSize; z++)
            {
                int index = GetVerticeIndex(x, z);
                if (index >= 0)
                {
                    // SetUV
                    if (tiling == TextureTilingType.Stretch)
                    {
                        uvs[index].x = simVertices[index].x / (float)(stripToVerticeData.Length - 1);
                        uvs[index].y = simVertices[index].z / (float)(maxStripSize - 1);
                    }
                    else if (tiling == TextureTilingType.Random)
                    {
                        uvs[index].x = Random.Range(0.0f, 1.0f);
                        uvs[index].y = Random.Range(0.0f, 1.0f);
                    }
                    else
                    {
                        uvs[index].x = x & 0x1;
                        uvs[index].y = z & 0x1;
                    }                   

                }
            }
        }

        return uvs;
    }

    public void SetBoxCollider(Vector3 boxPos, ref BoxCollider boxCollider)
    {
        Vector3 center = (boundingBoxMin + boundingBoxMax) / 2.0f;
        boxCollider.center = center - boxPos;
        boxCollider.size = boundingBoxMax - boundingBoxMin;
        boxCollider.size = new Vector3(Mathf.Abs(boxCollider.size.x), Mathf.Abs(boxCollider.size.y), Mathf.Abs(boxCollider.size.z));
        boxCollider.isTrigger = true;
    }

    // Return a local position relative to boundingBoxMin, from a (x, z) couple
    private Vector3 GetVertexLocalPos(int x, int z)
    {
        return new Vector3((float)x * gridResolution, boundingBoxMax.y - boundingBoxMin.y, (float)z * gridResolution - gridResolution / 2.0f);
    }       
}
