using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastCamera : MonoBehaviour
{
    /* General settings */
    public uint scanLineWidth = 16; // Pixels
    public uint nbScanLines = 16;

    public float horizontalFOV = 120f; // Degrees
    public float pixelScanTime = .01f; // Seconds per pixel

    public int maxRayCastDistance = 50; // Meters

    /* Current scan coordinates and deltaTime */
    private float lastPixelScanDeltaTime = 0; // Seconds
    private uint currentScanX = 0;
    private uint currentScanY = 0;

    private float scanStepX;
    private float scanStepY;
    private float scanStartX;
    private float scanStartY;

    /* Visual Ray feedback settings */
    public bool visualRaysEnabled = true;
    public Color visualRayStartColor = new Color(1f, 0.5f, 0.15f, 1f);
    public Color visualRayEndColor = new Color(1f, 0.15f, 0.5f, 1f);
    public float visualRayWidth = .1f;
    private float visualRayFadeTime; // Seconds, computed from scan width and time-per-pixel
    private List<((uint, uint), float)> visualRayTimeoutList; // ((x, y), remaining time)
    private GameObject[,] visualRayList;

    private float[,] depthBuffer;

    public delegate void onScanBufferRefresh(float[,] buffer);
    List<onScanBufferRefresh> scanBufferRefreshListeners = new List<onScanBufferRefresh>();

    public delegate void onRaycastBallHit(Vector3 pos, string tag);
    List<onRaycastBallHit> raycastBallHitsListeners = new List<onRaycastBallHit>();

    void Start()
    {
        float verticalFOV = horizontalFOV * (nbScanLines / scanLineWidth);

        scanStepX = Mathf.Tan(Mathf.Deg2Rad * horizontalFOV / 2) * maxRayCastDistance / scanLineWidth;
        scanStepY = Mathf.Tan(Mathf.Deg2Rad * verticalFOV / 2) * maxRayCastDistance / nbScanLines;

        scanStartX = -0.5f * scanLineWidth * scanStepX;
        scanStartY = 0; //No need to look up //0.5f * nbScanLines * scanStepY;

        if (visualRaysEnabled)
        {
            visualRayList = this.CreateVisualRays();
            visualRayTimeoutList = new List<((uint, uint), float)>();
            visualRayFadeTime = scanLineWidth * pixelScanTime;
        }

        depthBuffer = new float[scanLineWidth, nbScanLines];
    }

    private GameObject[,] CreateVisualRays()
    {
        var visualRays = new GameObject[scanLineWidth, nbScanLines];

        var points = new Vector3[2];
        points[0] = new Vector3(0, 0, 0);

        for (uint j = 0; j < nbScanLines; j++)
        {
            for (uint i = 0; i < scanLineWidth; i++)
            {
                GameObject obj = new GameObject("visualRay_" + i.ToString() + "_" + j.ToString());
                obj.transform.SetParent(this.transform, false);

                obj.layer = 2; /* Disable raycast */

                LineRenderer lineRenderer = obj.AddComponent(typeof(LineRenderer)) as LineRenderer;

                lineRenderer.useWorldSpace = false;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;

                points[1] = GetRayVector(i, j);
                lineRenderer.SetPositions(points.Clone() as Vector3[]);

                lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));

                lineRenderer.startWidth = visualRayWidth;
                lineRenderer.endWidth = visualRayWidth;

                lineRenderer.enabled = false;

                visualRays[i, j] = obj;
            }
        }

        return visualRays;
    }

    public void FixedUpdate()
    {
        lastPixelScanDeltaTime += Time.fixedDeltaTime;

        while (lastPixelScanDeltaTime >= pixelScanTime)
        {
            lastPixelScanDeltaTime -= pixelScanTime;
            currentScanX++;

            // Update current pixel coordinates
            if (currentScanX >= scanLineWidth)
            {
                currentScanX = 0;
                currentScanY++;
                if (currentScanY >= nbScanLines)
                {
                    currentScanY = 0;
                    callRefreshEventListeners();
                }
            }

            ScanPixelAt(currentScanX, currentScanY);
        }
    }

    private Vector3 GetRayVector(uint x, uint y)
    {
        return new Vector3(scanStartX + x * scanStepX,
                            scanStartY - y * scanStepY, // Y axis is inverted
                            maxRayCastDistance);
    }

    private void ScanPixelAt(uint x, uint y)
    {
        Vector3 rayVector = GetRayVector(x, y);
        RaycastHit raycastHit;

        Physics.Raycast(this.transform.position, this.transform.TransformVector(rayVector), out raycastHit, maxRayCastDistance);

        if (visualRaysEnabled)
        {
            var lineRenderer = visualRayList[x, y].GetComponent<LineRenderer>();

            visualRayTimeoutList.Add(((x, y), visualRayFadeTime));
            lineRenderer.enabled = true;

            if (raycastHit.collider != null)
            {
                if (raycastHit.collider.tag.StartsWith("Ball"))
                    CallBallHitEventListeners(raycastHit.point, raycastHit.collider.tag);

                lineRenderer.SetPosition(1, lineRenderer.GetPosition(1).normalized * raycastHit.distance); // Clip the visual ray to show the hit
            }
            else
                lineRenderer.SetPosition(1, GetRayVector(x, y));
        }

        depthBuffer[x, y] = raycastHit.collider == null ? -1 : raycastHit.distance;
    }

    private void Update()
    {
        /* Updating ray alpha */
        if (visualRaysEnabled)
            fadeVisualRays(Time.deltaTime);
    }

    private void fadeVisualRays(float deltaTime)
    {
        for (int i = (visualRayTimeoutList.Count - 1); i >= 0; i--)
        {
            var ((x, y), time) = visualRayTimeoutList[i];
            var visualRay = visualRayList[x, y];
            var lineRenderer = visualRay.GetComponent<LineRenderer>();

            time -= deltaTime; // The time field is a countdown timer

            if (time <= 0)
            {
                lineRenderer.enabled = false;
                visualRayTimeoutList.RemoveAt(i);
            }
            else
            {
                float t = time / visualRayFadeTime;

                lineRenderer.startColor = Color.Lerp(Color.clear, visualRayStartColor, t);
                lineRenderer.endColor = Color.Lerp(Color.clear, visualRayEndColor, t);

                visualRayTimeoutList[i] = ((x, y), time);
            }
        }
    }

    public float[,] GetRayCastBuffer()
    {
        return depthBuffer.Clone() as float[,];
    }

    public (uint, uint) GetRayCastBufferSize()
    {
        return (scanLineWidth, nbScanLines);
    }

    public (uint, uint) GetCurrentScanPixel()
    {
        return (currentScanX, currentScanY);
    }

    public void AddFullRefreshEventCall(onScanBufferRefresh handler)
    {
        scanBufferRefreshListeners.Add(handler);
    }

    private void callRefreshEventListeners()
    {
        foreach (onScanBufferRefresh refreshListener in scanBufferRefreshListeners)
        {
            refreshListener(GetRayCastBuffer());
        }
    }

    public void AddRaycastBallHitEventCall(onRaycastBallHit handler)
    {
        raycastBallHitsListeners.Add(handler);
    }

    private void CallBallHitEventListeners(Vector3 pos, string tag)
    {
        foreach (onRaycastBallHit ballHitListener in raycastBallHitsListeners)
        {
            ballHitListener(pos, tag);
        }
    }
}
