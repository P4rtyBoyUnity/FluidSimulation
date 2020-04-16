using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/* 
 * A way to highligt vertex (color would be nice)
 * Rubber ducky for demo
 * Volume augmentation with key
 * surface area for mousinteraction
 * Skirt
 * 
 * TODO:
 * transfertRate = diffusionSpeed * dt = c2/h2 * dT
 * Upper limit for choice of wave speed : c < h /Δt
 * Upper limit for choice of time step  : Δt < h/c 
 *  h = width of the cell (gridResolution), c = Constant c is the speed at which waves travel
 * 
 * DEMONSTRATION
    *  (Floating object, normals control) Rubber Ducky floating
    *  (volume augmentation) Object falling in water (ex: Cube)
    *  Topology change; walls
    *  Handling of different bottoms (splashing on walls until the water is above the wall)
    *  wave splashing on floating objects
 * PLANE CREATION
 *      AdjustXContour out!
 *          maybe we should add a second X min/max value array
 *      Recompute normal in //
 *          or at least recompute normal in a job while computing  physic objects, while processing objects & inputs 
 *      Skirt
 *      Bugs de contour
 *      Lors du rendering on vois des triangles weird (mais pas en scene view)
 *          - semble etre juste du coté X-
 *          
 *      Find limit of mesh and manage multiple meshes
 * DFG
 *  - Protection pour DVG Nb Vertices bug
 *  - experiment using componentNodes
 *          CanConnectBuffer_OfBufferElement_ToComponentNode_WithMatchingBuffer_ThroughWeakAPI
   
 * Fluid level should be different than the one at init time (so if the volume rise, we dont see the contour)
     - Water level can change if immerged object (check for places where we assume water level)
        - we must also change bounding box
    - Manage many types of objects shape (EvaluateObjectVolume...)
    - Improved objects physics
        - Manage splash
            - Wave in direction of impact
            - Objects slowdown
        - Consider object mass & volume for floating
        - when detecting surface level, take 5 measures (each bouding box corner + middle?) for object larger than grid
    - Create a fixed grid for inital config? (not good for parallel compute)
    - foaming?
*/

public class FluidSimulation : MonoBehaviour
{
    // 4 Types of simulation right now;
    // - Reference  : Classic code simulation
    // - Jobs       : Parrallelized classic code simulation
    // - ECS        : DOTS based simulation using ECS
    // - DFG        : Simulation done with Data Flow Graph tech
    public enum SimType { Reference, Jobs, Ecs, Dfg };
    public SimType simulationType = SimType.Reference;

    public enum SimSurfaceType { Rectangle, DetectPlane };
    public SimSurfaceType surfaceType = SimSurfaceType.Rectangle;

    public bool generateSkirt = false;
    public float gridResolution = 0.5f;
    public Vector3 maxRayLength = new Vector3(100.0f, 100.0f, 100.0f);
    public float diffusionSpeed = 20.0f;
    public float viscosity = 0.998f;
    public uint simulationIteration = 1;
    public bool debugPushVolume = false;
    public List<FluidSimEffect> physicEffects = new List<FluidSimEffect>();

    // Sim Data
    private FluidSimulationInterface simulation = null;
    private float halfGridResolution = 0.0f;
    private float gridResolutionSquare = 0.0f;
    private Vector3 boundingBoxMin;
    private Vector3 boundingBoxMax;
    private float initialTotalVolume = 0.0f;

    // Array length == Nb X Values
    private SurfaceDelimitation[]   surfaceLimits = null;
    private SimDataQuad             simData = null;
    private NativeArray<Vector3>    simVertices;

    private Mesh                    deformingMesh;
    private Color[]                 colors = null;

    // temp
    public  BoxCollider             simCollider;

    // Debug
    private List<GameObject>        debugItemToDelete = new List<GameObject>();
    private List<GameObject>        debugItemLastFrame = new List<GameObject>();

    // Floating objects
    public class PhysicObject
    {
        public GameObject gao;
        public Vector3 massCenterDelta;
        public float volume;

        // updated every frame
        public float submergedVolume;
    }
    private List<PhysicObject> physicObjects = new List<PhysicObject>();

    public void OnDestroy()
    {
        if (simulation != null)
            { simulation.Dispose(); }
        simVertices.Dispose();
    }

    // Start is called before the first frame update
    void Start()
    {
        halfGridResolution = gridResolution / 2.0f;
        gridResolutionSquare = gridResolution * gridResolution;

        SurfaceCreationBase surfaceFactory;
        if (surfaceType == SimSurfaceType.DetectPlane)
            surfaceFactory = new SurfaceCreationRayCast();
        else
            surfaceFactory = new SurfaceCreationQuad();

        // Create surface
        surfaceLimits = surfaceFactory.DetectSurface(transform.position, maxRayLength, gridResolution, out boundingBoxMin, out boundingBoxMax);

        // Create the simulation data
        simData = new SimDataQuad(transform.position, gridResolution, boundingBoxMin, boundingBoxMax,surfaceLimits);

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        // Create the mesh
        deformingMesh = simData.CreateSurface(surfaceLimits, out simVertices);        

        // Adjust X side contours for adjusted planes
        if (surfaceType == SimSurfaceType.DetectPlane)
            AdjustXContour();

        // Computes UVs (they need to be done AFTER adjustment)
        deformingMesh.uv = simData.ComputeUVs(simVertices, SimDataQuad.TextureTilingType.Stretch);

        meshFilter.mesh = deformingMesh;
        meshFilter.transform.position = boundingBoxMin;

        // colors
        colors = new Color[simData.simSize];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = new Color(0, 0, 1.0f);
        deformingMesh.colors = colors;

        // Create collider
        simCollider = gameObject.AddComponent<BoxCollider>();
        simData.SetBoxCollider(transform.position, ref simCollider);

        // Compute volume
        initialTotalVolume = ComputeVolume();

        /*
        Debug.Log("---------------------------------------------------------");
        Debug.Log("Nb Simulation points : " + simSize);
        Debug.Log("NbVertex             : " + simVertices.Length);
        Debug.Log("NbIndex              : " + deformingMesh.triangles.Length);
        Debug.Log("NbNormals            : " + deformingMesh.triangles.Length);
        Debug.Log("volume               : " + totalVolume);
        Debug.Log("---------------------------------------------------------");
        */

        if (simulationType == SimType.Jobs)
            simulation = new JobFluidSimulation(simData.neighbor, ref simVertices);
        if (simulationType == SimType.Ecs)
            simulation = new EcsSimulation(simData.neighbor, ref simVertices, initialTotalVolume / gridResolutionSquare);
        else if (simulationType == SimType.Dfg)
            simulation = new DfgSimulation(simData.neighbor, ref simVertices);
        else
            simulation = new ReferenceFluidSimulation(simData.neighbor, ref simVertices);
    }

    // Update is called once per frame
    void Update()
    {
        float deltaT = Time.deltaTime / simulationIteration;
        
        for (uint i = 0; i < simulationIteration; i++)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Simulation");
            simulation.Simulate(ComputeTargetVolume() / gridResolutionSquare, diffusionSpeed, viscosity, deltaT);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        UnityEngine.Profiling.Profiler.BeginSample("WaitForVerticeUpdate");
        simulation.WaitForVerticeUpdate();
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("Physic objects");
        UpdateFloatingObjects();
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("Update Vertices");
        deformingMesh.vertices = simVertices.ToArray();
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("Recompute Normals");        
        RecalculateNormals();
        UnityEngine.Profiling.Profiler.EndSample();
    }

    public void LateUpdate()
    {
        // Clear debug items
        foreach (var item in debugItemLastFrame)
            Destroy(item);
        debugItemLastFrame.Clear();

        debugItemLastFrame = debugItemToDelete;
        debugItemToDelete = new List<GameObject>();
    }

    public void PushVolume(Vector3 pos, Vector3 dir)
    {
        int index = GetArrayIndexFromPos(pos);
       
        if (index != -1)
            simulation.ApplyForce(index, dir.y, 1.0f);

        if(debugPushVolume)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = pos;
            sphere.transform.localScale = new Vector3(0.1f, 20.0f, 0.1f);
            debugItemToDelete.Add(sphere);
        }       
    }

    public void DisplaceVolume(Vector3 pos, float volume)
    {
        int index = GetArrayIndexFromPos(pos);
        if (index != -1)
            simVertices[index] = simVertices[index] + Vector3.up * volume;

        if (debugPushVolume)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = pos;
            sphere.transform.localScale = new Vector3(0.1f, 20.0f, 0.1f);
            debugItemToDelete.Add(sphere);
        }
    }
       

    private void RecalculateNormals()
    {
        /*
        if(useJobs)
        {
            Vector3[] normals = new Vector3[deformingMesh.normals.Length];
            int[] triangles = deformingMesh.triangles;

            //Debug.Log("Normals = " + normals.Length + " Triangles=" + triangles.Length);
            for (int i = 0; i < triangles.Length; i+= 3)
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];
                Vector3 v1 = simVertices[i1];
                Vector3 v2 = simVertices[i2];
                Vector3 v3 = simVertices[i3];
                Vector3 normal = Vector3.Cross(v2 - v3, v2 - v1);
                //normal = Vector3.up;
                normals[i1] += normal;
                normals[i2] += normal;
                normals[i3] += normal;
            }

            for (int i = 0; i < normals.Length; i++)
                normals[i] = normals[i].normalized;

            deformingMesh.normals = normals;
        }
        else
        */
        {
            deformingMesh.RecalculateNormals();
        }
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
        // Add unique index to struct
        for (int i = 0; i < physicObjects.Count; i++)
            foreach (var effect in physicEffects)
                effect.UpdateBehavior(this, physicObjects[i]);
    }
    
    void AdjustXContour()
    {
        for (int i = 1; i < simData.stripToVerticeData[0].count - 1; i++)
        {
            int z = i + simData.stripToVerticeData[0].localOffset;
            int index = simData.GetVerticeIndex(0, z);
            if (index >= 0)
            {
                int indexOrig = simData.GetVerticeIndex(1, z);
                if (indexOrig >= 0)
                {
                    Vector3 origPos = GetVertexGlobalPos(1, z);
                    RaycastHit hit;
                    if (CastRay(new Ray(origPos, Vector3.left), gridResolution, out hit))
                        simVertices[index] = new Vector3(hit.point.x - boundingBoxMin.x, simVertices[index].y, simVertices[index].z);
                }
            }
        }

        for (int i = 1; i < simData.stripToVerticeData[simData.stripToVerticeData.Length - 1].count - 1; i++)
        {
            int z = i + simData.stripToVerticeData[simData.stripToVerticeData.Length - 1].localOffset;
            int index = simData.GetVerticeIndex(simData.stripToVerticeData.Length - 1, z);
            if (index >= 0)
            {
                int indexOrig = simData.GetVerticeIndex(simData.stripToVerticeData.Length - 2, z);
                if (indexOrig >= 0)
                {
                    Vector3 origPos = GetVertexGlobalPos(simData.stripToVerticeData.Length - 2, z);
                    RaycastHit hit;
                    if (CastRay(new Ray(origPos, Vector3.right), gridResolution, out hit))
                        simVertices[index] = new Vector3(hit.point.x - boundingBoxMin.x, simVertices[index].y, simVertices[index].z);
                }
            }
        }

        // AdjustXContour top and bottom z to the raycast extermes
        int stripOfs = simData.stripToVerticeData[0].globalOffset;
        int stripOfs2 = simData.stripToVerticeData[stripOfs].count - 1;
        simVertices[stripOfs] = new Vector3(gridResolution, simVertices[stripOfs].y, simVertices[stripOfs].z);        
        simVertices[stripOfs2] = new Vector3(gridResolution, simVertices[stripOfs2].y, simVertices[stripOfs2].z);

        stripOfs = simData.stripToVerticeData[simData.stripToVerticeData.Length - 1].globalOffset;
        stripOfs2 = stripOfs + simData.stripToVerticeData[simData.stripToVerticeData.Length - 1].count - 1;
        float rightPos = gridResolution * (float)(simData.stripToVerticeData.Length - 2);
        simVertices[stripOfs] = new Vector3(rightPos, simVertices[stripOfs].y, simVertices[stripOfs].z);        
        simVertices[stripOfs2] = new Vector3(rightPos, simVertices[stripOfs2].y, simVertices[stripOfs2].z);
    }

    float ComputeVolume()
    {
        float result = 0.0f;

        // Compute all columns
        for (int i = 0; i < simData.simSize; i++)
            result += simVertices[i].y;

        return result * gridResolutionSquare;
    }

    float ComputeTargetVolume()
    {
        float targetVolume = initialTotalVolume;

        // Add up all submerged objects
        foreach (var obj in physicObjects)
            targetVolume += obj.submergedVolume;

        return targetVolume;
    }       

    public Vector3 GetYNormal(float x, float z, out float y)
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

        int y12Index = simData.neighbor[y11Index].nextZ;
        int y21Index = simData.neighbor[y11Index].nextX;
        int y22Index = simData.neighbor[y21Index].nextX;

        float y11 = simVertices[y11Index].y;
        float y12 = simVertices[y12Index].y;
        float y21 = simVertices[y21Index].y;
        float y22 = simVertices[y22Index].y;

        y = boundingBoxMin.y + (y11 * wx1 * wz1) + (y11 * wx1 * wz2) + (y21 * wx2 * wz1) + (y22 * wx2 * wz2);

        return Vector3.Cross(simVertices[y11Index] - simVertices[y12Index], simVertices[y11Index] - simVertices[y21Index]).normalized;
    }

    // Return a local position relative to boundingBoxMin, from a (x, z) couple
    Vector3 GetVertexLocalPos(int x, int z)
    {
        return new Vector3((float)x * gridResolution, boundingBoxMax.y - boundingBoxMin.y, (float)z * gridResolution - halfGridResolution);
    }

    // Return a global position from a (x, z) couple
    Vector3 GetVertexGlobalPos(int x, int z)
    {
        return boundingBoxMin + GetVertexLocalPos(x, z);
    }

    // Return an array index based on world position
    public int GetArrayIndexFromPos(Vector3 worldPos)
    {
        int x = (int)((worldPos.x - boundingBoxMin.x + halfGridResolution) / gridResolution);
        int z = (int)((worldPos.z - boundingBoxMin.z + halfGridResolution) / gridResolution);
        return simData.GetVerticeIndex(x, z);
    }

    int GetArrayIndexFromPos(float px, float pz, out float weightX, out float weightZ)
    {
        float x = (int)((px - boundingBoxMin.x + halfGridResolution) / gridResolution);
        float z = (int)((pz - boundingBoxMin.z + halfGridResolution) / gridResolution);
        weightX = 1.0f - Mathf.Repeat(x, 1.0f);
        weightZ = 1.0f - Mathf.Repeat(x, 1.0f);
        return simData.GetVerticeIndex((int)x, (int)z);
    }

    void AddNewGameObject(GameObject obj)
    {
        Vector3 massCenterDelta;
        float objVolume = EvaluateObjectVolume(obj, out massCenterDelta);
        var newPhysicObject = new PhysicObject { gao = obj, volume = objVolume, massCenterDelta = massCenterDelta };

        bool addPhysicObject = true;
        for (int i = 0; i < physicObjects.Count; i++)
            foreach (var effect in physicEffects)
                addPhysicObject &= effect.OnFluidSimContact(this, physicObjects[i]);

        if(addPhysicObject)
            physicObjects.Add(newPhysicObject);

    }

    float EvaluateObjectVolume(GameObject obj, out Vector3 massCenterDelta)
    {
        var boxCollider = obj.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            //Debug.Log("Box Collider");
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
}

