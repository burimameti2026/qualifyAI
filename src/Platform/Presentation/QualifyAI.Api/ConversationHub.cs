using Microsoft.AspNetCore.SignalR;
namespace QualifyAI.Api;
public sealed class ConversationHub:Hub { public Task JoinConversation(string id)=>Groups.AddToGroupAsync(Context.ConnectionId,$"conversation:{id}"); public Task LeaveConversation(string id)=>Groups.RemoveFromGroupAsync(Context.ConnectionId,$"conversation:{id}"); }
