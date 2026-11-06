using UnityEngine;

/// Keeps Pharmee's face in sync with what he's doing (expressions
/// depend on what he's saying; HAPPY by default, especially while following you).
/// PharmeeBrain/PharmeeGatekeeper set the per-line expression when a line starts;
/// this component resets the face to its default (happy) when the line ENDS, so he
/// never gets stuck on a warning face while floating after the player.
public class PharmeeMood : MonoBehaviour
{
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private MonoBehaviour faceBehaviour;   // IPharmeeFace (PharmeeFace)

    private PharmeeFace _face;
    private bool _subscribed;

    public void Bind(NPCNarrationController n, PharmeeFace f)
    {
        Unsubscribe();
        narration = n; faceBehaviour = f; _face = f;
        Subscribe();
    }

    /// The gate conversation's mood per state (pure — testable): friendly through
    /// the flow, warning when supplies run dry, celebratory on debrief/unlocks.
    public static PharmeeFaceExpression ExpressionForGate(GateState s)
    {
        switch (s)
        {
            case GateState.SupplyPrompt: return PharmeeFaceExpression.Warning;
            case GateState.ThresholdWarn: return PharmeeFaceExpression.Warning;
            case GateState.Debrief:
            case GateState.UnlockAnnounce:
            case GateState.LabTour:
            case GateState.Blocked:
            case GateState.QuizIntro:   // celebratory walk-over to the review corner
            case GateState.ModeChoice: return PharmeeFaceExpression.Happy;
            default: return PharmeeFaceExpression.Neutral;
        }
    }

    private void OnEnable()
    {
        _face = faceBehaviour as PharmeeFace;
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || narration == null) return;
        narration.LineEnded += OnLineEnded;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || narration == null) return;
        narration.LineEnded -= OnLineEnded;
        _subscribed = false;
    }

    private void OnLineEnded()
    {
        if (_face != null) _face.ResetToDefault();
    }
}
