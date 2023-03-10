using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR
{
    public class MessageHub : Hub
    {
        private readonly IUnitOfWork _unitOfWork;

        private readonly IMapper _mapper;

        private readonly IHubContext<PresenceHub> _presenceHub;

        private readonly PresenceTraker _tracker;

        public MessageHub(IUnitOfWork unitOfWork, IMapper mapper, IHubContext<PresenceHub> presenceHub,PresenceTraker tracker)
        {
            _tracker = tracker;
            _presenceHub = presenceHub;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public override async Task OnConnectedAsync()
        {
            var httpConext = Context.GetHttpContext();
            var otherUser = httpConext.Request.Query["user"].ToString();
            var groupName = GetGroupName(Context.User.GetUsername(), otherUser);

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            var group = AddToGroup(groupName);

            await Clients.Group(groupName).SendAsync("Updatedgroup", group);

            var messgages = await _unitOfWork.MessageRepository.GetMessageThread(Context.User.GetUsername(), otherUser);

            if(_unitOfWork.HasChanges()) await _unitOfWork.Complete();

            await Clients.Caller.SendAsync("ReceiveMessageThread", messgages);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var group = await RemoveFromMessageGroup();
            await Clients.Group(group.Name).SendAsync("Updatedgroup", group);

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(CreateMessageDto  createMessageDto)
        {
            var username = Context.User.GetUsername();

            if(username == createMessageDto.RecipientUsername.ToLower()) throw new HubException("You cannot send a message to yourself");

            var sender = await _unitOfWork.UserRepository.GetUsernameAsync(username);
            var recipient = await _unitOfWork.UserRepository.GetUsernameAsync(createMessageDto.RecipientUsername);

            if(recipient == null) throw new HubException("User not found");

            var message = new Message
            {
                Sender = sender,
                Recipient = recipient,
                SenderUsername = sender.UserName,
                RecipientUsername = recipient.UserName,
                Content = createMessageDto.Content
            };

            var groupName = GetGroupName(sender.UserName, recipient.UserName);
            var group = await _unitOfWork.MessageRepository.GetMessageGroup(groupName);

            if(group.Connections.Any(m => m.Username == recipient.UserName))
            {
                message.DateRead = DateTime.UtcNow;
            }
            else
            {
                var connections = await _tracker.GetConnectionsForUser(recipient.UserName);
                if(connections != null)
                {
                    await _presenceHub.Clients.Clients(connections).SendAsync("NewMessageReceived", 
                    new { username = sender.UserName, knownAs = sender.KnownAs});
                }
            }

            _unitOfWork.MessageRepository.AddMessage(message);

            if(await _unitOfWork.Complete())
            {
                await Clients.Group(groupName).SendAsync("NewMessage", _mapper.Map<MessageDto>(message));
            }
        }

        private async Task<Group> AddToGroup(string groupName)
        {
            var group = await _unitOfWork.MessageRepository.GetMessageGroup(groupName);
            var connection = new Connection(Context.ConnectionId, Context.User.GetUsername());

            if(group == null)
            {
                group = new Group(groupName);
                _unitOfWork.MessageRepository.AddGroup(group);
            }
            group.Connections.Add(connection);

            if(await _unitOfWork.Complete()) return group;

            throw new HubException("Failed to join group");
        }

        private async Task<Group> RemoveFromMessageGroup()
        {
            var group = await _unitOfWork.MessageRepository.GetGroupForConnection(Context.ConnectionId);
            var connection = group.Connections.FirstOrDefault(i => i.ConnectionId == Context.ConnectionId);

            _unitOfWork.MessageRepository.RemoveConnection(connection);
            if(await _unitOfWork.Complete()) return group;

            throw new HubException("Failed to remove from group");
        }

        private string GetGroupName(string caller,string other)
        {
            var stringCompare = string.CompareOrdinal(caller, other) <0;
            return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
        }
    }
}