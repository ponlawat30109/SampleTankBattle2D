using FishNet;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TankBody))]
[RequireComponent(typeof(TankBodyRotationSync))]
public class TankController : NetworkBehaviour
{
    [field: SerializeField] public float MoveSpeed { get; set; } = 2f;

    private float _desiredAngle = float.NaN;
    private Vector2 _moveInput;

    private PlayerInput _playerInput;
    private PlayerInputMap _inputMap;
    private Rigidbody2D _rb;
    private TankBody _tankBody;
    private TankBodyRotationSync _tankBodyRotationSync;

    [Header("Bullet Settings")]
    [field: SerializeField] public float BulletSpeed { get; set; } = 10f;
    [field: SerializeField] public float BulletLifeTime { get; set; } = 3f;
    [field: SerializeField] public float FireCooldown { get; set; } = 1f;
    private RaycastHit2D[] _moveHits = new RaycastHit2D[4];
    private float _lastLocalShotTime = -Mathf.Infinity;
    private float _lastServerShotTime = -Mathf.Infinity;
    // [field: SerializeField] public Color BodyColor { get; set; } = Color.white;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _rb = GetComponent<Rigidbody2D>();
        _tankBody = GetComponent<TankBody>();
        _tankBodyRotationSync = GetComponent<TankBodyRotationSync>();

        _inputMap = new PlayerInputMap();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        SetTankColor();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (_playerInput != null)
            _playerInput.enabled = IsOwner;

        if (IsOwner)
        {
            _inputMap.Player.Movement.started += OnMovement;
            _inputMap.Player.Movement.performed += OnMovement;
            _inputMap.Player.Movement.canceled += OnMovement;

            _inputMap.Player.Shoot.started += OnShoot;
            _inputMap.Player.Shoot.performed += OnShoot;
            _inputMap.Player.Shoot.canceled += OnShoot;

            _inputMap.Enable();
        }

        SetTankColor();
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        if (_playerInput != null)
            _playerInput.enabled = false;

        if (IsOwner)
        {
            _inputMap.Player.Movement.started -= OnMovement;
            _inputMap.Player.Movement.performed -= OnMovement;
            _inputMap.Player.Movement.canceled -= OnMovement;

            _inputMap.Player.Shoot.started -= OnShoot;
            _inputMap.Player.Shoot.performed -= OnShoot;
            _inputMap.Player.Shoot.canceled -= OnShoot;

            _inputMap.Disable();
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        if (!IsOwner)
            return;

        if (enabled)
        {
            _inputMap.Enable();
        }
        else
        {
            _inputMap.Disable();
            _moveInput = Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        Vector2 moveDelta = MoveSpeed * Time.fixedDeltaTime * _moveInput;
        if (moveDelta.sqrMagnitude <= 0.000001f)
            return;

        float dist = moveDelta.magnitude;
        Vector2 dir = (dist > 0f) ? moveDelta / dist : Vector2.zero;

        int hitCount = _rb.Cast(dir, _moveHits, dist + 0.01f);
        if (hitCount == 0)
        {
            _rb.MovePosition(_rb.position + moveDelta);
        }
    }

    public void SetTankColor()
    {
        if (_tankBody == null || _tankBody.BodyTransform == null || _tankBody.TankColor == null || _tankBody.TankColor.Length == 0)
            return;

        int ownerIndex = 0;
        int otherIndex = (_tankBody.TankColor.Length > 1) ? 1 : 0;
        int useIndex = IsOwner ? ownerIndex : otherIndex;

        var color = _tankBody.TankColor[useIndex];
        var renderers = _tankBody.BodyTransform.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            var sprite = renderer as SpriteRenderer;
            if (sprite != null)
            {
                color.a = sprite.color.a;
                sprite.color = color;
                continue;
            }

            var currentMatColor = renderer.material.color;
            color.a = currentMatColor.a;
            renderer.material.color = color;
        }

        // BodyColor = color;
    }

    public void OnMovement(InputAction.CallbackContext context)
    {
        if (!IsOwner)
            return;

        _moveInput = context.ReadValue<Vector2>();

        if (context.canceled)
            return;

        if (_moveInput.sqrMagnitude < 0.000001f)
            return;

        float angle = Mathf.Atan2(_moveInput.x, _moveInput.y) * Mathf.Rad2Deg;

        _desiredAngle = -angle;
        _tankBodyRotationSync.RequestRotation(_desiredAngle);
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (!IsOwner)
            return;
        if (context.performed)
        {
            if (_tankBody != null && _tankBody.FirePointTransform != null)
            {
                Vector3 pos = _tankBody.FirePointTransform.transform.position;
                float angle = _tankBody.FirePointTransform.transform.eulerAngles.z;
                if (Time.time - _lastLocalShotTime >= FireCooldown)
                {
                    _lastLocalShotTime = Time.time;
                    Server_RequestSpawnBullet(pos, angle);
                }
            }
        }
    }

    [ServerRpc]
    private void Server_RequestSpawnBullet(Vector3 position, float zAngle, Color bulletColor = default)
    {
        if (BulletPoolController.Instance == null)
            return;

        if (Time.time - _lastServerShotTime < FireCooldown)
            return;
        _lastServerShotTime = Time.time;

        Vector3 forward = Quaternion.Euler(0f, 0f, zAngle) * Vector3.up;
        Vector3 spawnPos = position + forward * 0.35f;

        BulletPoolController.Instance.SpawnNetworkBullet(spawnPos, Quaternion.Euler(0f, 0f, zAngle), Owner, BulletSpeed, BulletLifeTime, gameObject, bulletColor);
    }
}
