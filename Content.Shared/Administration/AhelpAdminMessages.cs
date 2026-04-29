using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

[Serializable, NetSerializable]
public sealed class RequestAhelpAdminStateMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class SetAhelpAutoReplyEnabledMessage : EntityEventArgs
{
    public bool Enabled { get; }

    public SetAhelpAutoReplyEnabledMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class SetAhelpPanicAutoReplyEnabledMessage : EntityEventArgs
{
    public bool Enabled { get; }

    public SetAhelpPanicAutoReplyEnabledMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class SetAhelpPanicAutoReplyTemplateMessage : EntityEventArgs
{
    public string Template { get; }

    public SetAhelpPanicAutoReplyTemplateMessage(string template)
    {
        Template = template;
    }
}

[Serializable, NetSerializable]
public sealed class SetAhelpTriageEnabledMessage : EntityEventArgs
{
    public bool Enabled { get; }

    public SetAhelpTriageEnabledMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class AddOrRestoreAhelpCategoryMessage : EntityEventArgs
{
    public string Category { get; }
    public string Template { get; }
    public string Keywords { get; }

    public AddOrRestoreAhelpCategoryMessage(string category, string template, string keywords)
    {
        Category = category;
        Template = template;
        Keywords = keywords;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveAhelpCategoryMessage : EntityEventArgs
{
    public string Category { get; }

    public RemoveAhelpCategoryMessage(string category)
    {
        Category = category;
    }
}

[Serializable, NetSerializable]
public sealed class SetAhelpAutoReplyTemplateMessage : EntityEventArgs
{
    public string Category { get; }
    public string Template { get; }

    public SetAhelpAutoReplyTemplateMessage(string category, string template)
    {
        Category = category;
        Template = template;
    }
}

[Serializable, NetSerializable]
public sealed class ResetAhelpAutoReplyTemplateMessage : EntityEventArgs
{
    public string Category { get; }

    public ResetAhelpAutoReplyTemplateMessage(string category)
    {
        Category = category;
    }
}

[Serializable, NetSerializable]
public sealed class SetAhelpTriageKeywordsMessage : EntityEventArgs
{
    public string Category { get; }
    public string Keywords { get; }

    public SetAhelpTriageKeywordsMessage(string category, string keywords)
    {
        Category = category;
        Keywords = keywords;
    }
}

[Serializable, NetSerializable]
public sealed class ResetAhelpTriageKeywordsMessage : EntityEventArgs
{
    public string Category { get; }

    public ResetAhelpTriageKeywordsMessage(string category)
    {
        Category = category;
    }
}

[Serializable, NetSerializable]
public sealed class SetAhelpAutoReplyBotNameMessage : EntityEventArgs
{
    public string BotName { get; }

    public SetAhelpAutoReplyBotNameMessage(string botName)
    {
        BotName = botName;
    }
}

[Serializable, NetSerializable]
public sealed class SetAhelpCategoryAutoReplyEnabledMessage : EntityEventArgs
{
    public string Category { get; }
    public bool Enabled { get; }

    public SetAhelpCategoryAutoReplyEnabledMessage(string category, bool enabled)
    {
        Category = category;
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class SetAhelpCategoryTriageEnabledMessage : EntityEventArgs
{
    public string Category { get; }
    public bool Enabled { get; }

    public SetAhelpCategoryTriageEnabledMessage(string category, bool enabled)
    {
        Category = category;
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class AhelpAdminCategoryState
{
    public string Name { get; }
    public string Template { get; }
    public string Keywords { get; }
    public bool IsDefault { get; }
    public bool HasAutoReply { get; }
    public bool HasTriage { get; }
    public bool AutoReplyEnabled { get; }
    public bool TriageEnabled { get; }

    public AhelpAdminCategoryState(
        string name,
        string template,
        string keywords,
        bool isDefault,
        bool hasAutoReply,
        bool hasTriage,
        bool autoReplyEnabled,
        bool triageEnabled)
    {
        Name = name;
        Template = template;
        Keywords = keywords;
        IsDefault = isDefault;
        HasAutoReply = hasAutoReply;
        HasTriage = hasTriage;
        AutoReplyEnabled = autoReplyEnabled;
        TriageEnabled = triageEnabled;
    }
}

[Serializable, NetSerializable]
public sealed class AhelpAdminConfigState
{
    public bool AutoReplyEnabled { get; }
    public bool PanicAutoReplyEnabled { get; }
    public bool TriageEnabled { get; }
    public string AutoReplyBotName { get; }
    public string PanicAutoReplyTemplate { get; }
    public AhelpAdminCategoryState[] Categories { get; }

    public AhelpAdminConfigState(
        bool autoReplyEnabled,
        bool panicAutoReplyEnabled,
        bool triageEnabled,
        string autoReplyBotName,
        string panicAutoReplyTemplate,
        AhelpAdminCategoryState[] categories)
    {
        AutoReplyEnabled = autoReplyEnabled;
        PanicAutoReplyEnabled = panicAutoReplyEnabled;
        TriageEnabled = triageEnabled;
        AutoReplyBotName = autoReplyBotName;
        PanicAutoReplyTemplate = panicAutoReplyTemplate;
        Categories = categories;
    }
}

[Serializable, NetSerializable]
public sealed class AhelpAdminConfigStateMessage : EntityEventArgs
{
    public AhelpAdminConfigState State { get; }

    public AhelpAdminConfigStateMessage(AhelpAdminConfigState state)
    {
        State = state;
    }
}