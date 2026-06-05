using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

[RequireComponent(typeof(Rigidbody2D))]
public class IsometricPlayerMovementController : MonoBehaviour
{

    public float movementSpeed = 1f;
    IsometricCharacterRenderer isoRenderer;

    Rigidbody2D rbody;

    private Phase2PlayerController phase2Controller;

    private void Awake()
    {
        rbody = GetComponent<Rigidbody2D>();
        rbody.gravityScale = 0;
        rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        isoRenderer = GetComponentInChildren<IsometricCharacterRenderer>();
        phase2Controller = GetComponent<Phase2PlayerController>();
    }


    // Update is called once per frame
    void FixedUpdate()
    {
        Vector2 currentPos = rbody.position;
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector2 inputVector = new Vector2(horizontalInput, verticalInput);
        inputVector = Vector2.ClampMagnitude(inputVector, 1);
        Vector2 movement = inputVector * movementSpeed;
        Vector2 newPos = currentPos + movement * Time.fixedDeltaTime;
        if (isoRenderer != null)
        {
            isoRenderer.SetDirection(movement);
        }
        rbody.MovePosition(newPos);
    }
}
