using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 20f;
    public float crouchSpeed = 3.5f;
    public float proneSpeed = 1.8f;
    public float jumpHeight = 2f;
    public float gravity = -20f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 100f;
    public Transform playerCam;
    private float xRotation = 0f;

    [Header("Crouch")]
    public bool enableCrouch = true;
    public float crouchHeight = 1.2f;
    public float crouchCameraHeight = 0.9f;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Prone")]
    public bool enableProne = true;
    public float proneHeight = 0.6f;
    public float proneCamHeight = 0.35f;
    public KeyCode proneKey = KeyCode.C;        // You set this to C

    [Header("Sprint Slide")]
    public bool enableSlide = true;
    public float slideDuration = 0.65f;
    public float slideSpeed = 18f;
    public float slideCooldown = 1.5f;

    [Header("Shoot/Attack")]
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float bulletSpeed = 30f;
    public float fireRate = 0.2f;

    private CharacterController cc;
    private Vector3 velocity;
    private float nextFireTime = 0f;
    private float nextSlideTime = 0f;

    private enum Posture { Standing, Crouching, Prone }
    private Posture currentPosture = Posture.Standing;

    private bool isSliding = false;
    private float slideTimer = 0f;

    private float currentHeight;
    private float targetCamHeight;

    void Start()
    {
        cc = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerCam == null)
            playerCam = Camera.main.transform;

        currentHeight = cc.height;
        targetCamHeight = 1.6f;
    }

    void Update()
    {
        HandlePostureInput();
        Movement();
        MouseLook();
        Shooting();

        // Debug
        // Debug.Log("Current Posture: " + currentPosture + " | Sliding: " + isSliding);
    }

    void HandlePostureInput()
    {
        // === SPRINT SLIDE (Highest Priority) ===
        if (enableSlide && Input.GetKeyDown(crouchKey) &&
            Input.GetKey(KeyCode.LeftShift) &&
            currentPosture == Posture.Standing &&
            cc.isGrounded &&
            Time.time >= nextSlideTime)
        {
            StartSlide();
            return;                    // Prevent crouch from triggering same frame
        }

        // === PRONE TOGGLE ===
        if (enableProne && Input.GetKeyDown(proneKey))
        {
            currentPosture = (currentPosture == Posture.Prone) ? Posture.Standing : Posture.Prone;
            if (isSliding) EndSlide();
            return;
        }

        // === CROUCH TOGGLE ===
        if (enableCrouch && Input.GetKeyDown(crouchKey))
        {
            if (currentPosture == Posture.Prone)
            {
                currentPosture = Posture.Crouching;     // Crouch key exits prone into crouch (feels good)
            }
            else
            {
                currentPosture = (currentPosture == Posture.Crouching) ? Posture.Standing : Posture.Crouching;
            }
        }
    }

    void Movement()
    {
        bool grounded = cc.isGrounded;
        if (grounded && velocity.y < 0)
            velocity.y = -2f;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // Speed logic
        float currentSpeed = walkSpeed;

        if (isSliding)
        {
            currentSpeed = slideSpeed;
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0) EndSlide();
        }
        else
        {
            switch (currentPosture)
            {
                case Posture.Crouching:
                    currentSpeed = crouchSpeed;
                    break;
                case Posture.Prone:
                    currentSpeed = proneSpeed;
                    break;
                case Posture.Standing:
                    if (Input.GetKey(KeyCode.LeftShift))
                        currentSpeed = sprintSpeed;
                    break;
            }
        }

        cc.Move(move * currentSpeed * Time.deltaTime);

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && grounded && !isSliding && currentPosture != Posture.Prone)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (currentPosture == Posture.Crouching)
                currentPosture = Posture.Standing;
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);

        // Smooth transitions
        float targetHeight = GetTargetHeight();
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, 12f * Time.deltaTime);

        cc.height = currentHeight;
        cc.center = new Vector3(0, currentHeight / 2f, 0);

        // Camera
        targetCamHeight = GetTargetCamHeight();
        Vector3 camPos = playerCam.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetCamHeight, 12f * Time.deltaTime);
        playerCam.localPosition = camPos;
    }

    float GetTargetHeight()
    {
        switch (currentPosture)
        {
            case Posture.Crouching: return crouchHeight;
            case Posture.Prone: return proneHeight;
            default: return 2f;
        }
    }

    float GetTargetCamHeight()
    {
        switch (currentPosture)
        {
            case Posture.Crouching: return crouchCameraHeight;
            case Posture.Prone: return proneCamHeight;
            default: return 1.6f;
        }
    }

    void StartSlide()
    {
        isSliding = true;
        slideTimer = slideDuration;
        nextSlideTime = Time.time + slideDuration + slideCooldown;
        currentPosture = Posture.Crouching;
    }

    void EndSlide()
    {
        isSliding = false;
    }

    void MouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        playerCam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Shooting()
    {
        if (Input.GetKey(KeyCode.Mouse0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Rigidbody rb = bullet.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = firePoint.forward * bulletSpeed;
            Destroy(bullet, 3f);
        }
    }
}