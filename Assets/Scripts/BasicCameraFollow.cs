using UnityEngine;
using System.Collections;

public class BasicCameraFollow : MonoBehaviour 
{

	private Vector3 startingPosition;
	public Transform followTarget;
	private Vector3 targetPos;
	public float smoothTime = 0.15f;
	public float maxSpeed = 20f;
	private Vector3 velocity;
	
	void Start()
	{
		startingPosition = transform.position;
	}

	void Update () 
	{
		if(followTarget != null)
		{
			targetPos = new Vector3(followTarget.position.x, followTarget.position.y, transform.position.z);
			transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime, maxSpeed, Time.deltaTime);
		}
	}
}
