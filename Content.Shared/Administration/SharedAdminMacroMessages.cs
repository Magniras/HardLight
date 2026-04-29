using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

[Serializable, NetSerializable]
public sealed class RequestSharedAdminMacrosMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class UpsertSharedAdminMacroMessage : EntityEventArgs
{
    public string Name { get; }
    public string Command { get; }

    public UpsertSharedAdminMacroMessage(string name, string command)
    {
        Name = name;
        Command = command;
    }
}

[Serializable, NetSerializable]
public sealed class DeleteSharedAdminMacroMessage : EntityEventArgs
{
    public string Name { get; }

    public DeleteSharedAdminMacroMessage(string name)
    {
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class SharedAdminMacroState
{
    public string Name { get; }
    public string Command { get; }
    public string UpdatedBy { get; }

    public SharedAdminMacroState(string name, string command, string updatedBy)
    {
        Name = name;
        Command = command;
        UpdatedBy = updatedBy;
    }
}

[Serializable, NetSerializable]
public sealed class SharedAdminMacrosStateMessage : EntityEventArgs
{
    public SharedAdminMacroState[] Macros { get; }

    public SharedAdminMacrosStateMessage(SharedAdminMacroState[] macros)
    {
        Macros = macros;
    }
}