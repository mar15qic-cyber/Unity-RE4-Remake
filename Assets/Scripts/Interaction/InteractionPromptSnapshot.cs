using GameArchitecture;
using UnityEngine;

public enum InteractionPromptKind
{
    None,
    BodyTechnique,
    GroundFinisher,
    Assassination,
    Interact,
    EmergencyDefense,
    GroundFinisherBlocked,
    WindowLadderPush
}

public readonly struct InteractionPromptSnapshot
{
    public readonly Component Target;
    public readonly ActionHandle Action;
    public readonly InteractionPromptKind Kind;
    public readonly bool CanExecute;
    public readonly string InputAction;
    public readonly string Content;
    public readonly Vector3 Position;
    public readonly int Technique;

    public InteractionPromptSnapshot(Component target, ActionHandle action, InteractionPromptKind kind, bool canExecute,
        string inputAction, string content, Vector3 position, int technique = 0)
    {
        Target = target;
        Action = action;
        Kind = kind;
        CanExecute = canExecute;
        InputAction = inputAction;
        Content = content;
        Position = position;
        Technique = technique;
    }

    public bool IsValid
    {
        get
        {
            if (Target == null || !Target.gameObject.activeInHierarchy)
            {
                return false;
            }
            if (Target is VillagerQTEParticipant participant)
            {
                return participant.IsAlive && participant.controller != null && participant.controller.actionCoordinator != null
                    && participant.controller.actionCoordinator.CurrentHandle == Action;
            }
            return true;
        }
    }
}
