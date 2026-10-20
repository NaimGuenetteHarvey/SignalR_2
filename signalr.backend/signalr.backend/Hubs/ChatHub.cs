using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using signalr.backend.Data;
using signalr.backend.Models;

namespace signalr.backend.Hubs
{
    // On garde en mémoire les connexions actives (clé: email, valeur: userId)
    // Note: Ce n'est pas nécessaire dans le TP
    public static class UserHandler
    {
        public static Dictionary<string, string> UserConnections { get; set; } = new Dictionary<string, string>();
    }

    // L'annotation Authorize fonctionne de la même façon avec SignalR qu'avec Web API
    [Authorize]
    // Le Hub est le type de base des "contrôleurs" de SignalR
    public class ChatHub : Hub
    {
        public ApplicationDbContext _context;

        public IdentityUser CurentUser
        {
            get
            {
                // On récupère le userid à partir du Cookie qui devrait être envoyé automatiquement
                string userid = Context.UserIdentifier!;
                return _context.Users.Single(u => u.Id == userid);
            }
        }

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        private void AugmenterNbConnexions()
        {
            if (!UserHandler.UserConnections.ContainsKey(CurentUser.UserName!))
            {
                UserHandler.UserConnections[CurentUser.UserName!] = new UserData(){UserId = Context.UserIdentifier!};
            }
            UserHandler.UserConnections[CurentUser.UserName!].NbConnexions++;
        }

        private void ReduireNbConnexions()
        {
            UserData data = UserHandler.UserConnections[CurentUser.UserName!];
            data.NbConnexions--;
            if (data.NbConnexions == 0)
                UserHandler.UserConnections.Remove(CurentUser.UserName!);
        }

        public async override Task OnConnectedAsync()
        {
            AugmenterNbConnexions();
            

            // TODO: Envoyer des message aux clients pour les mettre à jour
            await Clients.All.SendAsync("UsersList", UserHandler.UserConnections.ToList());
            await Clients.Caller.SendAsync("ChannelsList", _context.Channel.ToList());
        }

        public async override Task OnDisconnectedAsync(Exception? exception)
        {
            ReduireNbConnexions();

            // TODO: Envoyer un message aux clients pour les mettre à jour
            await Clients.All.SendAsync("UsersList", UserHandler.UserConnections.ToList());
        }

        public async Task CreateChannel(string title)
        {
            _context.Channel.Add(new Channel { Title = title });
            await _context.SaveChangesAsync();

            // TODO: Envoyer un message aux clients pour les mettre à jour
            await Clients.All.SendAsync("ChannelsList", await _context.Channel.ToListAsync());
        }

        public async Task DeleteChannel(int channelId)
        {
            Channel channel = _context.Channel.Find(channelId);

            if(channel != null)
            {
                _context.Channel.Remove(channel);
                await _context.SaveChangesAsync();

                string groupName = CreateChannelGroupName(channelId);
                // Envoyer les messages nécessaires aux clients
                await Clients.Group(groupName).SendAsync("NewMessage", "[" + channel.Title + "] a été détruit");
                await Clients.Group(groupName).SendAsync("LeaveChannel");
                await Clients.All.SendAsync("ChannelsList", await _context.Channel.ToListAsync());
            }       
        }

        public async Task JoinChannel(int oldChannelId, int newChannelId)
        {
            string userTag = "[" + CurentUser.Email! + "]";

            // TODO: Faire quitter le vieux canal à l'utilisateur
            if (oldChannelId > 0)
            {
                string oldGroupName = CreateChannelGroupName(oldChannelId);
                Channel channel = _context.Channel.Find(oldChannelId);
                string message = userTag + " quitte: " + channel.Title;
                await Clients.Group(oldGroupName).SendAsync("NewMessage", message);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGroupName);
            }

            // TODO: Faire joindre le nouveau canal à l'utilisateur
            if (newChannelId > 0)
            {
                string newGroupName = CreateChannelGroupName(newChannelId);
                await Groups.AddToGroupAsync(Context.ConnectionId, newGroupName);

                Channel channel = _context.Channel.Find(newChannelId);
                string message = userTag + " a rejoint : " + channel.Title;
                await Clients.Group(newGroupName).SendAsync("NewMessage", message);
            }
        }

        public async Task SendMessage(string message, int channelId, string userId)
        {
            if (userId != null)
            {
                // TODO: Envoyer le message à cet utilisateur
                string messageWithTag = "[De: " + CurentUser.Email! + "] " + message;
                await Clients.User(userId).SendAsync("NewMessage", messageWithTag);
            }
            else if (channelId != 0)
            {
                // TODO: Envoyer le message aux utilisateurs connectés à ce canal
                string groupName = CreateChannelGroupName(channelId);
                Channel channel = _context.Channel.Find(channelId);
                await Clients.Group(groupName).SendAsync("NewMessage", "[" + channel.Title + "] " + message);
            }
            else
            {
                await Clients.All.SendAsync("NewMessage", "[Tous] " + message);
            }
        }

        private static string CreateChannelGroupName(int channelId)
        {
            return "Channel" + channelId;
        }
    }
}
