using System.Text.RegularExpressions;
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

        public async override Task OnConnectedAsync()
        {
            UserHandler.UserConnections.Add(CurentUser.Email!, Context.UserIdentifier);
            
            // TODO: Envoyer des message aux clients pour les mettre à jour
            await Clients.All.SendAsync("UsersList", UserHandler.UserConnections.ToList());
        }

        public async override Task OnDisconnectedAsync(Exception? exception)
        {
            // Lors de la fermeture de la connexion, on met à jour notre dictionnary d'utilisateurs connectés
            KeyValuePair<string, string> entrie = UserHandler.UserConnections.SingleOrDefault(uc => uc.Value == Context.UserIdentifier);
            UserHandler.UserConnections.Remove(entrie.Key);
            
            // TODO: Envoyer un message aux clients pour les mettre à jour
            await Clients.All.SendAsync("UsersList", UserHandler.UserConnections.ToList());
        }

        public async Task CreateChannel(string title)
        {
            _context.Channel.Add(new Channel { Title = title });
            await _context.SaveChangesAsync();

            // TODO: Envoyer un message aux clients pour les mettre à jour
            await Clients.All.SendAsync("ChannelList", await _context.Channel.ToListAsync());
        }

        public async Task DeleteChannel(int channelId)
        {
            Channel channel = _context.Channel.Find(channelId);

            if(channel != null)
            {
                _context.Channel.Remove(channel);
                await _context.SaveChangesAsync();
            }
            string groupName = CreateChannelGroupName(channelId);
            // Envoyer les messages nécessaires aux clients
            await Clients.Group(groupName).SendAsync("deleteChannel", await _context.Channel.ToListAsync());
        }

        public async Task JoinChannel(int oldChannelId, int newChannelId)
        {
            string userTag = "[" + CurentUser.Email! + "]";

            if (oldChannelId > 0)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, CreateChannelGroupName(oldChannelId));
                await Clients.Group(CreateChannelGroupName(oldChannelId)).SendAsync("NewMessage", userTag + " a quitté le canal");
            }

            if (newChannelId > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, CreateChannelGroupName(newChannelId));     
                await Clients.Group(CreateChannelGroupName(newChannelId)).SendAsync("joinChannel", userTag);
            }
        }

        public async Task SendMessage(string message, int channelId, string userId)
        {
            if (userId != null)
            {
                // Message privé : userId est un UserIdentifier → Clients.User
                await Clients.User(userId).SendAsync("NewMessage", "[Privé de " + CurentUser.Email + "] " + message);
            }
            else if (channelId != 0)
            {
                // Message de canal : seulement les membres du groupe
                await Clients.Group(CreateChannelGroupName(channelId)).SendAsync("NewMessage", "[Canal] " + message);
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