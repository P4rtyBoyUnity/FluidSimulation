using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * ECS Simulation does not take viscosity, diffspeed into account & ammount to add per cell
 * Fluid level should be different than the one at init time (so if the volume rise, we dont see the contour)
 * - Separate Plane creation/Simulation & Object interaction (with same API for ECS, DFG, & Mono)
    - Water level can change if immerged object (check for places where we assume water level)
        - we must also change bounding box
    - Manage many types of objects shape (EvaluateObjectVolume...)
    - Improved objects physics
        - Manage splash
            - Wave in direction of impact
            - Objects slowdown
        - Consider object mass & volume for floating
        - when detecting surface level, take 5 measures (each bouding box corner + middle?) for object larger than grid
    - Skirt
    - Create a frixed grid for inital config? (not good for parallel compute)
*/

public class PlaneCreation : MonoBehaviour
{
    // 3 Types of simulation right now;
    // - Reference  : Classic C3 simulation
    // - ECS        : DOTS based simulation using ECS
    // - DFG        : Simulation done with Data Flow Graph tech
    public enum SimType { Reference, Ecs, Dfg };

    public SimType simulationType = SimType.Reference;
    public bool useSurfaceAreaDetection = false;
    public bool generateSkirt = false;
    public float gridResolution = 0.5f;
    public Vector3 maxRayLength = new Vector3(100.0f, 100.0f, 100.0f);
    public float diffusionSpeed = 20.0f;
    public float viscosity = 0.998f;
    public uint simulationIteration = 1;

    // Sim Data
    private FluidSimulationInterface simulation = null;
    private float halfResolution = 0.0f;
    private Vector3 boundingBoxMin;
    private Vector3 boundingBoxMax;
    private int simStripCount = 0;
    private int simSize = 0;
    private int maxStripSize = 0;
    private float totalVolume = 0.0f;

    // Array length == Nb X Values
    private float[] backLimitList = new float[0];
    private float[] frontLimitList = new float[0];
    private int[] stripToVerticeLocalOfs = new int[0];
    private int[] stripToVerticeGlobalOfs = new int[0];
    private int[] stripToVerticeCount = new int[0];

    // Array Length == Nb Vertex/Values Total
    private Vector3[] vertices = new Vector3[0];
    private Neighbor[] neighbor = new Neighbor[0];

    private Mesh deformingMesh;
    private BoxCollider simCollider;

    // Floating objects
    class PhysicObject
    {
        public GameObject gao;
        public Vector3 massCenterDelta;
        public float volume;

        // updated every frame
        public float submergedVolume;
    }
    private List<PhysicObject> physicObjects = new List<PhysicObject>();

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

        // Compute volume
        totalVolume = ComputeVolume();

        vertices[25].y += 2.0f;

        if (simulationType == SimType.Ecs)
            simulation = new EcsSimulation(neighbor, ref vertices, diffusionSpeed, viscosity);
        else if (simulationType == SimType.Dfg)
            simulation = new DfgSimulation(neighbor, ref vertices, diffusionSpeed, viscosity);
        else
            simulation = new ReferenceFluidSimulation(neighbor, ref vertices, diffusionSpeed, viscosity);
    }

    // Update is called once per frame
    void Update()
    {
        for (uint i = 0; i < simulationIteration; i++)
        {
            simulation.Diffusion();

            simulation.Advection(ComputeVolumeToAddPerCell());
        }

        simulation.ApplyToMesh();

        /*
        if (!UseEcs)
        {
            for (uint i = 0; i < simulationIteration; i++)
            {
                DiffusionPhase();

                AdvectionPhase();
            }
        }
        else
        {            
            for (int i = 0; i < simSize; i++)
                vertices[i].y = entityManager.GetComponentData<HeightComponent>(entityArray[i]).height;
        }
    */

        UpdateFloatingObjects();        

        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (simCollider)
            {
                simCollider.enabled = true;
                if (simCollider.Raycast(ray, out hit, 100))
                {
                    int index = GetArrayIndexFromPos(hit.point);
                    if (index != -1)
                        simulation.SetSpeed(index, simulation.GetSpeed(index) - 50.0f * Time.deltaTime);
                }
                simCollider.enabled = false;
            }
        }

        deformingMesh.vertices = vertices;
        deformingMesh.RecalculateNormals();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Enter! " + other.gameObject.name);
        AddNewGameObject(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger Exit! " + other.gameObject.name);
    }

    bool CastRay(Ray ray, float maxDistance, out RaycastHit hit)
    {
        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            //////////// DEBUG            
            /*
            Debug.Log("Hit = (" + hit.point.x + ", " + hit.point.z + ")");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = (hit.point + ray.origin) / 2.0f;
            //cube.transform.localScale = cube.transform.localScale * 0.05f;
            cube.transform.localScale = new Vector3(Mathf.Max(Mathf.Abs(hit.point.x - ray.origin.x), 0.05f), Mathf.Max(Mathf.Abs(hit.point.y - ray.origin.y), 0.05f), Mathf.Max(Mathf.Abs(hit.point.z - ray.origin.z), 0.05f));
            */
            /////////

            return true;
        }

        return false;
    }

    void UpdateFloatingObjects()
    {
        for (int i = 0; i < physicObjects.Count; i++)
        {
            // PARAMS FOR OBJECTS
            // How much they are in the water
            // How fast they rotate
            float y;
            Vector3 normal = GetYNormal(physicObjects[i].gao.transform.position.x, physicObjects[i].gao.transform.position.z, out y);

            var rigidBody = physicObjects[i].gao.GetComponent<Rigidbody>();
            if (rigidBody)
            {
                /*
                rigidBody.isKinematic = true;                
                physicObjects[i].transform.position = new Vector3(physicObjects[i].transform.position.x, y, physicObjects[i].transform.position.z);
                physicObjects[i].transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                */


                if (y > 0.0f)
                {
                    float mv = physicObjects[i].volume / rigidBody.mass;

                    float height = 0.0f;
                    var objCollider = physicObjects[i].gao.GetComponent<Collider>();
                    if (objCollider)
                        height = objCollider.bounds.size.y;

                    Debug.Log("Mass=" + rigidBody.mass + ", volume=" + physicObjects[i].volume + ", mv=" + mv + ", height = " + height);

                    float PercentageInFluid = 1.0f - (physicObjects[i].gao.transform.position.y - y + (height / 2.0f)) / height;
                    float Ratio = 1.0f - physicObjects[i].gao.transform.position.y / y;

                    Debug.Log("pos = " + physicObjects[i].gao.transform.position.y + ", y=" + y + ", Ratio = " + Ratio + ", Height=" + PercentageInFluid);

                    // compute mass in fluid 
                    float massInsideFluid = PercentageInFluid * physicObjects[i].volume;  //1.0 = liquid vm
                    float massOutsideFluid = (1.0f - PercentageInFluid) * rigidBody.mass;
                    float archimedRatio = massInsideFluid / (massInsideFluid + massOutsideFluid);
                    
                    // Update submerged volume
                    float newSubmergedVolume = PercentageInFluid * physicObjects[i].volume;
                    float deltaSubmergedVolume = newSubmergedVolume - physicObjects[i].submergedVolume;
                    physicObjects[i].submergedVolume = newSubmergedVolume;

                    Debug.Log("Mass in fluid=" + massInsideFluid + ", Mass outside fluid= " + massOutsideFluid + ", ArchiRatio=" + archimedRatio + ", Volume=" + physicObjects[i].volume);

                    // submerge volume effect
                    int index = GetArrayIndexFromPos(physicObjects[i].gao.transform.position);
                    /*
                    if (index >= 0)
                        speedY[index] += deltaSubmergedVolume * 2.0f;
                    */

                    /* physic effect temporarily removed 
                    if(index >= 0)
                        simulation.SetSpeed(index, simulation.GetSpeed(index) + deltaSubmergedVolume * 2.0f);

                    rigidBody.AddForce((0.2f * archimedRatio * mv * normal * Physics.gravity.magnitude), ForceMode.Force);
                    if (rigidBody.velocity.y < 0.0f)
                        rigidBody.velocity = rigidBody.velocity * 0.98f;
                        */

                    /*
                    if (Ratio > 0.0f)
                    {
                        rigidBody.AddForce(mv * normal * Physics.gravity.magnitude * (0.25f + Ratio * 0.25f), ForceMode.Force);
                        if (rigidBody.velocity.y < 0.0f)
                            rigidBody.velocity = rigidBody.velocity * 0.98f;
                    }
                    */
                }

                /*
                objectsSpeed[i] = (objectsSpeed[i] + normal) * 0.95f;
                Vector3 Dest = floatingObjects[i].transform.position + objectsSpeed[i] * Time.deltaTime;
                Dest.y = y;
                floatingObjects[i].transform.position = Vector3.Lerp(floatingObjects[i].transform.position, Dest, 0.3f);
                floatingObjects[i].transform.rotation = Quaternion.RotateTowards(floatingObjects[i].transform.rotation, Quaternion.FromToRotation(Vector3.up, normal), 0.3f);
                */

            }
            else
            {
                physicObjects[i].gao.transform.position = new Vector3(physicObjects[i].gao.transform.position.x, y, physicObjects[i].gao.transform.position.z);
                physicObjects[i].gao.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            }
        }
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
        int LimitListLength = 1 + Mathf.CeilToInt(lengthX / resolution);

        minZArray = new float[LimitListLength];
        maxZArray = new float[LimitListLength];

        Vector3 indexPos = StartX;
        for (int i = 1; i < LimitListLength - 1; i++)
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

            if (i > 0)
            {
                backZ = Mathf.Min(backZ, backLimitList[i - 1]);
                frontZ = Mathf.Max(frontZ, frontLimitList[i - 1]);
            }
            if (i < simStripCount - 1)
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
                    vertices[index] = GetVertexLocalPos(x, z);

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

        if (useSurfaceAreaDetection)
            AdjustXContour();

        // computes UVs
        for (int x = 0; x < simStripCount; x++)
        {
            for (int z = 0; z < maxStripSize; z++)
            {
                int index = GetVerticeIndex(x, z);
                if (index >= 0)
                {
                    // SetUV
                    uvs[index].x = vertices[index].x / (float)(simStripCount - 1);
                    uvs[index].y = vertices[index].z / (float)(maxStripSize - 1);
                }
            }

        }

        // Create the mesh!
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter)
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
        simCollider = gameObject.AddComponent<BoxCollider>();
        Vector3 center = (boundingBoxMin + boundingBoxMax) / 2.0f;
        simCollider.center = center - transform.position;
        simCollider.size = boundingBoxMax - boundingBoxMin;
        simCollider.size = new Vector3(Mathf.Abs(simCollider.size.x), Mathf.Abs(simCollider.size.y), Mathf.Abs(simCollider.size.z));
        simCollider.isTrigger = true;

    }

    void AdjustXContour()
    {
        for (int i = 1; i < stripToVerticeCount[0] - 1; i++)
        {
            int z = i + stripToVerticeLocalOfs[0];
            int index = GetVerticeIndex(0, z);
            if (index >= 0)
            {
                int indexOrig = GetVerticeIndex(1, z);
                if (indexOrig >= 0)
                {
                    Vector3 origPos = GetVertexGlobalPos(1, z);
                    RaycastHit hit;
                    if (CastRay(new Ray(origPos, Vector3.left), gridResolution, out hit))
                        vertices[index].x = hit.point.x - boundingBoxMin.x;
                }
            }
        }

        for (int i = 1; i < stripToVerticeCount[simStripCount - 1] - 1; i++)
        {
            int z = i + stripToVerticeLocalOfs[simStripCount - 1];
            int index = GetVerticeIndex(simStripCount - 1, z);
            if (index >= 0)
            {
                int indexOrig = GetVerticeIndex(simStripCount - 2, z);
                if (indexOrig >= 0)
                {
                    Vector3 origPos = GetVertexGlobalPos(simStripCount - 2, z);
                    RaycastHit hit;
                    if (CastRay(new Ray(origPos, Vector3.right), gridResolution, out hit))
                        vertices[index].x = hit.point.x - boundingBoxMin.x;
                }
            }
        }

        // AdjustXContour top and bottom z to the raycast extermes
        int stripOfs = stripToVerticeGlobalOfs[0];
        vertices[stripOfs].x = gridResolution;
        vertices[stripToVerticeCount[stripOfs] - 1].x = gridResolution;

        stripOfs = stripToVerticeGlobalOfs[simStripCount - 1];
        float rightPos = gridResolution * (float)(simStripCount - 2);
        vertices[stripOfs].x = rightPos;
        vertices[stripOfs + stripToVerticeCount[simStripCount - 1] - 1].x = rightPos;


    }

    float ComputeVolume()
    {
        float Result = 0.0f;
        float gridResolutionSquare = gridResolution * gridResolution;

        // Compute all columns
        for (int i = 0; i < simSize; i++)
            Result += vertices[i].y * gridResolutionSquare;

        return Result;
    }

    float ComputeVolumeToAddPerCell()
    {
        float targetVolume = totalVolume;
        float submergedVolume = 0.0f;

        // Add up all submerged objects
        foreach (var obj in physicObjects)
        {
            Debug.Log("Submerged volume=" + obj.submergedVolume);
            submergedVolume += obj.submergedVolume;
        }

        targetVolume += submergedVolume;

        float computedVolume = ComputeVolume();
        Debug.Log("target = " + targetVolume + ", Compute=" + computedVolume);

        return (targetVolume - computedVolume) / ((float)simSize * gridResolution * gridResolution);
    }

    /*
    void DiffusionPhase()
    {
        // Diffusion Phase
        for (int i = 0; i < simSize; i++)
        {
            float transferRate = diffusionSpeed * Time.deltaTime;
            speedY[i] += ((vertices[neighbor[i].prevX].y + vertices[neighbor[i].nextX].y + vertices[neighbor[i].prevZ].y + vertices[neighbor[i].nextZ].y) / 4.0f - vertices[i].y) * transferRate;
            speedY[i] *= viscosity;
        }
    }
    
    void AdvectionPhase()
    {
        float targetVolume = totalVolume;

        // Add up all submerged objects
        foreach (var obj in physicObjects)
            targetVolume += obj.submergedVolume;

        float volumeToAddPerCell = (targetVolume - ComputeVolume()) / (float)simSize;

        // Advection Phase
        for (int i = 0; i < simSize; i++)
        {
            vertices[i].y += volumeToAddPerCell + speedY[i] * Time.deltaTime;
            // check for bottom
            if (vertices[i].y < 0.0f)
                vertices[i].y = 0.0f;
        }
    }
    */

    Vector3 GetYNormal(float x, float z, out float y)
    {
        float wx1, wz1;
        int y11Index = GetArrayIndexFromPos(x, z, out wx1, out wz1);
        float wx2 = (1.0f - wx1);
        float wz2 = (1.0f - wz1);

        if (y11Index < 0)
        {
            y = boundingBoxMax.y;
            return Vector3.up;
        }

        int y12Index = neighbor[y11Index].nextZ;
        int y21Index = neighbor[y11Index].nextX;
        int y22Index = neighbor[y21Index].nextX; ;

        float y11 = vertices[y11Index].y;
        float y12 = vertices[y12Index].y;
        float y21 = vertices[y21Index].y;
        float y22 = vertices[y22Index].y;

        y = boundingBoxMin.y + (y11 * wx1 * wz1) + (y11 * wx1 * wz2) + (y21 * wx2 * wz1) + (y22 * wx2 * wz2);

        return Vector3.Cross(vertices[y11Index] - vertices[y12Index], vertices[y11Index] - vertices[y21Index]).normalized;
    }

    // Return a local position relative to boundingBoxMin, from a (x, z) couple
    Vector3 GetVertexLocalPos(int x, int z)
    {
        return new Vector3((float)x * gridResolution, boundingBoxMax.y - boundingBoxMin.y, (float)z * gridResolution - halfResolution);
    }

    // Return a global position from a (x, z) couple
    Vector3 GetVertexGlobalPos(int x, int z)
    {
        return boundingBoxMin + GetVertexLocalPos(x, z);
    }

    // Return an index, based on a local int position
    // return defaultVal if (x, z) is outside bound or doesn't have simulation point
    int GetVerticeIndex(int x, int z, int defaultVal = -1)
    {
        if ((x < 0) || (x >= simStripCount))
            return defaultVal;
        if ((z < stripToVerticeLocalOfs[x]) || (z >= (stripToVerticeLocalOfs[x] + stripToVerticeCount[x])))
            return defaultVal;
        return stripToVerticeGlobalOfs[x] + z - stripToVerticeLocalOfs[x];
    }

    // Return an array index based on world position
    int GetArrayIndexFromPos(Vector3 worldPos)
    {
        int x = (int)((worldPos.x - boundingBoxMin.x) / gridResolution);
        int z = (int)((worldPos.z - boundingBoxMin.z) / gridResolution);
        return GetVerticeIndex(x, z);
    }

    int GetArrayIndexFromPos(float px, float pz, out float weightX, out float weightZ)
    {
        float x = (int)((px - boundingBoxMin.x) / gridResolution);
        float z = (int)((pz - boundingBoxMin.z) / gridResolution);
        weightX = 1.0f - Mathf.Repeat(x, 1.0f);
        weightZ = 1.0f - Mathf.Repeat(x, 1.0f);
        return GetVerticeIndex((int)x, (int)z);
    }

    void AddNewGameObject(GameObject obj)
    {
        Vector3 massCenterDelta;
        float objVolume = EvaluateObjectVolume(obj, out massCenterDelta);
        physicObjects.Add(new PhysicObject { gao = obj, volume = objVolume, massCenterDelta = massCenterDelta });
    }

    float EvaluateObjectVolume(GameObject obj, out Vector3 massCenterDelta)
    {
        var boxCollider = obj.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            Debug.Log("Box Collider");
            massCenterDelta = boxCollider.transform.position - obj.transform.position;
            return boxCollider.size.x * obj.transform.localScale.x * boxCollider.size.y * obj.transform.localScale.y * boxCollider.size.z * obj.transform.localScale.z;
        }

        var objCollider = obj.GetComponent<Collider>();
        if (objCollider)
        {
            massCenterDelta = objCollider.transform.position - obj.transform.position;
            return objCollider.bounds.size.x * objCollider.bounds.size.y * objCollider.bounds.size.z;
        }

        // Default mass Center is object center
        massCenterDelta = new Vector3();
        return 1.0f;
    }

    /// <summary>
    ///  ECS
    /// </summary>
    /// 
    /*
    void InitEcsSimulation()
    {
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
    */
}


/*
Data required for Diffusion
---------------------------
- (RO) SimSize
- (RO) diffusionSpeed
- (RO) Topology				
- (RO) Vertices[]/height		
- (WR) Speed[]				

Data required for Advection
---------------------------
- (RO) Volume
	- (RO) Vertices[]/height
*** (RO) Objects volume
- (RO) SimSize
- (RW) Vertices[]/height
- (RO) speed[]
- (RO) Topology

Data required for objects sim
-----------------------------

- Bouding box for detection
- Topology to transfer pos -> index
- (RO) Vertices[]/height (for now)
- (RW) Speed[]
- (RW ) Objects Volume



--------------------------------

Topology =
SimSize
list of neighbor[SimSize]

OR

SizeX, SizeZ
SimSize
ArrayOfIndex[SizeX * SizeZ]

---------------------------------

For simulation, topo is suffficent
For object interation & forces, we need topo + pos translation.
Pos translation is easy with grid
With option 1, it requieres strips OR maybe we can embed x/z in index ?


 * */
