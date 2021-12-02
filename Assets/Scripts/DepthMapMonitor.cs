using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DepthMapMonitor : MonoBehaviour
{
    private Texture2D depthMap2DTexture;

    public GameObject rayCastCamera;
    private RaycastCamera raycastCameraScript;

    // Start is called before the first frame update
    void Start()
    {
        //rayCastCamera = this.transform.parent.Find("CameraOrigin").gameObject;
        raycastCameraScript = rayCastCamera.GetComponent(typeof(RaycastCamera)) as RaycastCamera;

        var (width, height) = raycastCameraScript.GetRayCastBufferSize();

        depthMap2DTexture = new Texture2D((int)width, (int)height);
        depthMap2DTexture.filterMode = FilterMode.Point;
        this.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));

        raycastCameraScript.AddFullRefreshEventCall(this.updateDepthTexture);
    }

    private Color getMappedColorFromDepth(float minDepth, float maxDepth, float depth)
    {   
        float t = Mathf.Pow(1 - (depth - minDepth) / (maxDepth - minDepth), 2); // Inverted colormap
        // Squared for better depth perception
        return Color.Lerp(
                            Color.Lerp(Color.white, Color.yellow, t),
                            Color.Lerp(Color.red, Color.blue, t),
                            t);
    }

    private (float, float) getMinMaxDepths(float[,] depthMap)
    {
        float min = float.PositiveInfinity;
        float max = 0;
        for (int y = 0; y < depthMap2DTexture.height; y++)
        {
            for (int x = 0; x < depthMap2DTexture.height; x++)
            {
                float val = depthMap[x, y];
                if (val < min && val != -1)
                    min = val;
                else if (val > max)
                    max = val;
            }
        }

        return (min, max);
    }

    private void updateDepthTexture(float[,] rayCastBuffer)
    {
        var (minDepth, maxDepth) = getMinMaxDepths(rayCastBuffer);
        for (int j = 0; j < depthMap2DTexture.height; j++)
        {
            for (int i = 0; i < depthMap2DTexture.height; i++)
            {
                var val = rayCastBuffer[i, j];
                Color pixelColor = (val == -1) ? Color.black : getMappedColorFromDepth(minDepth, maxDepth, val);

                depthMap2DTexture.SetPixel(i, depthMap2DTexture.height - j - 1, pixelColor);
            }
        }

        this.GetComponent<Renderer>().material.SetTexture("_MainTex", depthMap2DTexture);

        depthMap2DTexture.Apply();
    }
}
