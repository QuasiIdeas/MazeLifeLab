using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders (L/R важно)")]
    public WheelCollider wheelFL; // Front Left
    public WheelCollider wheelFR; // Front Right
    public WheelCollider wheelRL; // Rear Left
    public WheelCollider wheelRR; // Rear Right

    [Header("Wheel Meshes (визуал)")]
    public Transform wheelFLMesh;
    public Transform wheelFRMesh;
    public Transform wheelRLMesh;
    public Transform wheelRRMesh;

    [Header("Drive & Steering")]
    public float motorTorque = 600f;        // 300–800 для начала
    public float maxSteerAngle = 30f;       // угол поворота передних
    public float brakeTorque = 1500f;       // Space — тормоз

    [Header("Suspension")]
    public float suspensionDistance = 0.25f;  // 0.15–0.35
    public float spring = 35000f;             // зависят от массы
    public float damper = 4500f;
    public float targetPosition = 0.5f;       // 0..1 (середина хода)

    [Header("Friction")]
    public float forwardStiffness = 1.5f;    // 1.0–2.0
    public float sidewaysStiffness = 2.0f;   // 1.5–3.0

    [Header("Stability")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.4f, 0f);
    public float antiRoll = 5000f;           // 0 чтобы отключить

    Rigidbody rb;
    float steerInput;
    float throttleInput;
    bool braking;
    // When true, external controller (executor) supplies steer/motor/brake via ApplyExternalControl
    public bool ExternalControl = false;
    // last external commands (for visualization / fixed update)
    float extSteerDeg = 0f;
    float extMotor = 0f;
    float extBrake = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = Mathf.Max(rb.mass, 900f); // 900–1400 кг
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // опустить центр масс — меньше «козлит»
        rb.centerOfMass += centerOfMassOffset;

        // Безопасные настройки подвески/фрикций
        SetupWheel(wheelFL);
        SetupWheel(wheelFR);
        SetupWheel(wheelRL);
        SetupWheel(wheelRR);
    }

    void SetupWheel(WheelCollider wc)
    {
        if (!wc) return;

        wc.suspensionDistance = suspensionDistance;

        var sus = wc.suspensionSpring;
        sus.spring = spring;
        sus.damper = damper;
        sus.targetPosition = targetPosition;
        wc.suspensionSpring = sus;

        WheelFrictionCurve fwd = wc.forwardFriction;
        fwd.asymptoteSlip = 0.8f; fwd.asymptoteValue = 0.75f;
        fwd.extremumSlip = 0.4f;  fwd.extremumValue = 1.0f;
        fwd.stiffness = forwardStiffness;
        wc.forwardFriction = fwd;

        WheelFrictionCurve sdw = wc.sidewaysFriction;
        sdw.asymptoteSlip = 0.5f; sdw.asymptoteValue = 0.75f;
        sdw.extremumSlip = 0.2f;  sdw.extremumValue = 1.0f;
        sdw.stiffness = sidewaysStiffness;
        wc.sidewaysFriction = sdw;
    }

    void Update()
    {
        // только читаем ввод
        steerInput = Input.GetAxisRaw("Horizontal"); // A/D
        throttleInput = Input.GetAxisRaw("Vertical"); // W/S
        braking = Input.GetKey(KeyCode.Space);

        // обновляем позы мешей (визуал лучше в Update/LateUpdate)
        UpdateWheelVisuals();
    }

    void FixedUpdate()
    {
        // ВСЮ физику — в FixedUpdate
        HandleSteering();
        HandleDrive();
        HandleBrakes();
        ApplyAntiRoll();
    }

    void HandleSteering()
    {
        if (ExternalControl)
        {
            wheelFL.steerAngle = extSteerDeg;
            wheelFR.steerAngle = extSteerDeg;
            return;
        }

        float steer = steerInput * maxSteerAngle;
        wheelFL.steerAngle = steer;
        wheelFR.steerAngle = steer;
    }

    void HandleDrive()
    {
        if (ExternalControl)
        {
            wheelRL.motorTorque = extMotor;
            wheelRR.motorTorque = extMotor;
            return;
        }

        // Начни с заднего привода — устойчивее
        float torque = throttleInput * motorTorque;
        wheelRL.motorTorque = torque;
        wheelRR.motorTorque = torque;

        // Если хочешь полный: раскомментируй
        // wheelFL.motorTorque = torque * 0.5f;
        // wheelFR.motorTorque = torque * 0.5f;
    }

    void HandleBrakes()
    {
        if (ExternalControl)
        {
            wheelFL.brakeTorque = extBrake;
            wheelFR.brakeTorque = extBrake;
            wheelRL.brakeTorque = extBrake;
            wheelRR.brakeTorque = extBrake;
            if (extBrake > 0f)
            {
                wheelFL.motorTorque = 0f; wheelFR.motorTorque = 0f; wheelRL.motorTorque = 0f; wheelRR.motorTorque = 0f;
            }
            return;
        }

        float bt = braking ? brakeTorque : 0f;
        wheelFL.brakeTorque = bt;
        wheelFR.brakeTorque = bt;
        wheelRL.brakeTorque = bt;
        wheelRR.brakeTorque = bt;

        // когда тормозим — убираем моторный момент
        if (braking)
        {
            wheelFL.motorTorque = 0f;
            wheelFR.motorTorque = 0f;
            wheelRL.motorTorque = 0f;
            wheelRR.motorTorque = 0f;
        }
    }

    void ApplyAntiRoll()
    {
        if (antiRoll <= 0f) return;

        ApplyAntiRollForAxle(wheelFL, wheelFR);
        ApplyAntiRollForAxle(wheelRL, wheelRR);
    }

    void ApplyAntiRollForAxle(WheelCollider left, WheelCollider right)
    {
        bool groundedL = left.GetGroundHit(out WheelHit hitL);
        bool groundedR = right.GetGroundHit(out WheelHit hitR);

        float travelL = 1.0f;
        float travelR = 1.0f;

        if (groundedL)
            travelL = (-left.transform.InverseTransformPoint(hitL.point).y - left.radius) / left.suspensionDistance;
        if (groundedR)
            travelR = (-right.transform.InverseTransformPoint(hitR.point).y - right.radius) / right.suspensionDistance;

        float antiRollForce = (travelL - travelR) * antiRoll;

        if (groundedL)
            rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
        if (groundedR)
            rb.AddForceAtPosition(right.transform.up *  antiRollForce, right.transform.position);
    }

    void UpdateWheelVisuals()
    {
        UpdateWheelPose(wheelFL, wheelFLMesh);
        UpdateWheelPose(wheelFR, wheelFRMesh);
        UpdateWheelPose(wheelRL, wheelRLMesh);
        UpdateWheelPose(wheelRR, wheelRRMesh);
    }

    void UpdateWheelPose(WheelCollider col, Transform mesh)
    {
        if (!col || !mesh) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    /// <summary>
    /// Apply external control values (called by executors). steerDeg in degrees.
    /// When invoked, ExternalControl will be used to apply these commands in FixedUpdate.
    /// </summary>
    public void ApplyExternalControl(float steerDeg, float motor, float brake)
    {
        ExternalControl = true;
        extSteerDeg = steerDeg;
        extMotor = motor;
        extBrake = brake;
    }

    /// <summary>Disable external control and return to player input.</summary>
    public void ReleaseExternalControl()
    {
        ExternalControl = false;
        extSteerDeg = extMotor = extBrake = 0f;
    }
}
