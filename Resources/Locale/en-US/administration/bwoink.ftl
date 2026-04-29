bwoink-user-title = Admin Message

bwoink-system-starmute-message-no-other-users = *System: Nobody is available to receive your message. Try pinging Game Admins on Discord.

bwoink-system-messages-being-relayed-to-discord =
    All messages are relayed to game administrators via Discord.
    Issues may be handled without a response.

bwoink-system-introductory-message =
    Please describe the issue that you have encountered in detail. Assume that the game administrator who is resolving the problem does not have first-hand knowledge of what has occurred.
    Please do not ask for special events or punishments for other players.
    Any bugs and other related issues should be reported through Discord or Github.
    Misuse of this message system may result in disciplinary action.

bwoink-system-typing-indicator = {$players} {$count ->
[one] is
*[other] are
} typing...

admin-ahelp-admin-only = Admin Only
admin-ahelp-admin-only-tooltip = If checked, then the message won't be visible for the player,
    but will be visible for other admins and still will be Discord relayed.

admin-bwoink-play-sound = Bwoink?

bwoink-title-none-selected = None selected

bwoink-system-rate-limited = System: you are sending messages too quickly.
bwoink-system-player-disconnecting = has disconnected.
bwoink-system-player-reconnecting = has reconnected.
bwoink-system-player-banned = has been banned for: {$banReason}

bwoink-message-admin-only = (Admin Only)
bwoink-message-silent = (S)
bwoink-message-triage = Triage: {$category}

# Auto-reply templates are now authored by admins via the AHelp tab; no defaults shipped.

# Triage shortcut bar (admin-side bwoink panel).
bwoink-triage-suggested-prefix = Suggested:
bwoink-triage-action-player-panel = Player Panel
bwoink-triage-action-respawn = Respawn
bwoink-triage-action-follow = Follow
bwoink-triage-action-notes = Notes
bwoink-triage-action-inspect-ships = Inspect Ships
bwoink-triage-ship-inspect-error = Ship inspection failed.
bwoink-triage-ship-inspect-none = No ships found for this player.
bwoink-triage-ship-inspect-header = Ships ({$count}):
bwoink-triage-action-snapshot = Snapshot
bwoink-triage-snapshot-error = Snapshot failed.
bwoink-triage-snapshot-header = Player snapshot:
bwoink-triage-popup-close = Close
bwoink-triage-popup-ships-title = Ship Inspection
bwoink-triage-popup-snapshot-title = Player Snapshot

# Banking management (admin-side)
bwoink-triage-action-banking = Banking
bwoink-triage-popup-banking-title = Banking Management
bwoink-triage-banking-error = Unable to retrieve bank information.

bwoink-banking-window-title = Bank Account: {$name}
bwoink-banking-balance-label = Current Balance:
bwoink-banking-amount-label = Amount:
bwoink-banking-reason-label = Reason:
bwoink-banking-reason-placeholder = Admin adjustment reason (optional)
bwoink-banking-add-button = Add Money
bwoink-banking-remove-button = Remove Money
bwoink-banking-confiscate-button = Confiscate all for CC
bwoink-banking-confiscate-confirm = Seize for Colonial Command?
bwoink-banking-confiscate-tooltip = Drains the player's entire bank balance to zero. Funds are deemed reclaimed by NT Colonial Command for "asset reallocation".
bwoink-banking-confiscate-reason-default = Assets reallocated by Colonial Command audit
bwoink-banking-confiscate-nothing = Account already empty - Colonial Command finds nothing to seize.
bwoink-banking-close-button = Close
bwoink-banking-invalid-amount = Please enter a valid positive amount.
bwoink-banking-sending = Processing...
bwoink-banking-success = Balance updated successfully.
bwoink-banking-error-send = Failed to send request. Please try again.
bwoink-banking-error-result = Error: {$error}
bwoink-banking-not-supported = Balance modifications are managed through in-game bank ATMs for game integrity.
bwoink-banking-audit-no-reason = no reason given
bwoink-banking-audit-add = Deposited {$amount} spesos. New balance: {$balance}. Reason: {$reason}
bwoink-banking-audit-remove = Withdrew {$amount} spesos. New balance: {$balance}. Reason: {$reason}
bwoink-banking-audit-confiscate = Confiscated {$amount} spesos for Colonial Command (account drained). Reason: {$reason}

# Unstick ship (admin tool)
bwoink-triage-action-unstick-ship = Unstick Ship
bwoink-triage-action-tp-station = TP to Station
bwoink-triage-action-tp-ship = TP to Ship
bwoink-triage-popup-unstick-title = Unstick Ship
bwoink-triage-popup-tp-station-title = Teleport to Station
bwoink-tp-station-success = [color=lightgreen][bold]Teleported[/bold][/color] player to [bold]{$destination}[/bold] at ({$x}, {$y}).
bwoink-tp-station-error-not-authorized = You are not authorized to teleport players.
bwoink-tp-station-error-invalid-owner = Invalid player identifier for this ahelp channel.
bwoink-tp-station-error-offline = Player is offline.
bwoink-tp-station-error-no-attached-entity = Player has no attached entity to teleport.
bwoink-tp-station-error-no-station = Could not resolve a station for this player.
bwoink-tp-station-error-no-arrivals = No arrivals/latejoin spawn point found.
bwoink-tp-station-error-generic = Teleport to station failed.

# Teleport to own ship (admin tool)
bwoink-triage-popup-tp-ship-title = Teleport to Ship
bwoink-tp-ship-success = [color=lightgreen][bold]Teleported[/bold][/color] player to their ship [bold]{$ship}[/bold] at ({$x}, {$y}).
bwoink-tp-ship-error-not-authorized = You are not authorized to teleport players.
bwoink-tp-ship-error-invalid-owner = Invalid player identifier for this ahelp channel.
bwoink-tp-ship-error-offline = Player is offline.
bwoink-tp-ship-error-no-attached-entity = Player has no attached entity to teleport.
bwoink-tp-ship-error-no-ship = No owned ship found for this player.
bwoink-tp-ship-error-in-ftl = Ship is currently in FTL — wait for the jump to finish.
bwoink-tp-ship-error-no-safe-tile = Could not find a safe tile to land on aboard the ship.
bwoink-tp-ship-error-generic = Teleport to ship failed.
bwoink-unstick-confirm = Are you sure you want to FTL [bold]{$ship}[/bold] to a nearby clear point?
bwoink-unstick-confirm-button = Confirm Unstick
bwoink-unstick-success = [color=#5cc8ff][bold]Unstuck[/bold][/color] [bold]{$ship}[/bold] — FTL'd to ({$x}, {$y}).
bwoink-unstick-audit = Unstuck [bold]{$ship}[/bold] via FTL nudge to ({$x}, {$y}).
bwoink-unstick-error-not-authorized = You are not authorized to unstick ships.
bwoink-unstick-error-empty-owner = No player owner ID provided.
bwoink-unstick-error-no-ship = No owned ship found for this player.
bwoink-unstick-error-in-ftl = Ship is already in FTL — wait for the jump to finish.
bwoink-unstick-error-no-position = Ship has no valid map position.
bwoink-unstick-error-no-clear-spot = Could not find a clear spot nearby. Try again or move the ship manually.
bwoink-unstick-error-ftl-failed = FTL refused (see server log).
bwoink-unstick-error-generic = Unstick failed for an unknown reason.

bwoink-triage-action-save-ship = Save Ship
bwoink-triage-popup-save-ship-title = Save Ship
bwoink-save-ship-confirm = Are you sure you want to force-save [bold]{$ship}[/bold]? This will teleport everyone aboard to the medbay rescue beacon and deliver the ship file to the owner's client.
bwoink-save-ship-confirm-button = Confirm Save Ship
bwoink-save-ship-success = [color=lightgreen][bold]Saved[/bold][/color] [bold]{$ship}[/bold] — file sent to owner's client. Evicted {$evicted} player(s) to the medbay rescue beacon.
bwoink-save-ship-error-not-authorized = You are not authorized to force-save player ships.
bwoink-save-ship-error-empty-owner = No player owner ID provided.
bwoink-save-ship-error-invalid-owner = Invalid player identifier for this ahelp channel.
bwoink-save-ship-error-no-ship = No owned ship found for this player.
bwoink-save-ship-error-owner-offline = Owner is offline — cannot deliver the ship file.
bwoink-save-ship-error-save-failed = Ship save threw an exception (see server log).
bwoink-save-ship-error-generic = Save Ship failed for an unknown reason.

# Assign Ship Deed (admin tool)
bwoink-triage-action-assign-deed = Assign Deed
bwoink-assign-deed-window-title = Assign Ship Deed — {$name}
bwoink-assign-deed-header = Select a ship to assign its deed to [bold]{$name}[/bold]'s ID card or PDA.
bwoink-assign-deed-refresh = Refresh
bwoink-assign-deed-preview-header = Ship Details
bwoink-assign-deed-preview-name = Ship:
bwoink-assign-deed-preview-owner = Current owner:
bwoink-assign-deed-preview-owner-id = Owner ID:
bwoink-assign-deed-no-owner = (unowned)
bwoink-assign-deed-no-selection = No ship selected.
bwoink-assign-deed-assign = Assign Deed
bwoink-assign-deed-success = [color=lightgreen][bold]Deed assigned[/bold][/color] — [bold]{$ship}[/bold] is now registered to this player's ID card.
bwoink-assign-deed-error-not-authorized = You are not authorized to assign ship deeds.
bwoink-assign-deed-error-invalid-owner = Invalid player identifier for this ahelp channel.
bwoink-assign-deed-error-offline = Player is offline.
bwoink-assign-deed-error-no-attached-entity = Player has no attached entity.
bwoink-assign-deed-error-no-id-card = Could not find an ID card on the player (checked hands, ID slot, and PDA).
bwoink-assign-deed-error-no-ship = The selected ship no longer exists.
bwoink-assign-deed-error-generic = Deed assignment failed.
