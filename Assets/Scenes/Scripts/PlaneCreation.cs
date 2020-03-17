using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Contour is adjusted in X & Z, but not UVs
// Contours extremes X (new raycast for X vertexs)
// Jupette
// objects detection & management
// Config pour fixer la shape initale

public class PlaneCreation : MonoBehaviour
{
    public bool         useSurfaceAreaDetection = false;
    public bool         generateSkirt           = false;
    public float        gridResolution          = 0.5f;
    public Vector3      maxRayLength            = new Vector3(100.0f, 100.0f, 100.0f);
    public float        diffusionSpeed          = 20.0f;
    public float        viscosity               = 0.998f;
    public uint         simulationIteration     = 1;

    // Sim Data
    private float       halfResolution          = 0.0f;
    private Vector3     boundingBoxMin;
    private Vector3     boundingBoxMax;
    private int         simStripCount           = 0;
    private int         simSize                 = 0;
    private int         maxStripSize            = 0;
    private float       totalVolume             = 0.0f;

    // Array length == Nb X Values
    private float[]     backLimitList           = new float[0];
    private float[]     frontLimitList          = new float[0];
    private int[]       stripToVerticeLocalOfs  = new int[0];
    private int[]       stripToVerticeGlobalOfs = new int[0];
    private int[]       stripToVerticeCount     = new int[0];

    // Array Length == Nb Vertex/Values Total
    private float[]     speedY                  = new float[0];
    private Vector3[]   vertices                = new Vector3[0];
    private int[]       prevIndexX              = new int[0];
    private int[]       nextIndexX              = new int[0];
    private int[]       prevIndexZ              = new int[0];
    private int[]       nextIndexZ              = new int[0];

    private Mesh        deformingMesh;
    private BoxCollider collider;

    // Start is called before the first frame update
    void Start()
    {
        halfResolution = gridResolution / 2.0f;

        if (useSurfaceAreaDetection)
        {
            // Check limit on the right (X+) & left (X-) side            
            RaycastHit hit;
            boundingBoxMin.x = CastRay(new Ray(transform.position, Vector3.left), maxRayLength.x, out hit) ? hit.point.x : transform.position.x - maxRayLength.x;
            boundingBoxMax.x = CastRay(new Ray(transform.position, Vector3.right), maxRayLength.x, out hit) ? hit.point.x : transform.position.x + maxRayLength.x;
            boundingBoxMin.y = CastRay(new Ray(transform.position, Vector3.down), maxRayLength.y, out hit) ? hit.point.y : transform.position.y - maxRayLength.y;
            boundingBoxMax.y = transform.position.y;

            // Along the ine inbetween leftHit & rightHit, project rays on each side toward Z- & Z+, at resolution distance
            // and fill backLimitList, frontLimitList with the results
            Vector3 leftHit = transform.position;
            leftHit.x = boundingBoxMin.x;
            ComputeZSurface(leftHit, boundingBoxMax.x - boundingBoxMin.x, maxRayLength.z, gridResolution, out backLimitList, out frontLimitList);

            // Compute Bounding box in z            
            boundingBoxMin.z = backLimitList[0];
            boundingBoxMax.z = frontLimitList[0];
            for (int i = 1; i < backLimitList.Length; i++)
            {
                boundingBoxMin.z = Mathf.Min(boundingBoxMin.z, backLimitList[i]);
                boundingBoxMax.z = Mathf.Max(boundingBoxMax.z, frontLimitList[i]);
            }
        }
        else
        {
            // The surface is a rectangle defined by maxRayLengthX & maxRayLengthZ            
            boundingBoxMin = transform.position - maxRayLength;
            boundingBoxMax = transform.position + maxRayLength;
            boundingBoxMax.y = transform.position.y;
            int LimitListLength = 1 + Mathf.CeilToInt((boundingBoxMax.x - boundingBoxMin.x) / gridResolution);
            backLimitList = new float[LimitListLength];
            frontLimitList = new float[LimitListLength];
            for (int i = 0; i < LimitListLength; i++)
            {
                backLimitList[i] = boundingBoxMin.z;
                frontLimitList[i] = boundingBoxMax.z;
            }
        }

        CreateSimData();        

        CreateSurface();

        totalVolume = ComputeVolume();

        vertices[20].y = 2.0f;
    }

    // Update is called once per frame
    void Update()
    {
        for (uint i = 0; i < simulationIteration; i++)
        {
            DiffusionPhase();

            AdvectionPhase();
        }

        //UpdateFloatingObjects();

        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (collider)
            {
                collider.enabled = true;
                if (collider.Raycast(ray, out hit, 100))
                {
                    int index = GetArrayIndexFromPos(hit.point);
                    if(index != -1)
                        speedY[index] -= 50.0f * Time.deltaTime;
                }
                collider.enabled = false;
            }
        }

        deformingMesh.vertices = vertices;
        deformingMesh.RecalculateNormals();
    }

    bool CastRay(Ray ray, float maxDistance, out RaycastHit hit)
    {
        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            /*
            Debug.Log("Hit = (" + hit.point.x + ", " + hit.point.z + ")");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = (hit.point + ray.origin) / 2.0f;
            //cube.transform.localScale = cube.transform.localScale * 0.05f;
            cube.transform.localScale = new Vector3(Mathf.Max(Mathf.Abs(hit.point.x - ray.origin.x), 0.05f), Mathf.Max(Mathf.Abs(hit.point.y - ray.origin.y), 0.05f), Mathf.Max(Mathf.Abs(hit.point.z - ray.origin.z), 0.05f));
            */
            return true;
        }

        return false;
    }

    /// <summary>
    /// Create 2 arrays for minZ/maxZ, which cover the XoZ surface, starting from StartX point, to StartX.x + lengthX. 
    /// Rays are projected at every [resolution] interval along the X axis.
    /// </summary>
    /// <param name="StartX"></param>The starting point of the surface in X
    /// <param name="lengthX"></param>The X length of the surface
    /// <param name="maxZDistance"></param>The maximum distance to project rays in the Z axis
    /// <param name="resolution"></param>The resolution inbetween 2 rays along the X axis
    /// <param name="minZArray"></param>Output value; list of detected collisions toward Z-
    /// <param name="maxZArray"></param>Output value; list of detected collisions toward Z+
    /// <returns></returns>
    int ComputeZSurface(Vector3 StartX, float lengthX, float maxZDistance, float resolution, out float[] minZArray, out float[] maxZArray)
    {
        int LimitListLength =  1 + Mathf.CeilToInt(lengthX / resolution);

        minZArray = new float[LimitListLength];
        maxZArray = new float[LimitListLength];

        Vector3 indexPos = StartX;
        for (int i = 1; i < LimitListLength-1; i++)
        {
            indexPos.x = StartX.x + Mathf.Min((float)i * resolution, lengthX);

            RaycastHit hit;
            minZArray[i] = CastRay(new Ray(indexPos, Vector3.back), maxZDistance, out hit) ? hit.point.z : indexPos.z - maxZDistance;
            maxZArray[i] = CastRay(new Ray(indexPos, Vector3.forward), maxZDistance, out hit) ? hit.point.z : indexPos.z + maxZDistance;
        }

        minZArray[0] = minZArray[1];
        maxZArray[0] = maxZArray[1];
        minZArray[LimitListLength - 1] = minZArray[LimitListLength - 2];
        maxZArray[LimitListLength - 1] = maxZArray[LimitListLength - 2];

        return LimitListLength;
    }

    int GetVerticeIndex(int x, int z, int defaultVal = -1)
    {
        if ((x < 0) || (x >= simStripCount))
            return defaultVal;
        if((z < stripToVerticeLocalOfs[x]) || (z >= (stripToVerticeLocalOfs[x] + stripToVerticeCount[x])))
            return defaultVal;
        return stripToVerticeGlobalOfs[x] + z - stripToVerticeLocalOfs[x];
    }

    void CreateSimData()
    {
        simStripCount = backLimitList.Length;

        stripToVerticeGlobalOfs = new int[simStripCount];
        stripToVerticeLocalOfs = new int[simStripCount];
        stripToVerticeCount = new int[simStripCount];

        int referenceOfs = Mathf.CeilToInt((transform.position.z - boundingBoxMin.z - halfResolution) / gridResolution);

        // Evaluate each strip
        simSize = 0;
        maxStripSize = 0;
        for (int i = 0; i < simStripCount; i++)
        {
            float backZ = backLimitList[i];
            float frontZ = frontLimitList[i];

            if( i > 0)
            {
                backZ = Mathf.Min(backZ, backLimitList[i - 1]);
                frontZ = Mathf.Max(frontZ, frontLimitList[i - 1]);
            }
            if (i < simStripCount-1)
            {
                backZ = Mathf.Min(backZ, backLimitList[i + 1]);
                frontZ = Mathf.Max(frontZ, frontLimitList[i + 1]);
            }

            int nbBackPoints = Mathf.CeilToInt((transform.position.z - backZ - halfResolution) / gridResolution);
            int nbFrontPoints = Mathf.CeilToInt((frontZ - transform.position.z - halfResolution) / gridResolution);

            stripToVerticeLocalOfs[i] = referenceOfs - nbBackPoints;
            stripToVerticeGlobalOfs[i] = simSize;
            stripToVerticeCount[i] = nbBackPoints + nbFrontPoints + 2;
            simSize += stripToVerticeCount[i];

            maxStripSize = Mathf.Max(maxStripSize, stripToVerticeLocalOfs[i] + stripToVerticeCount[i]);
        }

        /*
        for (int i = 0; i < simStripCount; i++)
            Debug.Log("Strip " + i + " : Ofs=" + stripToVerticeGlobalOfs[i] + ", Count=" + stripToVerticeCount[i] + ", LocalOfs=" + stripToVerticeLocalOfs[i]);
        */

        // Allocate for the whole sim
        // Array Length == Nb Vertex/Values Total
        speedY = new float[simSize];
        prevIndexX = new int[simSize];
        nextIndexX = new int[simSize];
        prevIndexZ = new int[simSize];
        nextIndexZ = new int[simSize];

        // Init sim
        for (int x = 0; x < simStripCount; x++)
        {
            for (int z = 0; z < maxStripSize; z++)
            {
                int index = GetVerticeIndex(x, z);
                if (index >= 0)
                {                    
                    prevIndexZ[index] = GetVerticeIndex(x, z - 1, index);
                    nextIndexZ[index] = GetVerticeIndex(x, z + 1, index);
                    prevIndexX[index] = GetVerticeIndex(x - 1, z, index);
                    nextIndexX[index] = GetVerticeIndex(x + 1, z, index);
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
                    if((prevIndexZ[index] != index) && (prevIndexZ[index] != nextIndexZ[index]))
                    prevIndexZ[index] = GetVerticeIndex(x, z - 1, index);
                    nextIndexZ[index] = GetVerticeIndex(x, z + 1, index);
                    prevIndexX[index] = GetVerticeIndex(x - 1, z, index);
                    nextIndexX[index] = GetVerticeIndex(x + 1, z, index);
                    //Debug.Log("index " + index + " = " + prevIndexZ[index] + ", " + nextIndexZ[index] + ", " + prevIndexX[index] + ", " + nextIndexX[index]);
                }
            }
        }    
}

    void CreateSurface()
    {
        vertices = new Vector3[simSize];
        Vector2[] uvs = new Vector2[simSize];
        int[] triangles = new int[simSize * 6];
        
        int triangleIndex = 0;
        Vector3 halfBoundingBoxSize = boundingBoxMin;

        for (int x = 0; x < simStripCount; x++)
        {
            for (int z = 0; z < maxStripSize; z++)
            {
                int index = GetVerticeIndex(x, z);
                if (index >= 0)
                {
                    // Set Vertex
                    vertices[index] = new Vector3((float)x * gridResolution, boundingBoxMax.y - boundingBoxMin.y, (float)z * gridResolution - halfResolution);

                    // Adjust tight contour
                    vertices[index].z = Mathf.Clamp(vertices[index].z, backLimitList[x] - boundingBoxMin.z, frontLimitList[x] - boundingBoxMin.z);

                    //////////// DEBUG
                    /*
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Vector3 spherePos = vertices[index];
                    spherePos.y = transform.position.y;
                    sphere.transform.position = spherePos;
                    sphere.transform.localScale = sphere.transform.localScale * 0.1f;
                    */
                    ///////////////


                    // SetUV
                    uvs[index].x = (float)x / (float)(simStripCount - 1);
                    uvs[index].y = (float)z / (float)(maxStripSize - 1);

                    // Set Triangles
                    int nextXindex = GetVerticeIndex(x + 1, z);
                    int nextZindex = GetVerticeIndex(x, z + 1);
                    int nextXZindex = GetVerticeIndex(x + 1, z + 1);

                    if((nextXindex != -1) && (nextZindex != -1) && (nextXZindex != -1))
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

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if(!meshFilter)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        deformingMesh = new Mesh();
        deformingMesh.vertices = vertices;
        deformingMesh.uv = uvs;
        deformingMesh.triangles = triangles;
        deformingMesh.RecalculateNormals();
        meshFilter.mesh = deformingMesh;

        //meshFilter.transform.position = new Vector3(transform.position.x, boundingBoxMin.y, transform.position.z);
        meshFilter.transform.position = boundingBoxMin;

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

        // Create collider
        collider = gameObject.AddComponent<BoxCollider>();
        Vector3 center = (boundingBoxMin + boundingBoxMax) / 2.0f;        
        collider.center = center - transform.position;
        collider.size = boundingBoxMax - boundingBoxMin;
        collider.size = new Vector3(Mathf.Abs(collider.size.x), Mathf.Abs(collider.size.y), Mathf.Abs(collider.size.z));

    }

    float ComputeVolume()
    {
        float Result = 0.0f;

        // Diffusion Phase
        for (int i = 0; i < simSize; i++)
                Result += vertices[i].y;
        return Result;
    }

    void DiffusionPhase()
    {
        // Diffusion Phase
        for (int i = 0; i < simSize; i++)
        {
            float transferRate = diffusionSpeed * Time.deltaTime;
            speedY[i] += ((vertices[prevIndexX[i]].y + vertices[nextIndexX[i]].y + vertices[prevIndexZ[i]].y + vertices[nextIndexZ[i]].y) / 4.0f - vertices[i].y) * transferRate;
            speedY[i] *= viscosity;            
        }
    }

    void AdvectionPhase()
    {
        float volumeToAddPerCell = (totalVolume - ComputeVolume()) / (float)simSize;

        // Advection Phase
        for (int i = 0; i < simSize; i++)
        {
            vertices[i].y += volumeToAddPerCell + speedY[i] * Time.deltaTime;
            // check for bottom
            if (vertices[i].y < 0.0f)
                vertices[i].y = 0.0f;
        }
    }

    int GetArrayIndexFromPos(Vector3 pos)
    {
        int x = (int)((pos.x - boundingBoxMin.x) / gridResolution);
        int z = (int)((pos.z - boundingBoxMin.z) / gridResolution);
        return GetVerticeIndex(x, z);
    }

    int GetArrayIndexFromZPos(float pz)
    {
        //return planeLength - (int)(pz + 6.5f - transform.position.z);
        return 0;
    }

    int GetArrayIndexFromXPos(float px, out float weight)
    {
        /*
        float pos = (float)planeWidth - (px + 6.0f - transform.position.x);
        int result = (int)pos;
        weigth = 1.0f - (pos - (float)result);
        return result;
        */
        weight = 0.0f;
        return 0;
    }

    int GetArrayIndexFromZPos(float pz, out float weight)
    {
        /*
        float pos = (float)planeLength - (pz + 6.0f - transform.position.z);
        int result = (int)pos;
        weigth = 1.0f - (pos - (float)result);
        return result;
        */
        weight = 0.0f;
        return 0;
    }
}

