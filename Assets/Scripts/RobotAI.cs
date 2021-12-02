using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotAI : MonoBehaviour
{
    public enum RobotMode
    {
        STOPPED,
        WANDERING,
        AVOIDING,
        TRAVELLING
    }

    public RobotMode robotMode = RobotMode.WANDERING;
    private Vector3 travelTarget;
    private int travelTargetIndex;

    public float gravity = 9.81f;
    public float speed = 2f;

    private float lastDeltaTime;

    Quaternion startRotation;
    Quaternion endRotation;

    public List<(Vector3, string)> targetBallsFound = new List<(Vector3, string)>();

    public List<RobotAI> otherRobots;

    public float minBallDistanceThreshold = 1.5f; // Distance between two successful raycasts to consider them being separate

    public RaycastCamera raycastCamera;

    // Start is called before the first frame update
    void Start()
    {
        Vector3 forward_world = transform.TransformDirection(Vector3.forward);
        Vector2 mouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        raycastCamera.AddRaycastBallHitEventCall(this.OnRayCastBallHit);
    }

    public void OnRayCastBallHit(Vector3 pos, string tag)
    {
        if (TestBallTagForTarget(tag))
            StoreFoundBall(pos, tag);
        else
            BroadcastBallPosition(pos, tag);
    }

    private int FindStoredBallIndex(Vector3 targetPos)
    {
        for (int i = 0; i < targetBallsFound.Count; i++)
        {
            var (pos, tag) = targetBallsFound[i];
            if ((pos - targetPos).magnitude <= minBallDistanceThreshold)
                return i;
        }
        return -1;
    }

    private bool TestBallTagForTarget(string tag)
    {
        if (tag.StartsWith("Ball"))
        {
            if (tag.EndsWith(this.transform.tag.Substring("Robot".Length)))
            {
                return true;
            }
        }
        return false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.rigidbody != null)
        {
            if (TestBallTagForTarget(collision.gameObject.tag))
            {
                print(this.transform.tag + " found " + collision.gameObject.transform.tag);
                Destroy(collision.gameObject);
                robotMode = RobotMode.WANDERING;
                targetBallsFound.RemoveAt(travelTargetIndex);
            }
        }
    }

    private void BroadcastBallPosition(Vector3 pos, string tag)
    {
        foreach (var r in otherRobots)
        {
            r.ReceiveBallPosition(pos, tag);
        }
    }

    public void ReceiveBallPosition(Vector3 pos, string tag)
    {
        if (TestBallTagForTarget(tag))
            StoreFoundBall(pos, tag);
    }

    private void StoreFoundBall(Vector3 foundPos, string foundTag)
    {
        if (FindStoredBallIndex(foundPos) == -1)
            targetBallsFound.Add((foundPos, foundTag));
    }

    private int GetNearestBall()
    {
        float minDist = float.PositiveInfinity;
        int minDistIndex = 0;

        for (int i = 0; i < targetBallsFound.Count; i++)
        {
            float currentDist = (targetBallsFound[i].Item1 - transform.position).magnitude;
            if(currentDist < minDist)
                minDist = currentDist;
                minDistIndex = i;
        }

        return minDistIndex;
    }

    public void FixedUpdate()
    {
        lastDeltaTime += Time.fixedDeltaTime;

        switch (robotMode)
        {
            case RobotMode.STOPPED:
                return;
            case RobotMode.WANDERING:
                while (lastDeltaTime >= 1.5f)
                {
                    lastDeltaTime -= 5f;
                    startRotation = transform.rotation;
                    endRotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y + Random.Range(-180, 180), transform.rotation.z);
                }

                if (targetBallsFound.Count > 0)
                {
                    robotMode = RobotMode.TRAVELLING;
                }
                break;
            case RobotMode.TRAVELLING:
                while (lastDeltaTime >= 1.5f)
                {

                    travelTargetIndex = GetNearestBall();
                    travelTarget = targetBallsFound[travelTargetIndex].Item1;
                    
                    lastDeltaTime -= 1.5f;
                    Vector3 deltaPos = travelTarget - this.transform.position;

                    endRotation = startRotation = transform.rotation;
                    endRotation.SetLookRotation(deltaPos);
                    endRotation.z = endRotation.x = 0;
                }
                break;
        }

        transform.rotation = Quaternion.Slerp(startRotation, endRotation, lastDeltaTime);
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 forward_world = transform.TransformDirection(Vector3.forward);
        transform.position += forward_world * speed * Time.deltaTime;
    }
}
