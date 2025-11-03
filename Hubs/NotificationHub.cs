using Microsoft.AspNetCore.SignalR;

public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user.IsInRole("RM_LineManagers")) await Groups.AddToGroupAsync(Context.ConnectionId, "RM_LineManagers");
        if (user.IsInRole("RM_Security")) await Groups.AddToGroupAsync(Context.ConnectionId, "RM_Security");
        if (user.IsInRole("RM_ITAdmins")) await Groups.AddToGroupAsync(Context.ConnectionId, "RM_ITAdmins");
        await base.OnConnectedAsync();
    }
}
