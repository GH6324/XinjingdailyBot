﻿using Telegram.Bot.Types;
using XinjingdailyBot.Model.Models;

namespace XinjingdailyBot.Interface.Bot.Handler
{
    public interface ICommandHandler
    {
        void InitCommands();
        Task OnCommandReceived(Users dbUser, Message message);
    }
}
