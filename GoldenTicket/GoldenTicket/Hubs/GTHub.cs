using GoldenTicket.Database;
using System.Collections.Concurrent;
using GoldenTicket.Utilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using GoldenTicket.Entities;
using GoldenTicket.Models;


namespace GoldenTicket.Hubs
{
    public class GTHub : Hub
    {
        #region General
        private static readonly ConcurrentDictionary<int, HashSet<string>> _connections = new ConcurrentDictionary<int, HashSet<string>>();
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var entry in _connections)
            {
                if (entry.Value.Contains(Context.ConnectionId))
                {
                    entry.Value.Remove(Context.ConnectionId);
                    if (entry.Value.Count == 0)
                    {
                        _connections.TryRemove(entry.Key, out _);
                    }
                    break;
                }
            }
            await base.OnDisconnectedAsync(exception);
        }



        public async Task Broadcast(string message)
        {
            await Clients.All.SendAsync("Announce", message);
        }

        public async Task Online(int userID, string role)
        {
            _connections.AddOrUpdate(userID, new HashSet<string> { Context.ConnectionId },
                (key, existingSet) => 
                { 
                    existingSet.Add(Context.ConnectionId);
                    return existingSet;
                });

            Console.WriteLine($"[SignalR] User {userID} has connections: {string.Join(", ", _connections[userID])}");

            bool isEmployee = role == "Employee"; 
            await Clients.Caller.SendAsync("Online", new 
            {
                tags = DBUtil.GetTags(), 
                faq = DBUtil.GetFAQs(), 
                users = DBUtil.GetUsersByRole(), 
                chatrooms = DBUtil.GetChatrooms(userID, isEmployee), 
                tickets = DBUtil.GetTickets(userID, isEmployee),
                status = DBUtil.GetStatuses(),
                priorities = DBUtil.GetPriorities()
            });
        }
        #endregion
        

        #region FAQ
        public async Task AddFAQ(string Title, string Description, string Solution, string MainTagName, string SubTagName) 
        {
            DBUtil.AddFAQ(Title, Description, Solution, MainTagName, SubTagName);
            await Clients.All.SendAsync("FAQUpdate", new {faq = DBUtil.GetFAQs()});
        }
        public async Task UpdateFAQ(int faqID, string Title, string Description, string Solution, string Maintag, string Subtag, bool IsArchived)
        {
            await DBUtil.UpdateFAQ(faqID,Title, Description, Solution, Maintag, Subtag, IsArchived);
            await Clients.All.SendAsync("FAQUpdate", new {faq = DBUtil.GetFAQs()});
        }
        #endregion

        


        #region Chatroom
        public async Task RequestChat(int AuthorID) 
        {
            int openChatroomsCount = DBUtil.GetChatrooms(AuthorID, true).Count(c => c.Ticket == null);
            if (openChatroomsCount >= 3)
            {
                await Clients.Caller.SendAsync("MaximumChatroom");
                return;
            }

            var chatroom = await DBUtil.AddChatroom(AuthorID);
            var adminUser = DBUtil.GetAdminUsers();
            var chatroomDTO = new ChatroomDTO(DBUtil.GetChatroom(chatroom.ChatroomID)!, true);
            var chatroomDTOAdmin = new ChatroomDTO(DBUtil.GetChatroom(chatroom.ChatroomID)!);

            await Clients.Caller.SendAsync("ReceiveSupport", new { chatroom = chatroomDTO });
        }


        public async Task ResolveTickets(List<ChatroomDTO> chatrooms){

            foreach(ChatroomDTO chatroom in chatrooms){
                foreach(var member in chatroom.GroupMembers)
                {
                    
                    if (_connections.TryGetValue(member.User.UserID, out var connectionIds))
                    {
                        foreach (var connectionId in connectionIds)
                        {
                            await Clients.Client(connectionId).SendAsync("ChatroomUpdate", new {chatroom = chatroom});
                        }
                    }
                }
            }
        }

        public async Task JoinChatroom(int UserID, int ChatroomID)
        {
            var chatroomDTO = new ChatroomDTO(DBUtil.GetChatroom(ChatroomID)!);
            if (chatroomDTO!.GroupMembers.Any(m => m.User.UserID == UserID))
            {
                await Clients.Caller.SendAsync("AlreadyMember");
                return;
            }
            chatroomDTO = DBUtil.JoinChatroom(UserID, ChatroomID);
            var userDTO = new UserDTO(DBUtil.FindUser(UserID));
            foreach(var member in chatroomDTO.GroupMembers)
            {
                if (_connections.TryGetValue(member.User.UserID, out var connectionIds)){
                    foreach (var connectionId in connectionIds)
                    {
                        await Clients.Client(connectionId).SendAsync("StaffJoined", new {user = userDTO, chatroom = chatroomDTO});
                    }
                }
            }
        }
        public async Task OpenChatroom(int UserID, int ChatroomID) 
        {
            var chatroomDTO = new ChatroomDTO(DBUtil.GetChatroom(ChatroomID)!, true);
            await UserSeen(UserID, ChatroomID);
            await Clients.Caller.SendAsync("ReceiveMessages", new {chatroom = chatroomDTO});
        }
        public async Task UserSeen(int UserID, int ChatroomID) 
        {
            var chatroomDTO = DBUtil.GetChatroom(ChatroomID);
            DBUtil.UpdateLastSeen(UserID, ChatroomID);
            foreach(var member in chatroomDTO!.Members) {
                if (_connections.TryGetValue(member.Member!.UserID, out var connectionIds)){
                    foreach (var connectionId in connectionIds)
                    {
                        await Clients.Client(connectionId).SendAsync("UserSeen", new {userID = UserID, chatroomID = ChatroomID});
                    }
                }
            }
        }
        public async Task SendMessage(int SenderID, int ChatroomID, string Message) 
        {
            var message = await DBUtil.SendMessage(SenderID, ChatroomID, Message);
            var messageDTO = new MessageDTO(DBUtil.GetMessage(message.MessageID)!);
            var chatroomDTO = new ChatroomDTO(DBUtil.GetChatroom(ChatroomID)!);
            foreach(var member in chatroomDTO.GroupMembers){
                if (member.User.Role == "Employee")
                {
                    if (_connections.TryGetValue(member.User!.UserID, out var connectionIds))
                    {
                        foreach (var connectionId in connectionIds)
                        {
                            await Clients.Client(connectionId).SendAsync("ReceiveMessage", new {chatroom = chatroomDTO, message = messageDTO});
                        }
                    }
                }
            }

            if(chatroomDTO.Ticket != null)
            {
                var adminUser = DBUtil.GetAdminUsers();
                foreach(var user in adminUser){
                    if(user.Role == "Admin" || user.Role == "Staff"){
                        
                        if (_connections.TryGetValue(user.UserID, out var connectionIds)){
                            foreach (var connectionId in connectionIds)
                            {
                                await Clients.Client(connectionId).SendAsync("ReceiveMessage", new {chatroom = chatroomDTO, message = messageDTO});

                            }
                        }
                    }
                    
                }
            }
            
            await UserSeen(SenderID, ChatroomID);
            if(chatroomDTO.Ticket == null)
            {
                await AISendMessage(ChatroomID, Message, SenderID);
            }
        }
        
        public async Task AISendMessage(int chatroomID, string userMessage, int userID) 
        {
            int SenderID = 100000001;
            var response = await AIUtil.GetJsonResponseAsync(chatroomID.ToString(), userMessage, userID);
            if (response == null)
            {
                response = AIResponse.Unavailable();
            }
          
            var message = await DBUtil.SendMessage(SenderID, chatroomID, response!.Message);
            var chatroomDTO = new ChatroomDTO(DBUtil.GetChatroom(chatroomID)!);
            var messageDTO = new MessageDTO(DBUtil.GetMessage(message.MessageID)!);
            foreach(var member in chatroomDTO.GroupMembers){
                if(member.User.UserID == userID){
                    if (_connections.TryGetValue(userID, out var connectionIds)){
                        foreach (var connectionId in connectionIds)
                        {
                            await Clients.Client(connectionId).SendAsync("ReceiveMessage", new {chatroom = chatroomDTO, message = messageDTO});
                            await Clients.Client(connectionId).SendAsync("AllowMessage");
                        }
                    }
                }
            }
            if(chatroomDTO.Ticket == null)
            {
                if(response.CallAgent)
                {
                    if (_connections.TryGetValue(userID, out var connectionIds)){
                        foreach (var connectionId in connectionIds)
                        {
                            await AddTicket(response.Title, userID, response.MainTag, response.SubTags, response.Priority, chatroomID);
                            chatroomDTO = new ChatroomDTO(DBUtil.GetChatroom(chatroomID)!);
                            await Clients.Client(connectionId).SendAsync("AllowMessage");
                        }
                    }
                }
            }
            
            await UserSeen(SenderID, chatroomID);
        }
        #endregion


        #region Tags
        public async Task AddMainTag(string TagName)
        {
            if(DBUtil.AddMainTag(TagName))
                await Clients.All.SendAsync("TagUpdate", new {tags = DBUtil.GetTags()});
            else
                await Clients.Caller.SendAsync("ExistingTag");
        }
        public async Task AddSubTag(string TagName, string MainTagName)
        {
            if(DBUtil.AddSubTag(TagName, MainTagName))
                await Clients.All.SendAsync("TagUpdate", new {tags = DBUtil.GetTags()});
            else
                await Clients.Caller.SendAsync("ExistingTag");
        }
        #endregion


        #region Ticket
        public async Task AddTicket(string TicketTitle, int AuthorID, string MainTagName, string SubTagName, string Priority, int ChatroomID)
        {
            var newTicket = await DBUtil.AddTicket(TicketTitle, AuthorID, MainTagName, SubTagName, Priority, ChatroomID);
            if (newTicket == null)
            {
                return;
            }
            var ticketDTO = new TicketDTO(DBUtil.GetTicket(newTicket.TicketID)!);
            var chatroomDTO = new ChatroomDTO(DBUtil.GetChatroom(ChatroomID)!);
            var adminUser = DBUtil.GetAdminUsers();
            foreach(var user in adminUser){
                if(user.Role == "Admin" || user.Role == "Staff"){
                    
                    if (_connections.TryGetValue(user.UserID, out var connectionIds)){
                        foreach (var connectionId in connectionIds)
                        {
                            await Clients.Client(connectionId).SendAsync("TicketUpdate", new {ticket = ticketDTO});
                            await Clients.Client(connectionId).SendAsync("ChatroomUpdate", new {chatroom = chatroomDTO});
                        }
                    }
                }
                
            }
            await Clients.Caller.SendAsync("TicketUpdate", new {ticket = ticketDTO});
            await Clients.Caller.SendAsync("ChatroomUpdate", new {chatroom = chatroomDTO});
                        
        }
        public async Task UpdateTicket(int TicketID, string Title, string Status, string Priority, string? MainTag, string? SubTag, int? AssignedID)
        {
            int EditorID = _connections.FirstOrDefault(kvp => kvp.Value.Contains(Context.ConnectionId)).Key;

            var updatedTicket = await DBUtil.UpdateTicket(TicketID, Title, Status, Priority, MainTag, SubTag, AssignedID, EditorID);
            var ticketDTO = new TicketDTO(DBUtil.GetTicket(TicketID)!);
            
            var chatroomDTO = DBUtil.GetChatrooms().Where(c => c.Ticket!.TicketID == TicketID).FirstOrDefault();
            int chatroomID = chatroomDTO?.ChatroomID ?? throw new InvalidOperationException("ChatroomID cannot be null.");


            // Chatroom Close
            if(Status == "Closed")
            {
                await DBUtil.CloseChatroom(chatroomID);
                chatroomDTO = DBUtil.GetChatrooms().Where(c => c.Ticket!.TicketID == TicketID).FirstOrDefault();
            }
            // Chatroom Reopen
            if(Status == "Open" && chatroomDTO!.IsClosed)
            {
                await DBUtil.ReopenChatroom(chatroomID);
                chatroomDTO = DBUtil.GetChatrooms().Where(c => c.Ticket!.TicketID == TicketID).FirstOrDefault();
            }

            var adminUser = DBUtil.GetAdminUsers();
            foreach (var user in adminUser)
            {
                if (user.Role == "Admin" || user.Role == "Staff")
                {
                    if (_connections.TryGetValue(user.UserID, out var connectionIds))
                    {
                        foreach (var connectionId in connectionIds)
                        {
                            await Clients.Client(connectionId).SendAsync("TicketUpdate", new { ticket = ticketDTO });
                            await Clients.Client(connectionId).SendAsync("ChatroomUpdate", new { chatroom = chatroomDTO });
                        }
                    }
                }
            }

            foreach (var member in chatroomDTO!.GroupMembers)
            {
                if (_connections.TryGetValue(member.User.UserID, out var connectionIds))
                {
                    foreach (var connectionId in connectionIds)
                    {
                        Console.WriteLine($"Broadcasted to User ID:{connectionId}");
                        await Clients.Client(connectionId).SendAsync("TicketUpdate", new { ticket = ticketDTO });
                        await Clients.Client(connectionId).SendAsync("ChatroomUpdate", new { chatroom = chatroomDTO });
                    }
                }
            }
        }
        public async Task OpenTicket(int TicketID)
        {
            var ticketDTO = new TicketDTO(DBUtil.GetTicket(TicketID)!);
            await Clients.Caller.SendAsync("TicketUpdate", new {ticket = ticketDTO});
        }
        #endregion
    }
}
