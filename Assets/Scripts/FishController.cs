using UnityEngine;

namespace Seeman
{
    public class FishController : MonoBehaviour
    {
        public FishState State { get; private set; } = FishState.Wander;

        [Header("Swim")]
        public float WanderSpeed = 0.28f;
        public float ApproachSpeed = 0.85f;
        public float TurnSpeed = 2.0f;
        public float CamDistance = 1.05f;

        Vector3 _target;
        Animator _anim;
        Transform _cam;
        FoodPellet _food;
        float _conversingLinger;

        void Start()
        {
            _anim = GetComponentInChildren<Animator>();
            if (Camera.main != null) _cam = Camera.main.transform;
            PickNewTarget();
        }

        public void Summon()
        {
            if (State == FishState.Eating || State == FishState.Conversing) return;
            State = FishState.Approach;
        }

        public void OnConversationStart()
        {
            State = FishState.Conversing;
            _conversingLinger = 12f;
        }

        public void KeepNear() { _conversingLinger = 12f; }

        public void EatFood(FoodPellet p)
        {
            if (p == null) return;
            _food = p;
            State = FishState.Eating;
        }

        Vector3 CamFrontPoint()
        {
            if (_cam == null) return _target;
            return _cam.position + _cam.forward * CamDistance;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (State)
            {
                case FishState.Wander:
                    MoveTowards(_target, WanderSpeed, dt);
                    if ((transform.position - _target).sqrMagnitude < 0.04f) PickNewTarget();
                    break;

                case FishState.Approach:
                    MoveTowards(CamFrontPoint(), ApproachSpeed, dt);
                    if ((transform.position - CamFrontPoint()).sqrMagnitude < 0.025f)
                    {
                        State = FishState.Conversing;
                        _conversingLinger = 8f;
                    }
                    break;

                case FishState.Conversing:
                    Vector3 hold = CamFrontPoint();
                    hold.y += Mathf.Sin(Time.time * 1.3f) * 0.02f;
                    transform.position = Vector3.Lerp(transform.position, hold, dt * 2.5f);
                    FaceCamera(dt);
                    bool busy = GameManager.I != null &&
                                (GameManager.I.State == GameState.Listening ||
                                 GameManager.I.State == GameState.Thinking ||
                                 GameManager.I.State == GameState.Speaking);
                    if (busy) _conversingLinger = 8f;
                    else
                    {
                        _conversingLinger -= dt;
                        if (_conversingLinger <= 0f) { State = FishState.Wander; PickNewTarget(); }
                    }
                    break;

                case FishState.Eating:
                    if (_food == null) { State = FishState.Wander; PickNewTarget(); break; }
                    MoveTowards(_food.transform.position, ApproachSpeed * 1.4f, dt);
                    if ((transform.position - _food.transform.position).sqrMagnitude < 0.03f)
                    {
                        _food.Consume();
                        _food = null;
                        State = FishState.Wander;
                        PickNewTarget();
                    }
                    break;
            }

            if (_anim != null)
            {
                float spd = State == FishState.Conversing ? 0.55f : (State == FishState.Wander ? 1f : 1.9f);
                _anim.speed = Mathf.Lerp(_anim.speed, spd, dt * 3f);
            }
        }

        void MoveTowards(Vector3 target, float speed, float dt)
        {
            Vector3 dir = target - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, dt * TurnSpeed);
                transform.position += transform.forward * speed * dt;
            }
            ClampToTank();
        }

        void FaceCamera(float dt)
        {
            if (_cam == null) return;
            Vector3 toCam = _cam.position - transform.position;
            toCam.y *= 0.3f;
            if (toCam.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, dt * TurnSpeed * 1.5f);
        }

        void ClampToTank()
        {
            if (GameManager.I == null) return;
            Bounds b = GameManager.I.TankBounds;
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
            p.y = Mathf.Clamp(p.y, b.min.y, b.max.y);
            p.z = Mathf.Clamp(p.z, b.min.z, b.max.z);
            transform.position = p;
        }

        void PickNewTarget()
        {
            Bounds b = GameManager.I != null ? GameManager.I.TankBounds
                : new Bounds(Vector3.up * 0.5f, new Vector3(2f, 0.8f, 1f));
            _target = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                Random.Range(b.min.z, b.max.z));
        }
    }
}
