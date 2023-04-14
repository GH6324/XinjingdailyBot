﻿using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XinjingdailyBot.Infrastructure.Attribute;
using XinjingdailyBot.Infrastructure.Enums;
using XinjingdailyBot.Interface.Bot.Handler;
using XinjingdailyBot.Interface.Data;
using XinjingdailyBot.Interface.Helper;
using XinjingdailyBot.Model.Models;
using XinjingdailyBot.Repository;
using XinjingdailyBot.Service.Data;

namespace XinjingdailyBot.Service.Bot.Handler
{
    [AppService(typeof(IChannelPostHandler), LifeTime.Singleton)]
    public class ChannelPostHandler : IChannelPostHandler
    {
        private readonly IPostService _postService;
        private readonly ITextHelperService _textHelperService;
        private readonly IAttachmentService _attachmentService;
        private readonly IUserService _userService;
        private readonly IChannelOptionService _channelOptionService;
        private readonly TagRepository _tagRepository;
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<ChannelPostHandler> _logger;
        private readonly IMediaGroupService _mediaGroupService;

        public ChannelPostHandler(
            IPostService postService,
            ITextHelperService textHelperService,
            IAttachmentService attachmentService,
            IUserService userService,
            IChannelOptionService channelOptionService,
            TagRepository tagRepository,
            ITelegramBotClient botClient,
            ILogger<ChannelPostHandler> logger,
            IMediaGroupService mediaGroupService)
        {
            _postService = postService;
            _textHelperService = textHelperService;
            _attachmentService = attachmentService;
            _userService = userService;
            _channelOptionService = channelOptionService;
            _tagRepository = tagRepository;
            _botClient = botClient;
            _logger = logger;
            _mediaGroupService = mediaGroupService;
        }

        public async Task OnTextChannelPostReceived(Users dbUser, Message message)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                return;
            }
            if (message.Text.Length > IPostService.MaxPostText)
            {
                return;
            }

            string? channelName = null, channelTitle = null;
            if (message.ForwardFromChat?.Type == ChatType.Channel)
            {
                channelTitle = message.ForwardFromChat.Title;
                channelName = $"{message.ForwardFromChat.Username}/{message.ForwardFromMessageId}";
                long channelId = message.ForwardFromChat.Id;
                var option = await _channelOptionService.FetchChannelOption(channelId, message.ForwardFromChat.Username, channelTitle);

                if (option == ChannelOption.AutoReject)
                {
                    await _botClient.DeleteMessageAsync(message.Chat, message.MessageId);
                    _logger.LogInformation("删除消息 {msgid}", message.MessageId);
                    return;
                }
            }

            int newTag = _tagRepository.FetchTags(message.Text);
            string text = _textHelperService.ParseMessage(message);

            //生成数据库实体
            Posts newPost = new()
            {
                OriginChatID = message.Chat.Id,
                OriginMsgID = message.MessageId,
                ActionMsgID = 0,
                ReviewMsgID = 0,
                ManageMsgID = 0,
                PublicMsgID = message.MessageId,
                Anonymous = false,
                Text = text,
                RawText = message.Text ?? "",
                ChannelName = channelName ?? "",
                ChannelTitle = channelTitle ?? "",
                Status = PostStatus.Accepted,
                PostType = message.Type,
                NewTags = newTag,
                PosterUID = dbUser.UserID,
                ReviewerUID = dbUser.UserID
            };

            await _postService.Insertable(newPost).ExecuteCommandAsync();

            //增加通过数量
            dbUser.AcceptCount++;
            dbUser.ModifyAt = DateTime.Now;
            await _userService.Updateable(dbUser).UpdateColumns(x => new { x.AcceptCount, x.ModifyAt }).ExecuteCommandAsync();
        }

        public async Task OnMediaChannelPostReceived(Users dbUser, Message message)
        {
            string? channelName = null, channelTitle = null;
            if (message.ForwardFromChat?.Type == ChatType.Channel)
            {
                channelTitle = message.ForwardFromChat.Title;
                channelName = $"{message.ForwardFromChat.Username}/{message.ForwardFromMessageId}";
                long channelId = message.ForwardFromChat.Id;
                var option = await _channelOptionService.FetchChannelOption(channelId, message.ForwardFromChat.Username, channelTitle);

                if (option == ChannelOption.AutoReject)
                {
                    await _botClient.DeleteMessageAsync(message.Chat, message.MessageId);
                    _logger.LogInformation("删除消息 {msgid}", message.MessageId);
                    return;
                }
            }

            var newTags = _tagRepository.FetchTags(message.Caption);
            string text = _textHelperService.ParseMessage(message);

            //生成数据库实体
            Posts newPost = new()
            {
                OriginChatID = message.Chat.Id,
                OriginMsgID = message.MessageId,
                ActionMsgID = 0,
                ReviewMsgID = 0,
                ManageMsgID = 0,
                PublicMsgID = message.MessageId,
                Anonymous = false,
                Text = text,
                RawText = message.Text ?? "",
                ChannelName = channelName ?? "",
                ChannelTitle = channelTitle ?? "",
                Status = PostStatus.Accepted,
                PostType = message.Type,
                NewTags = newTags,
                HasSpoiler = message.HasMediaSpoiler ?? false,
                PosterUID = dbUser.UserID,
                ReviewerUID = dbUser.UserID
            };

            long postID = await _postService.Insertable(newPost).ExecuteReturnBigIdentityAsync();

            Attachments? attachment = _attachmentService.GenerateAttachment(message, postID);

            if (attachment != null)
            {
                await _attachmentService.Insertable(attachment).ExecuteCommandAsync();
            }

            //增加通过数量
            dbUser.AcceptCount++;
            dbUser.ModifyAt = DateTime.Now;
            await _userService.Updateable(dbUser).UpdateColumns(x => new { x.AcceptCount, x.ModifyAt }).ExecuteCommandAsync();
        }

        /// <summary>
        /// mediaGroupID字典
        /// </summary>
        private ConcurrentDictionary<string, long> MediaGroupIDs { get; } = new();

        public async Task OnMediaGroupChannelPostReceived(Users dbUser, Message message)
        {
            string mediaGroupId = message.MediaGroupId!;
            if (!MediaGroupIDs.TryGetValue(mediaGroupId, out long postID)) //如果mediaGroupId不存在则创建新Post
            {
                MediaGroupIDs.TryAdd(mediaGroupId, -1);

                bool exists = await _postService.Queryable().AnyAsync(x => x.MediaGroupID == mediaGroupId);
                if (!exists)
                {
                    string? channelName = null, channelTitle = null;
                    if (message.ForwardFromChat?.Type == ChatType.Channel)
                    {
                        channelTitle = message.ForwardFromChat.Title;
                        channelName = $"{message.ForwardFromChat.Username}/{message.ForwardFromMessageId}";
                        long channelId = message.ForwardFromChat.Id;
                        var option = await _channelOptionService.FetchChannelOption(channelId, message.ForwardFromChat.Username, channelTitle);

                        if (option == ChannelOption.AutoReject)
                        {
                            await _botClient.DeleteMessageAsync(message.Chat, message.MessageId);
                            _logger.LogInformation("删除消息 {msgid}", message.MessageId);
                            return;
                        }
                    }

                    int newTags = _tagRepository.FetchTags(message.Caption);
                    string text = _textHelperService.ParseMessage(message);

                    //生成数据库实体
                    Posts newPost = new()
                    {
                        OriginChatID = message.Chat.Id,
                        OriginMsgID = message.MessageId,
                        ActionMsgID = 0,
                        ReviewMsgID = 0,
                        ManageMsgID = 0,
                        PublicMsgID = message.MessageId,
                        Anonymous = false,
                        Text = text,
                        RawText = message.Text ?? "",
                        ChannelName = channelName ?? "",
                        ChannelTitle = channelTitle ?? "",
                        Status = PostStatus.Accepted,
                        PostType = message.Type,
                        NewTags = newTags,
                        HasSpoiler = message.HasMediaSpoiler ?? false,
                        PosterUID = dbUser.UserID,
                        ReviewerUID = dbUser.UserID
                    };

                    postID = await _postService.Insertable(newPost).ExecuteReturnBigIdentityAsync();

                    MediaGroupIDs[mediaGroupId] = postID;

                    //两秒后停止接收媒体组消息
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1500);
                        MediaGroupIDs.Remove(mediaGroupId, out _);

                        //增加通过数量
                        dbUser.AcceptCount++;
                        dbUser.ModifyAt = DateTime.Now;
                        await _userService.Updateable(dbUser).UpdateColumns(x => new { x.AcceptCount, x.ModifyAt }).ExecuteCommandAsync();
                    });
                }
            }

            if (postID > 0)
            {
                //更新附件
                Attachments? attachment = _attachmentService.GenerateAttachment(message, postID);
                if (attachment != null)
                {
                    await _attachmentService.Insertable(attachment).ExecuteCommandAsync();
                }

                //记录媒体组
                await _mediaGroupService.AddPostMediaGroup(message);
            }
        }
    }
}
