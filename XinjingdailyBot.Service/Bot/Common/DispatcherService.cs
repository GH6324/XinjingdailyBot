﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XinjingdailyBot.Infrastructure;
using XinjingdailyBot.Infrastructure.Attribute;
using XinjingdailyBot.Interface.Bot.Common;
using XinjingdailyBot.Interface.Bot.Handler;
using XinjingdailyBot.Interface.Data;
using XinjingdailyBot.Interface.Helper;
using XinjingdailyBot.Model.Models;

namespace XinjingdailyBot.Service.Bot.Common
{
    [AppService(ServiceType = typeof(IDispatcherService), ServiceLifetime = LifeTime.Scoped)]
    internal class DispatcherService : IDispatcherService
    {
        private readonly IMessageHandler _messageHandler;
        private readonly ICommandHandler _commandHandler;

        public DispatcherService(
            IMessageHandler messageHandler,
            ICommandHandler commandHandler)
        {
            _messageHandler = messageHandler;
            _commandHandler = commandHandler;
        }

        public async Task OnMessageReceived(Users dbUser, Message message)
        {
            var handler = message.Type switch
            {
                MessageType.Text => message.Text!.StartsWith("/") ?
                    _commandHandler.OnCommandReceived(dbUser, message) :
                    _messageHandler.OnTextMessageReceived(dbUser, message),
                MessageType.Photo => _messageHandler.OnMediaMessageReceived(dbUser, message),
                MessageType.Audio => _messageHandler.OnMediaMessageReceived(dbUser, message),
                MessageType.Video => _messageHandler.OnMediaMessageReceived(dbUser, message),
                MessageType.Voice => _messageHandler.OnMediaMessageReceived(dbUser, message),
                MessageType.Document => _messageHandler.OnMediaMessageReceived(dbUser, message),
                MessageType.Sticker => _messageHandler.OnMediaMessageReceived(dbUser, message),
                _ => null,
            };

            if (handler != null)
            {
                await handler;
            }
        }

        public async Task OnCallbackQueryReceived(Users dbUser, CallbackQuery query)
        {
            await _commandHandler.OnQueryCommandReceived(dbUser, query);
        }
    }
}
