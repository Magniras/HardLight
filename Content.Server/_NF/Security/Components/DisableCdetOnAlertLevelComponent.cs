namespace Content.Server._NF.Security.Components;

[RegisterComponent]
public sealed partial class DisableCdetOnAlertLevelComponent : Component
{
    [DataField]
    public HashSet<string> DisabledAlertLevels =
    [
        "red",
        "delta",
        "violet",
        "gamma",
        "omicron",
    ];

    [ViewVariables]
    public bool RestoreAfterLockdown;
}