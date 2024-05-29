using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public Animator Anim;
    public Transform Head;
    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    public float playerSpeed = 2.0f;
    public float jumpHeight = 1.0f;
    public float gravityValue = -9.81f;

    public Camera playerCamera;
    public float mouseSensitivity = 100.0f;
    private float xRotation = 0f;
    public LayerMask ignore;
    private int deltaTimeCount;
    private float maxDeltaTime;
    private float averageDeltaTime;
    public Transform Hand;
    public float HandAmount;
    public float HandAmountX;
    public float HandAmountY;
    private float MovementDotY;
    private Vector3 ChunkPosition;
    private ChunkManager chunkManager;
    private bool PlayerInChunk;
    private void Start()
    {
        controller = gameObject.GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        chunkManager = ChunkManager.Instance;
    }

    void Update()
    {
        Chunk currentChunk = chunkManager.GetChunk(transform.position);

        PlayerInChunk = currentChunk != null;

        maxDeltaTime += Time.deltaTime;
        deltaTimeCount++;
        averageDeltaTime = maxDeltaTime / deltaTimeCount;

        Move();
        MouseLook();


        float playerVelocityAmount = new Vector3(playerVelocity.x, 0, playerVelocity.z).magnitude;
        float MoveAmount = Mathf.Clamp01(Mathf.Abs(Mathf.RoundToInt(playerVelocityAmount)));

        if (MoveAmount == 1)
        {
            Anim.SetBool("Moving", true);
            MovementDotY = (Vector3.Dot(transform.right, playerVelocity)) * 5;
        }
        else
        {
            Anim.SetBool("Moving", false);
        }
        Head.transform.rotation = Quaternion.Euler(playerCamera.transform.eulerAngles.z, playerCamera.transform.eulerAngles.y + 90, playerCamera.transform.eulerAngles.x);

        Hand.transform.localPosition = new Vector3(Mathf.Sin(Time.time * playerSpeed * HandAmountX * MoveAmount) * HandAmount, Mathf.Sin(Time.time * playerSpeed * HandAmountY * MoveAmount) * HandAmount, 0);
        Anim.transform.localRotation = Quaternion.Euler(0, MovementDotY + 90, 0);
    }


    void MouseLook()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity * averageDeltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity * averageDeltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Move()
    {
        if (PlayerInChunk == false) return;
        RaycastHit hit;
        groundedPlayer = Physics.SphereCast(transform.position, 0.4f, -Vector3.up, out hit, (1.1f-0.4f), ~ignore);
        if (groundedPlayer && playerVelocity.y < 0)
        {
            playerVelocity.y = 0f;
        }

        Vector3 move = transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");
        playerVelocity.x = move.x * playerSpeed;
        playerVelocity.z = move.z * playerSpeed;

        if (Input.GetButtonDown("Jump") && groundedPlayer)
        {
            playerVelocity.y += Mathf.Sqrt(jumpHeight * -3.0f * gravityValue);
        }


        playerVelocity.y += gravityValue * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);
    }
}
