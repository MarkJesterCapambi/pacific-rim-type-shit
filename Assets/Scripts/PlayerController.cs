using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float speed = 6f;
    public float jumpForce = 7f;

    private Rigidbody rb;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    void Update()
    {
        // 1. Read input via Legacy Input Manager (assignment standard)
        float horizontal = 0f;
        float vertical = 0f;
        bool jumpPressed = false;

        try
        {
            horizontal = Input.GetAxis("Horizontal");
            vertical = Input.GetAxis("Vertical");
            jumpPressed = Input.GetKeyDown(KeyCode.Space);
        }
        catch
        {
            // If Legacy Input is not active in this session
        }

#if ENABLE_INPUT_SYSTEM
        // 2. Seamless fallback to New Input System (active in Unity 6 until Editor restart)
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (Mathf.Approximately(horizontal, 0f))
            {
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
            }
            if (Mathf.Approximately(vertical, 0f))
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;
            }
            if (!jumpPressed && kb.spaceKey.wasPressedThisFrame)
            {
                jumpPressed = true;
            }
        }
#endif

        Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized;

#if UNITY_6000_0_OR_NEWER
        Vector3 velocity = rb.linearVelocity;
        velocity.x = movement.x * speed;
        velocity.z = movement.z * speed;
        rb.linearVelocity = velocity;
#else
        Vector3 velocity = rb.velocity;
        velocity.x = movement.x * speed;
        velocity.z = movement.z * speed;
        rb.velocity = velocity;
#endif

        if (jumpPressed && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }
}
