namespace Skill.Suite.Components.Pages.Sessions;

/// <summary>Which button closed the marking dialog.</summary>
public enum MarkingAction
{
    /// <summary>Bring this competitor's marking containers up for the machine named in the dialog.</summary>
    Start,

    /// <summary>Remove this competitor's marking containers, freeing their ports and their proxy route.</summary>
    Stop,
}
