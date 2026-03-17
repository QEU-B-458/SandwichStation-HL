using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._NF.Shipyard.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class ShiftUpdateAnnouncementCommand : IConsoleCommand
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    public string Command => "update_ready";
    public string Description => "Sends the update scheduled announcement.";
    public string Help => "Usage: update_ready";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        _chatManager.DispatchServerAnnouncement("Update found! After this shift the server will be (temporary) down for updates!");
        shell.WriteLine("Server message sent.");
    }
}
