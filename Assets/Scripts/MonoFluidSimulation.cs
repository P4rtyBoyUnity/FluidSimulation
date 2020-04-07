using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;


// Detection auto des objets qui entrent en contact avec eau 
// Rubber ducky - doit flotter comme du monde avec physique
// objet qui coule/semi coule avec déplacement de liquide
// Test limits bottom avec array

[RequireComponent(typeof(MeshFilter))]
public class MonoFluidSimulation : MonoBehaviour
{
    public int planeLength = 11;       // Size of of the plane in z
    public int planeWidth = 11;       // Size of of the plane in x
    public float diffusionSpeed = 0.05f;
    public float viscosity = 0.999f;
    public float originalHeight = 5.0f;
    public MeshFilter frontWaterMesh = null;
    public GameObject[] floatingObjects = new GameObject[0];

    public float testAntiGravity = 2.0f;

    Vector3[] objectsSpeed = new Vector3[2];

    Mesh deformingMesh;
    Vector3[] displacedVertices;
    Vector3[] frontDisplacedVertices;
    float[] speedY = new float[0];

    void Start()
    {
        // Init deforming mesh
        deformingMesh = GetComponent<MeshFilter>().mesh;

        {
            Vector3[] originalVertices = deformingMesh.vertices;
            displacedVertices = new Vector3[originalVertices.Length];
            for (int i = 0; i < originalVertices.Length; i++)
                displacedVertices[i] = originalVertices[i];
        }

        if (frontWaterMesh)
        {
            Vector3[] originalVertices = frontWaterMesh.mesh.vertices;
            frontDisplacedVertices = new Vector3[originalVertices.Length];
            for (int i = 0; i < originalVertices.Length; i++)
                frontDisplacedVertices[i] = originalVertices[i];
        }

        // Init simulation
        speedY = new float[planeLength * planeWidth];
        transform.Translate(new Vector3(0.0f, originalHeight, 0.0f));
    }

    // Update is called once per frame
    void Update()
    {
        DiffusionPhase();

        AdvectionPhase();

        UpdateFloatingObjects();

        deformingMesh.vertices = displacedVertices;
        deformingMesh.RecalculateNormals();

        if (frontWaterMesh)
        {
            StitchFront();
            frontWaterMesh.mesh.vertices = frontDisplacedVertices;
            frontWaterMesh.mesh.RecalculateNormals();
        }

        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            var collider = GetComponent<MeshCollider>();
            if (collider)
            {
                collider.enabled = true;
                if (collider.Raycast(ray, out hit, 100))
                {
                    int x = GetArrayIndexFromXPos(hit.point.x);
                    int z = GetArrayIndexFromZPos(hit.point.z);
                    speedY[x + z * planeWidth] -= 50.0f * Time.deltaTime;
                }
                collider.enabled = false;
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            var collider = GetComponent<MeshCollider>();
            if (collider)
            {
                collider.enabled = true;
                if (collider.Raycast(ray, out hit, 100))
                {
                    //if (hit.collider.gameObject == this)
                    {
                        float wx, wz;
                        int x = GetArrayIndexFromXPos(hit.point.x, out wx);
                        int z = GetArrayIndexFromZPos(hit.point.z, out wz);
                        //displacedVertices[x + z * planeWidth].y += 1.0f;
                        //ball.transform.position = new Vector3(hit.point.x, displacedVertices[x + z * planeWidth].y, hit.point.z);
                        Debug.Log("Hit = (" + hit.point.x + ", " + hit.point.z + ", Array = (" + x + "." + wx + ", " + z + "." + wz + ")");
                    }
                }
                collider.enabled = false;
            }
        }

    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Enter! " + other.gameObject.name);
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger Exit! " + other.gameObject.name);
    }

    float ComputeVolume()
    {
        float Result = 0.0f;

        // Diffusion Phase
        for (int x = 0; x < planeWidth; x++)
            for (int z = 0; z < planeLength; z++)
                Result += displacedVertices[x + z * planeWidth].y;
        return Result;
    }

    void DiffusionPhase()
    {
        // Diffusion Phase
        for (int x = 0; x < planeWidth; x++)
        {
            for (int z = 0; z < planeLength; z++)
            {
                float transferRate = diffusionSpeed * Time.deltaTime;
                speedY[x + z * planeWidth] += ((GetHeight(x - 1, z) + GetHeight(x + 1, z) + GetHeight(x, z - 1) + GetHeight(x, z + 1)) / 4.0f - GetHeight(x, z)) * transferRate;
                speedY[x + z * planeWidth] *= viscosity;
            }
        }
    }

    void AdvectionPhase()
    {
        float volumeToAddPerCell = -ComputeVolume() / (float)(planeWidth * planeLength);

        // Advection Phase
        for (int x = 0; x < planeWidth; x++)
            for (int z = 0; z < planeLength; z++)
            {
                displacedVertices[x + z * planeWidth].y += volumeToAddPerCell + speedY[x + z * planeWidth] * Time.deltaTime;
                // check for bottom
                if (displacedVertices[x + z * planeWidth].y < -originalHeight)
                    displacedVertices[x + z * planeWidth].y = -originalHeight;
            }
    }

    void UpdateFloatingObjects()
    {
        for(uint i = 0; i < floatingObjects.Length; i++)        
        {            
            // PARAMS FOR OBJECTS
            // How much they are in the water
            // How fast they rotate
            float y;
            Vector3 normal = GetYNormal(floatingObjects[i].transform.position.x, floatingObjects[i].transform.position.z, out y);

            var rigidBody = floatingObjects[i].GetComponent<Rigidbody>();
            if(rigidBody)
            {
                if (y > 0.0f)
                {
                    float Ratio = 1.0f - floatingObjects[i].transform.position.y / y;
                    if (Ratio > 0.0f)
                    {
                        rigidBody.AddForce(normal * Physics.gravity.magnitude * (0.25f + Ratio * 0.25f), ForceMode.Force);
                        if (rigidBody.velocity.y < 0.0f)
                            rigidBody.velocity = rigidBody.velocity * 0.98f;
                    }
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
                floatingObjects[i].transform.position = new Vector3(floatingObjects[i].transform.position.x, y, floatingObjects[i].transform.position.z);
                floatingObjects[i].transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            }
        }
    }

    void StitchFront()
    {
        for (int i = 0; i < 11; i++)
        {
            float top    = (originalHeight + 2.0f * displacedVertices[10 * 11 + i].y) * 1.25f;
            float bottom = originalHeight * -1.5f;
            for (int j = 0; j < 11; j++)
                frontDisplacedVertices[j * 11 + i].z = Mathf.Lerp(top, bottom, (float)j / 11.0f);
        }

    }

    int GetArrayIndexFromXPos(float px)
    {
        return planeWidth - (int)(px + 6.5f - transform.position.x);
    }

    int GetArrayIndexFromZPos(float pz)
    {
        return planeLength - (int)(pz + 6.5f - transform.position.z);
    }
    int GetArrayIndexFromXPos(float px, out float weigth)
    {        
        float pos = (float)planeWidth - (px + 6.0f - transform.position.x);
        int result = (int)pos;
        weigth = 1.0f - (pos - (float)result);
        return result;
    }

    int GetArrayIndexFromZPos(float pz, out float weigth)
    {
        float pos = (float)planeLength - (pz + 6.0f - transform.position.z);
        int result = (int)pos;
        weigth = 1.0f - (pos - (float)result);
        return result;
    }

    float GetYPos(float x, float z)
    {
        float wx1, wz1;
        int xi = GetArrayIndexFromXPos(x, out wx1);
        int zi = GetArrayIndexFromZPos(z, out wz1);
        float wx2 = (1.0f - wx1);
        float wz2 = (1.0f - wz1);

        float result = displacedVertices[xi + 0 + (zi + 0) * planeWidth].y * wx1 * wz1
                     + displacedVertices[xi + 1 + (zi + 0) * planeWidth].y * wx2 * wz1
                     + displacedVertices[xi + 0 + (zi + 1) * planeWidth].y * wx1 * wz2
                     + displacedVertices[xi + 1 + (zi + 1) * planeWidth].y * wx2 * wz2;
        return originalHeight + result;        
    }

    Vector3 GetYNormal(float x, float z, out float y)
    {
        float wx1, wz1;
        int xi = GetArrayIndexFromXPos(x, out wx1);
        int zi = GetArrayIndexFromXPos(z, out wz1);
        float wx2 = (1.0f - wx1);
        float wz2 = (1.0f - wz1);
        float y11 = displacedVertices[xi + 0 + (zi + 0) * planeWidth].y;
        float y12 = displacedVertices[xi + 0 + (zi + 1) * planeWidth].y;
        float y21 = displacedVertices[xi + 1 + (zi + 0) * planeWidth].y;        
        float y22 = displacedVertices[xi + 1 + (zi + 1) * planeWidth].y;

        y = originalHeight + (y11 * wx1 * wz1) + (y11 * wx1 * wz2) + (y21 * wx2 * wz1) + (y22 * wx2 * wz2);

        return Vector3.Cross(displacedVertices[xi + 0 + (zi + 0) * planeWidth] - displacedVertices[xi + 0 + (zi + 1) * planeWidth], displacedVertices[xi + 0 + (zi + 0) * planeWidth] - displacedVertices[xi + 1 + (zi + 0) * planeWidth]).normalized;
    }

    float GetHeight(int x, int z)
    {
        if (x < 0)
            x = 0;
        if (x >= planeWidth)
            x = planeWidth-1;
        if (z < 0)
            z = 0;
        if (z >= planeLength)
            z = planeLength - 1;
        return displacedVertices[x + z * planeWidth].y;
    }
}
