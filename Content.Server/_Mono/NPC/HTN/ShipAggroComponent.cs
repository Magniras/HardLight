namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Tracks "aggro" state for an AI ship core. While aggroed, the HTN
/// blackboard key <see cref="BlackboardKey"/> is present so HTN compounds
/// can branch into a chase/attack behavior. Aggro is triggered by either
/// taking incoming ship-weapon fire on the parent grid, or by a hostile
/// shuttle target entering <see cref="AggroProximityRange"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ShipAggroComponent : Component
{
    /// <summary>
    /// Distance at which a hostile ShipNpcTarget triggers aggro by proximity.
    /// </summary>
    [DataField]
    public float AggroProximityRange = 500f;

    /// <summary>
    /// While aggroed, distance within which a hostile ShipNpcTarget keeps
    /// aggro refreshed. Aggro only fades once the target is past this
    /// range AND <see cref="AggroDuration"/> has elapsed since the last
    /// trigger. Should be larger than <see cref="AggroProximityRange"/>
    /// so the AI doesn't immediately re-engage after dropping aggro.
    /// </summary>
    [DataField]
    public float AggroLeashRange = 1000f;

    /// <summary>
    /// How long aggro persists after the most recent trigger (damage or
    /// proximity / in-leash). Refreshed every tick the trigger is still
    /// active.
    /// </summary>
    [DataField]
    public TimeSpan AggroDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Server time at which aggro currently expires. Default = never aggroed.
    /// </summary>
    [DataField]
    public TimeSpan AggroEndTime = TimeSpan.Zero;

    /// <summary>
    /// Blackboard key set on the HTN while aggroed.
    /// </summary>
    [DataField]
    public string BlackboardKey = "Aggroed";
}
