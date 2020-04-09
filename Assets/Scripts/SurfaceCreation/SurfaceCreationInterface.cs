using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct SurfaceDelimitation
{
    public float front;
    public float back;
}

public abstract class SurfaceCreationBase
{
    public abstract SurfaceDelimitation[] DetectSurface(Vector3 pos, Vector3 maxRayLength, float gridResolution, out Vector3 boundingBoxMin, out Vector3 boundingBoxMax);
}

public class SurfaceCreationQuad : SurfaceCreationBase
{
    public override SurfaceDelimitation[] DetectSurface(Vector3 pos, Vector3 maxRayLength, float gridResolution, out Vector3 boundingBoxMin, out Vector3 boundingBoxMax)
    {
        // The surface is a rectangle defined by maxRayLengthX & maxRayLengthZ            
        boundingBoxMin = pos - maxRayLength;
        boundingBoxMax = pos + maxRayLength;
        boundingBoxMax.y = pos.y;
        int LimitListLength = 1 + Mathf.CeilToInt((boundingBoxMax.x - boundingBoxMin.x) / gridResolution);
        SurfaceDelimitation[] surfaceLimits = new SurfaceDelimitation[LimitListLength];
        for (int i = 0; i < LimitListLength; i++)
        {
            surfaceLimits[i].back = boundingBoxMin.z;
            surfaceLimits[i].front = boundingBoxMax.z;
        }

        return surfaceLimits;
    }    
}

public class SurfaceCreationRayCast : SurfaceCreationBase
{
    public override SurfaceDelimitation[] DetectSurface(Vector3 pos, Vector3 maxRayLength, float gridResolution, out Vector3 boundingBoxMin, out Vector3 boundingBoxMax)
    {
        // Check limit on the right (X+) & left (X-) side            
        RaycastHit hit;
        boundingBoxMin.x = CastRay(new Ray(pos, Vector3.left), maxRayLength.x, out hit) ? hit.point.x : pos.x - maxRayLength.x;
        boundingBoxMax.x = CastRay(new Ray(pos, Vector3.right), maxRayLength.x, out hit) ? hit.point.x : pos.x + maxRayLength.x;
        boundingBoxMin.y = CastRay(new Ray(pos, Vector3.down), maxRayLength.y, out hit) ? hit.point.y : pos.y - maxRayLength.y;
        boundingBoxMax.y = pos.y;

        // Along the line inbetween leftHit & rightHit, project rays on each side toward Z- & Z+, at resolution distance
        // and fill backLimitList, frontLimitList with the results
        Vector3 leftHit = pos;
        leftHit.x = boundingBoxMin.x;
        SurfaceDelimitation[] surfaceLimits = ComputeZSurface(leftHit, boundingBoxMax.x - boundingBoxMin.x, maxRayLength.z, gridResolution);

        // Compute Bounding box in z            
        boundingBoxMin.z = surfaceLimits[0].back;
        boundingBoxMax.z = surfaceLimits[0].front;
        for (int i = 1; i < surfaceLimits.Length; i++)
        {
            boundingBoxMin.z = Mathf.Min(boundingBoxMin.z, surfaceLimits[i].back);
            boundingBoxMax.z = Mathf.Max(boundingBoxMax.z, surfaceLimits[i].front);
        }

        return surfaceLimits;
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
    SurfaceDelimitation[] ComputeZSurface(Vector3 StartX, float lengthX, float maxZDistance, float resolution)
    {
        int LimitListLength = 1 + Mathf.CeilToInt(lengthX / resolution);
        SurfaceDelimitation[] surface = new SurfaceDelimitation[LimitListLength];

        Vector3 indexPos = StartX;
        for (int i = 1; i < LimitListLength - 1; i++)
        {
            indexPos.x = StartX.x + Mathf.Min((float)i * resolution, lengthX);

            RaycastHit hit;
            surface[i].back = CastRay(new Ray(indexPos, Vector3.back), maxZDistance, out hit) ? hit.point.z : indexPos.z - maxZDistance;
            surface[i].front = CastRay(new Ray(indexPos, Vector3.forward), maxZDistance, out hit) ? hit.point.z : indexPos.z + maxZDistance;
        }

        surface[0] = surface[1];
        surface[LimitListLength - 1] = surface[LimitListLength - 2];

        return surface;
    }
}