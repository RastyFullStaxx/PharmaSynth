using System;

/// Pure state machine for Dr. Jimenez's proctor roaming (he
/// roams the room from time to time, observing the player or looking at the shelves,
/// then walks back to his original position to run the quiz). No Unity types.
///
/// Loop: AtHome (idle a while) → WalkingOut (to the next observation point) →
/// Observing (look at it) → WalkingHome → AtHome … . Round-robin over the points
/// with a deterministic idle-time jitter (seeded — testable). When roaming is
/// disallowed (quiz/post-lab time) any outing is cut short and he heads home.
public class ProctorRoamModel
{
    public enum Phase { AtHome, WalkingOut, Observing, WalkingHome }

    public Phase Current { get; private set; } = Phase.AtHome;
    public int TargetIndex { get; private set; } = -1;

    private readonly int _pointCount;
    private readonly float _idleMin, _idleMax, _observeSeconds;
    private float _timer;
    private uint _rng;

    public ProctorRoamModel(int pointCount, float idleMin = 12f, float idleMax = 28f,
                            float observeSeconds = 5f, uint seed = 12345)
    {
        _pointCount = Math.Max(0, pointCount);
        _idleMin = idleMin; _idleMax = Math.Max(idleMin, idleMax);
        _observeSeconds = observeSeconds;
        _rng = seed == 0 ? 1u : seed;
        _timer = NextIdle();
    }

    private float NextRand01() { _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5; return (_rng & 0xFFFFFF) / (float)0x1000000; }
    private float NextIdle() => _idleMin + (_idleMax - _idleMin) * NextRand01();

    /// Advance time. `allowRoam` false (quiz on) forces a walk home. `arrived`
    /// reports the driver reached its current walk target. Returns true when the
    /// phase changed this tick (driver re-reads Current/TargetIndex).
    public bool Tick(float dt, bool allowRoam, bool arrived)
    {
        switch (Current)
        {
            case Phase.AtHome:
                if (!allowRoam || _pointCount == 0) return false;
                _timer -= dt;
                if (_timer > 0f) return false;
                TargetIndex = (TargetIndex + 1) % _pointCount;
                Current = Phase.WalkingOut;
                return true;

            case Phase.WalkingOut:
                if (!allowRoam) { Current = Phase.WalkingHome; return true; }
                if (!arrived) return false;
                Current = Phase.Observing;
                _timer = _observeSeconds;
                return true;

            case Phase.Observing:
                _timer -= dt;
                if (allowRoam && _timer > 0f) return false;
                Current = Phase.WalkingHome;
                return true;

            case Phase.WalkingHome:
                if (!arrived) return false;
                Current = Phase.AtHome;
                _timer = NextIdle();
                return true;
        }
        return false;
    }

    /// True while the driver should be moving the body.
    public bool IsWalking => Current == Phase.WalkingOut || Current == Phase.WalkingHome;
}
