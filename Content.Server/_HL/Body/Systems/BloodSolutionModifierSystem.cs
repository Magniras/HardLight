using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._HL.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server._HL.Body.Systems;

public sealed class BloodSolutionModifierSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodSolutionModifierComponent, ComponentStartup>(OnStartup, after: [typeof(BloodstreamSystem)]);
        SubscribeLocalEvent<BloodSolutionModifierComponent, MapInitEvent>(OnMapInit, after: [typeof(BloodstreamSystem)]);
    }

    private void OnStartup(Entity<BloodSolutionModifierComponent> ent, ref ComponentStartup args)
    {
        ApplyModifier(ent);
    }

    private void OnMapInit(Entity<BloodSolutionModifierComponent> ent, ref MapInitEvent args)
    {
        ApplyModifier(ent);
    }

    public void ApplyModifier(Entity<BloodSolutionModifierComponent> ent)
    {
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream)
            || !_solution.ResolveSolution(ent.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out _)
            || bloodstream.BloodSolution is not { } bloodSolutionEntity)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ent.Comp.BloodReagent))
            _bloodstream.ChangeBloodReagent(ent.Owner, ent.Comp.BloodReagent, bloodstream);

        if (ent.Comp.ClearExisting)
            _solution.RemoveAllSolution(bloodSolutionEntity);

        if (ent.Comp.Solution.Volume <= 0)
            return;

        var bloodData = _bloodstream.GetEntityBloodData(ent.Owner);
        foreach (var reagent in ent.Comp.Solution.Contents)
        {
            List<ReagentData>? reagentData = reagent.Reagent.Data;
            if (reagent.Reagent.Prototype == bloodstream.BloodReagent)
                reagentData = bloodData;

            _solution.TryAddReagent(
                bloodSolutionEntity,
                reagent.Reagent.Prototype,
                reagent.Quantity,
                out _,
                ent.Comp.Solution.Temperature,
                reagentData);
        }
    }
}