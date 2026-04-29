admin-player-actions-window-admin-tools = Admin Tools

admin-tools-window-title = Admin Tools

admin-tools-section-quick = Quick Tools
admin-tools-section-quick-desc = Open the standard admin panels.
admin-tools-section-votes = Vote Tools
admin-tools-section-votes-desc = Start or cancel an in-game vote. Destructive votes require confirmation.
admin-tools-section-ships = Ship Tools
admin-tools-section-ships-desc = Save the ship registered to a deed entity to disk.
admin-tools-section-ahelp = Automated AHelp
admin-tools-section-ahelp-desc = Toggle and edit automatic responses sent in ahelp threads.
admin-tools-section-triage-desc = Toggle and edit the keyword rules used to classify ahelps.
admin-tools-section-panic = Panic Auto-Reply
admin-tools-section-panic-desc = Sends this reply on the first incoming message of each new ahelp chat.

admin-tools-button-player-panel = Player Panel
admin-tools-button-ban-panel = Ban Panel
admin-tools-button-admin-logs = Admin Logs
admin-tools-button-permissions = Permissions

admin-tools-vote-restart = Create Restart Vote
admin-tools-vote-preset = Create Preset Vote
admin-tools-vote-map = Create Map Vote
admin-tools-custom-vote-title = Custom Title
admin-tools-custom-vote-title-placeholder = Event vote, policy check, etc.
admin-tools-custom-vote-options = Options (;)
admin-tools-custom-vote-options-placeholder = yes;no;abstain
admin-tools-custom-vote-create = Create Custom Vote
admin-tools-cancel-vote = Cancel Vote
admin-tools-cancel-vote-id = Vote ID
admin-tools-cancel-vote-id-placeholder = Vote ID

admin-tools-ship-deed-id = Deed Entity ID
admin-tools-ship-deed-id-placeholder = 12345
admin-tools-ship-save = Save Ship

admin-tools-ahelp-enable = Enable Auto-AHelp
admin-tools-ahelp-disable = Disable Auto-AHelp
admin-tools-ahelp-category = Category
admin-tools-ahelp-template = Reply Template
admin-tools-ahelp-template-placeholder = Enter override text for selected category
admin-tools-ahelp-template-format =
    Format: plain text, sent verbatim and prefixed with "<Bot Name>: ".
    Rich-text tags supported: [color=red]…[/color], [bold]…[/bold], [italic]…[/italic], [head=2]…[/head], [bullet]…
    No placeholders are substituted — write the literal message you want the player to see.
admin-tools-ahelp-save = Save Override
admin-tools-ahelp-reset = Reset To Default
admin-tools-ahelp-note = Categories are edited live for this server runtime. Use Reset To Default to clear an override.
admin-tools-ahelp-refresh = Refresh
admin-tools-ahelp-state-loading = Auto-reply state loading...
admin-tools-ahelp-state-enabled = Auto-reply is enabled
admin-tools-ahelp-state-disabled = Auto-reply is disabled
admin-tools-ahelp-bot-name = Bot Display Name
admin-tools-ahelp-bot-name-placeholder = Helper Bot
admin-tools-ahelp-bot-name-save = Save Bot Name
admin-tools-ahelp-rule-state-loading = Rule state loading...
admin-tools-ahelp-rule-state-unselected = Select a category to manage this auto-reply rule.
admin-tools-ahelp-rule-state-enabled = Selected auto-reply rule is enabled
admin-tools-ahelp-rule-state-disabled = Selected auto-reply rule is disabled
admin-tools-ahelp-rule-state-enabled-category = Auto-reply rule "{$category}" is on
admin-tools-ahelp-rule-state-disabled-category = Auto-reply rule "{$category}" is off
admin-tools-ahelp-rule-enable = Enable Rule
admin-tools-ahelp-rule-disable = Disable Rule
admin-tools-ahelp-new-category = Category Name
admin-tools-ahelp-new-category-placeholder = New custom category name
admin-tools-ahelp-add-category = Add / Restore
admin-tools-ahelp-remove-category = Remove Selected
admin-tools-ahelp-enabled-tag =  [on]
admin-tools-ahelp-custom-tag =  [custom]
admin-tools-ahelp-disabled-tag =  [off]

admin-tools-panic-enable = Enable Panic Auto-Reply
admin-tools-panic-disable = Disable Panic Auto-Reply
admin-tools-panic-state-loading = Panic state loading...
admin-tools-panic-state-enabled = Panic auto-reply is enabled
admin-tools-panic-state-disabled = Panic auto-reply is disabled
admin-tools-panic-template = Panic Reply Template
admin-tools-panic-save = Save Panic Reply
admin-tools-panic-note = Panic auto-reply is shared across admins and triggers once per new incoming chat.

admin-tools-section-triage = AHelp Triage Rules
admin-tools-triage-enable = Enable Triage
admin-tools-triage-disable = Disable Triage
admin-tools-triage-keywords = Rule Keywords
admin-tools-triage-keywords-placeholder = failed to save, cant load, duplicate ship
admin-tools-triage-save = Save Rule Override
admin-tools-triage-reset = Reset Rule To Default
admin-tools-triage-note = Triage keyword rules are edited live for this server runtime. Separate keywords with commas or semicolons.
admin-tools-triage-state-loading = Triage state loading...
admin-tools-triage-state-enabled = Triage is enabled
admin-tools-triage-state-disabled = Triage is disabled
admin-tools-triage-rule-state-loading = Rule state loading...
admin-tools-triage-rule-state-unselected = Select a category to manage this triage rule.
admin-tools-triage-rule-state-enabled = Selected triage rule is enabled
admin-tools-triage-rule-state-disabled = Selected triage rule is disabled
admin-tools-triage-rule-state-enabled-category = Triage rule "{$category}" is on
admin-tools-triage-rule-state-disabled-category = Triage rule "{$category}" is off
admin-tools-triage-rule-enable = Enable Rule
admin-tools-triage-rule-disable = Disable Rule

admin-tools-vote-restart-tooltip = Starts a server-wide round restart vote. Requires confirmation.
admin-tools-vote-preset-tooltip = Starts a vote to change the round preset. Requires confirmation.
admin-tools-vote-map-tooltip = Starts a vote to change the next map. Requires confirmation.
admin-tools-cancel-vote-tooltip = Cancels the active vote with the supplied id. Requires confirmation.
admin-tools-ship-save-tooltip = Writes the ship for this deed to disk. Requires confirmation.
admin-tools-ahelp-reset-tooltip = Removes the override and restores the default reply for this category.
admin-tools-triage-reset-tooltip = Removes the override and restores the default keyword rule for this category.

admin-tools-section-saved = Saved Commands
admin-tools-section-saved-desc = Save console commands locally for one-click reuse. Local macros stay on this client; use Shared Library for admin-wide runtime sharing.
admin-tools-saved-name = Name
admin-tools-saved-name-placeholder = Short label, e.g. "Restart now"
admin-tools-saved-command = Command
admin-tools-saved-command-placeholder = Full console command, e.g. restartroundnow
admin-tools-saved-run = Run
admin-tools-saved-run-tooltip = Run the selected saved command (or double-click an entry).
admin-tools-saved-save = Save / Update
admin-tools-saved-save-tooltip = Save the name + command pair, replacing any existing entry with the same name.
admin-tools-saved-delete = Delete
admin-tools-saved-delete-tooltip = Remove the selected saved command. Requires confirmation.
admin-tools-saved-note = Saved commands persist across sessions on this client. They run with your current admin permissions, so review before using.
admin-tools-saved-write-error = Failed to write admin saved commands file.
admin-tools-shared-macros-open = Shared Library
admin-tools-shared-macros-open-tooltip = Open the shared admin macro library for copying to or from your local macros.
admin-tools-shared-macros-window-title = Shared Admin Macros
admin-tools-shared-macros-list = Shared Macros
admin-tools-shared-macros-preview = Macro Preview
admin-tools-shared-macros-refresh = Refresh
admin-tools-shared-macros-copy-to-local = Copy Selected To Local
admin-tools-shared-macros-copy-local-to-shared = Copy Current Local To Shared
admin-tools-shared-macros-delete = Delete Shared
admin-tools-shared-macros-note = Shared macros are stored server-side and persist across restarts. Copy one into your local editor to save or run it locally.
admin-tools-shared-macros-empty = No shared macro selected.
admin-tools-shared-macros-selected-meta = Last updated by {$updatedBy}

# Sub-tab titles for the integrated Admin Tools tab
admin-tools-tab-quick = Quick Tools
admin-tools-tab-votes = Votes
admin-tools-tab-ships = Ships
admin-tools-tab-ahelp = AHelp
admin-tools-tab-macros = Macros
admin-tools-tab-statistics = Statistics

# Vote sub-sections
admin-tools-subsection-custom-vote = Custom Vote
admin-tools-subsection-cancel-vote = Cancel Active Vote
admin-tools-subsection-vote-audit = Vote Audit
admin-tools-subsection-vote-audit-desc = Browse active and recent votes, select one to see who voted for each option.
admin-tools-vote-audit-open = Open Vote Audit
admin-tools-vote-audit-open-tooltip = Opens the Vote Audit window – select any vote to see the per-option voter breakdown.

# AHelp shared category selector
admin-tools-ahelp-shared-category = AHelp Category
admin-tools-ahelp-shared-category-desc = Select a category. Used by both Auto-Reply and Triage sections below.

admin-tools-section-statistics-desc = Live server and round overview for admins.
admin-tools-statistics-refresh = Refresh Statistics
admin-tools-statistics-updated-never = Last updated: never
admin-tools-statistics-updated-loading = Last updated: loading...
admin-tools-statistics-updated-error = Last updated: failed
admin-tools-statistics-updated-now = Last updated: {$time}
admin-tools-statistics-online-loading = Online players: loading...
admin-tools-statistics-lag-loading = Lag info: loading...
admin-tools-statistics-round-loading = Round info: loading...
admin-tools-statistics-uptime-loading = Server uptime: loading...
admin-tools-statistics-online-value = Online players: {$count}
admin-tools-statistics-lag-value = Lag: avg {$avg} ms, max {$max} ms, high-ping players {$high}
admin-tools-statistics-round-value = Round: #{$roundId}, {$runLevel}, duration {$duration}
admin-tools-statistics-uptime-value = Server uptime: {$uptime}
admin-tools-statistics-roles-header = Role Slots (Taken / Open)
admin-tools-statistics-roles-search = Search roles...
admin-tools-statistics-role-finite = {$name}: {$taken} taken, {$open} open
admin-tools-statistics-role-unlimited = {$name}: {$taken} taken, unlimited open
admin-tools-statistics-antags-header = Current Antagonists
admin-tools-statistics-antags-none = None
admin-tools-statistics-error = Statistics unavailable.
